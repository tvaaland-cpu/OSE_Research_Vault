using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class WorkspaceService(
    IAppSettingsService appSettingsService,
    IDatabaseInitializer databaseInitializer,
    IAutomationScheduler automationScheduler) : IWorkspaceService
{
    public async Task<IReadOnlyList<WorkspaceSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        return settings.Workspaces
            .OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToSummary)
            .ToList();
    }

    public async Task<WorkspaceSummary> CreateAsync(string name, string folderPath, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ArgumentException("Workspace name is required.", nameof(name));
        }

        var basePath = Path.GetFullPath(folderPath.Trim());
        Directory.CreateDirectory(basePath);
        var dataDirectory = Path.Combine(basePath, "data");
        var vaultDirectory = Path.Combine(basePath, "vault");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(vaultDirectory);

        var workspace = new WorkspaceSetting
        {
            Id = Guid.NewGuid().ToString(),
            Name = trimmedName,
            Path = basePath,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        };

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        settings.Workspaces.RemoveAll(w => string.Equals(w.Path, basePath, StringComparison.OrdinalIgnoreCase));
        settings.Workspaces.Add(workspace);
        settings.CurrentWorkspaceId = workspace.Id;
        settings.DatabaseDirectory = dataDirectory;
        settings.VaultStorageDirectory = vaultDirectory;
        await appSettingsService.SaveSettingsAsync(settings, cancellationToken);

        await databaseInitializer.InitializeAsync(cancellationToken);
        await EnsureWorkspaceRowAsync(settings.DatabaseFilePath, workspace, cancellationToken);

        return ToSummary(workspace);
    }

    public async Task<WorkspaceSummary> CloneWorkspaceAsync(string sourceWorkspaceId, string destinationFolder, string newName, CancellationToken cancellationToken = default)
    {
        var trimmedName = newName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ArgumentException("Workspace name is required.", nameof(newName));
        }

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var sourceWorkspace = settings.Workspaces.FirstOrDefault(w => w.Id == sourceWorkspaceId);
        if (sourceWorkspace is null)
        {
            throw new InvalidOperationException("Source workspace was not found.");
        }

        var destinationPath = Path.GetFullPath(destinationFolder.Trim());
        if (string.Equals(sourceWorkspace.Path, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Destination folder must be different from the source workspace folder.");
        }
        if (Directory.Exists(destinationPath) && Directory.EnumerateFileSystemEntries(destinationPath).Any())
        {
            throw new InvalidOperationException("Destination folder must be empty.");
        }

        Directory.CreateDirectory(destinationPath);
        var destinationDataDirectory = Path.Combine(destinationPath, "data");
        var destinationVaultDirectory = Path.Combine(destinationPath, "vault");
        Directory.CreateDirectory(destinationDataDirectory);
        Directory.CreateDirectory(destinationVaultDirectory);

        var sourceDatabasePath = Path.Combine(sourceWorkspace.Path, "data", "ose-research-vault.db");
        var destinationDatabasePath = Path.Combine(destinationDataDirectory, "ose-research-vault.db");
        await CloneDatabaseAsync(sourceDatabasePath, destinationDatabasePath, cancellationToken);

        var sourceVaultDirectory = Path.Combine(sourceWorkspace.Path, "vault");
        CopyDirectory(sourceVaultDirectory, destinationVaultDirectory);

        var workspace = new WorkspaceSetting
        {
            Id = Guid.NewGuid().ToString(),
            Name = trimmedName,
            Path = destinationPath,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        };

        settings.Workspaces.RemoveAll(w => string.Equals(w.Path, destinationPath, StringComparison.OrdinalIgnoreCase));
        settings.Workspaces.Add(workspace);
        settings.CurrentWorkspaceId = workspace.Id;
        settings.DatabaseDirectory = destinationDataDirectory;
        settings.VaultStorageDirectory = destinationVaultDirectory;

        await EnsureWorkspaceRowAsync(destinationDatabasePath, workspace, cancellationToken);

        await automationScheduler.StopAsync(cancellationToken);
        await appSettingsService.SaveSettingsAsync(settings, cancellationToken);
        await automationScheduler.StartAsync(cancellationToken);

        return ToSummary(workspace);
    }

    public async Task<bool> SwitchAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspace = settings.Workspaces.FirstOrDefault(w => w.Id == workspaceId);
        if (workspace is null)
        {
            return false;
        }

        await automationScheduler.StopAsync(cancellationToken);

        settings.CurrentWorkspaceId = workspace.Id;
        settings.DatabaseDirectory = Path.Combine(workspace.Path, "data");
        settings.VaultStorageDirectory = Path.Combine(workspace.Path, "vault");
        await appSettingsService.SaveSettingsAsync(settings, cancellationToken);

        await databaseInitializer.InitializeAsync(cancellationToken);
        await EnsureWorkspaceRowAsync(settings.DatabaseFilePath, workspace, cancellationToken);
        await automationScheduler.StartAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteReferenceAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var removed = settings.Workspaces.RemoveAll(w => w.Id == workspaceId) > 0;
        if (!removed)
        {
            return false;
        }

        if (settings.CurrentWorkspaceId == workspaceId)
        {
            settings.CurrentWorkspaceId = settings.Workspaces.FirstOrDefault()?.Id;
        }

        await appSettingsService.SaveSettingsAsync(settings, cancellationToken);
        return true;
    }

    public async Task<WorkspaceSummary?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspace = settings.Workspaces.FirstOrDefault(w => w.Id == settings.CurrentWorkspaceId);
        return workspace is null ? null : ToSummary(workspace);
    }

    private static WorkspaceSummary ToSummary(WorkspaceSetting setting) => new()
    {
        Id = setting.Id,
        Name = setting.Name,
        Path = setting.Path,
        CreatedAt = setting.CreatedAt
    };

    private static async Task EnsureWorkspaceRowAsync(string databasePath, WorkspaceSetting workspace, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString());
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT OR IGNORE INTO workspace (id, name, description, created_at, updated_at) VALUES (@Id, @Name, @Path, @Now, @Now)",
            new { workspace.Id, workspace.Name, Path = workspace.Path, Now = DateTimeOffset.UtcNow.ToString("O") },
            cancellationToken: cancellationToken));
    }

    private static async Task CloneDatabaseAsync(string sourceDatabasePath, string destinationDatabasePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourceDatabasePath))
        {
            throw new FileNotFoundException("Could not find source workspace database.", sourceDatabasePath);
        }

        if (File.Exists(destinationDatabasePath))
        {
            File.Delete(destinationDatabasePath);
        }

        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = sourceDatabasePath,
            Mode = SqliteOpenMode.ReadOnly,
            ForeignKeys = true
        }.ToString();

        var destinationConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = destinationDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();

        await using var sourceConnection = new SqliteConnection(sourceConnectionString);
        await sourceConnection.OpenAsync(cancellationToken);
        await using var destinationConnection = new SqliteConnection(destinationConnectionString);
        await destinationConnection.OpenAsync(cancellationToken);
        sourceConnection.BackupDatabase(destinationConnection);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }
}
