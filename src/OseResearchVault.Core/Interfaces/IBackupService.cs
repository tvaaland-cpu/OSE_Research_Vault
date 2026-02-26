namespace OseResearchVault.Core.Interfaces;

public interface IBackupService
{
    Task ExportWorkspaceBackupAsync(string workspaceId, string outputZipPath, CancellationToken cancellationToken = default);
}

public interface IRestoreService
{
    Task<Core.Models.WorkspaceSummary> RestoreWorkspaceFromZipAsync(
        string zipPath,
        string destinationFolder,
        string? newWorkspaceName = null,
        CancellationToken cancellationToken = default);
}
