using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;

namespace OseResearchVault.Data.Services;

public sealed class SqliteBackupService(IAppSettingsService appSettingsService) : IBackupService
{
    public async Task ExportWorkspaceBackupAsync(string workspaceId, string outputZipPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputZipPath);

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspace = await GetWorkspaceAsync(settings.DatabaseFilePath, workspaceId, cancellationToken);

        var outputDirectory = Path.GetDirectoryName(outputZipPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-backup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var tempDatabasePath = Path.Combine(tempRoot, "ose-research-vault.db");
            await VacuumIntoAsync(settings.DatabaseFilePath, tempDatabasePath, cancellationToken);

            var manifest = new
            {
                app_version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
                schema_migration_versions = await GetAppliedMigrationsAsync(settings.DatabaseFilePath, cancellationToken),
                exported_at = DateTimeOffset.UtcNow.ToString("O"),
                workspace_id = workspace.Id,
                workspace_name = workspace.Name
            };

            var manifestPath = Path.Combine(tempRoot, "manifest.json");
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, manifestJson, cancellationToken);

            if (File.Exists(outputZipPath))
            {
                File.Delete(outputZipPath);
            }

            using var archive = ZipFile.Open(outputZipPath, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(tempDatabasePath, "database/ose-research-vault.db");
            archive.CreateEntryFromFile(manifestPath, "manifest.json");

            if (File.Exists(AppPaths.SettingsFilePath))
            {
                archive.CreateEntryFromFile(AppPaths.SettingsFilePath, "settings/settings.json");
            }

            if (Directory.Exists(settings.VaultStorageDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(settings.VaultStorageDirectory, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relativePath = Path.GetRelativePath(settings.VaultStorageDirectory, file)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    archive.CreateEntryFromFile(file, $"vault/{relativePath}");
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static async Task VacuumIntoAsync(string sourceDatabasePath, string destinationDatabasePath, CancellationToken cancellationToken)
    {
        if (File.Exists(destinationDatabasePath))
        {
            File.Delete(destinationDatabasePath);
        }

        await using var connection = new SqliteConnection($"Data Source={sourceDatabasePath}");
        await connection.OpenAsync(cancellationToken);

        var escapedPath = destinationDatabasePath.Replace("'", "''", StringComparison.Ordinal);
        await connection.ExecuteAsync(new CommandDefinition($"VACUUM INTO '{escapedPath}'", cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<string>> GetAppliedMigrationsAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);

        var versions = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT version FROM schema_migrations ORDER BY version",
            cancellationToken: cancellationToken));

        return versions.ToList();
    }

    private static async Task<WorkspaceRow> GetWorkspaceAsync(string databasePath, string workspaceId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);

        WorkspaceRow? row;
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            row = await connection.QuerySingleOrDefaultAsync<WorkspaceRow>(new CommandDefinition(
                "SELECT id, name FROM workspace ORDER BY created_at LIMIT 1",
                cancellationToken: cancellationToken));
        }
        else
        {
            row = await connection.QuerySingleOrDefaultAsync<WorkspaceRow>(new CommandDefinition(
                "SELECT id, name FROM workspace WHERE id = @WorkspaceId",
                new { WorkspaceId = workspaceId },
                cancellationToken: cancellationToken));
        }

        return row ?? new WorkspaceRow { Id = string.IsNullOrWhiteSpace(workspaceId) ? "unknown" : workspaceId, Name = "Default Workspace" };
    }

    private sealed class WorkspaceRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }
}
