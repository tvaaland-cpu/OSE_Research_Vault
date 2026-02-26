namespace OseResearchVault.Core.Interfaces;

public interface IBackupService
{
    Task ExportWorkspaceBackupAsync(string workspaceId, string outputZipPath, CancellationToken cancellationToken = default);
}
