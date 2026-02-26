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
}
