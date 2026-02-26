using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteRestoreService(IAppSettingsService appSettingsService) : IRestoreService
{
    public async Task<WorkspaceSummary> RestoreWorkspaceFromZipAsync(
        string zipPath,
        string destinationFolder,
        string? newWorkspaceName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);

        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("Backup zip was not found.", zipPath);
        }

        var workspacePath = Path.GetFullPath(destinationFolder.Trim());
        if (Directory.Exists(workspacePath) && Directory.EnumerateFileSystemEntries(workspacePath).Any())
        {
            throw new InvalidOperationException("Destination folder must be empty before restore.");
        }

        Directory.CreateDirectory(workspacePath);
        var dataDirectory = Path.Combine(workspacePath, "data");
        var vaultDirectory = Path.Combine(workspacePath, "vault");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(vaultDirectory);

        string? restoredWorkspaceId;
        string? restoredWorkspaceName;

        using (var archive = ZipFile.OpenRead(zipPath))
        {
            var manifestEntry = archive.GetEntry("manifest.json");
            var dbEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".db", StringComparison.OrdinalIgnoreCase));
            if (manifestEntry is null || dbEntry is null)
            {
                throw new InvalidDataException("Backup zip must contain manifest.json and a database file.");
            }

            var manifest = await ReadManifestAsync(manifestEntry, cancellationToken);
            restoredWorkspaceId = manifest.WorkspaceId;
            restoredWorkspaceName = manifest.WorkspaceName;

            var dbDestinationPath = Path.Combine(dataDirectory, "ose-research-vault.db");
            await using (var dbSource = dbEntry.Open())
            await using (var dbDestination = File.Create(dbDestinationPath))
            {
                await dbSource.CopyToAsync(dbDestination, cancellationToken);
            }

            var vaultEntries = archive.Entries
                .Where(entry => entry.FullName.StartsWith("vault/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Name))
                .ToList();

            foreach (var vaultEntry in vaultEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = vaultEntry.FullName["vault/".Length..]
                    .Replace('/', Path.DirectorySeparatorChar);
                var destinationPath = Path.GetFullPath(Path.Combine(vaultDirectory, relativePath));
                if (!destinationPath.StartsWith(vaultDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var destinationEntryDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationEntryDir))
                {
                    Directory.CreateDirectory(destinationEntryDir);
                }

                await using var source = vaultEntry.Open();
                await using var destination = File.Create(destinationPath);
                await source.CopyToAsync(destination, cancellationToken);
            }
        }

        var databasePath = Path.Combine(dataDirectory, "ose-research-vault.db");
        var dbWorkspace = await EnsureWorkspaceMetadataAsync(databasePath, restoredWorkspaceId, restoredWorkspaceName, workspacePath, cancellationToken);

        var displayName = string.IsNullOrWhiteSpace(newWorkspaceName) ? dbWorkspace.Name : newWorkspaceName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Restored Workspace";
        }

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        settings.Workspaces.RemoveAll(w => string.Equals(w.Path, workspacePath, StringComparison.OrdinalIgnoreCase));
        settings.Workspaces.RemoveAll(w => w.Id == dbWorkspace.Id);
        settings.Workspaces.Add(new WorkspaceSetting
        {
            Id = dbWorkspace.Id,
            Name = displayName,
            Path = workspacePath,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        });

        await appSettingsService.SaveSettingsAsync(settings, cancellationToken);

        return new WorkspaceSummary
        {
            Id = dbWorkspace.Id,
            Name = displayName,
            Path = workspacePath,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        };
    }

    private static async Task<BackupManifest> ReadManifestAsync(ZipArchiveEntry manifestEntry, CancellationToken cancellationToken)
    {
        await using var stream = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, cancellationToken: cancellationToken);
        return manifest ?? new BackupManifest();
    }

    private static async Task<WorkspaceInfo> EnsureWorkspaceMetadataAsync(
        string databasePath,
        string? fallbackWorkspaceId,
        string? fallbackWorkspaceName,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<WorkspaceInfo>(new CommandDefinition(
            "SELECT id, name FROM workspace ORDER BY created_at LIMIT 1",
            cancellationToken: cancellationToken));

        if (row is not null)
        {
            return row;
        }

        var workspaceId = string.IsNullOrWhiteSpace(fallbackWorkspaceId) ? Guid.NewGuid().ToString() : fallbackWorkspaceId;
        var workspaceName = string.IsNullOrWhiteSpace(fallbackWorkspaceName) ? "Restored Workspace" : fallbackWorkspaceName;
        var now = DateTimeOffset.UtcNow.ToString("O");

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO workspace (id, name, description, created_at, updated_at) VALUES (@Id, @Name, @Description, @Now, @Now)",
            new { Id = workspaceId, Name = workspaceName, Description = workspacePath, Now = now },
            cancellationToken: cancellationToken));

        return new WorkspaceInfo { Id = workspaceId, Name = workspaceName };
    }

    private sealed class BackupManifest
    {
        [JsonPropertyName("workspace_id")]
        public string? WorkspaceId { get; set; }

        [JsonPropertyName("workspace_name")]
        public string? WorkspaceName { get; set; }
    }

    private sealed class WorkspaceInfo
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }
}
