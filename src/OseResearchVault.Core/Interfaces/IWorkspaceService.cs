using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IWorkspaceService
{
    Task<IReadOnlyList<WorkspaceSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceSummary> CreateAsync(string name, string folderPath, CancellationToken cancellationToken = default);
    Task<WorkspaceSummary> CloneWorkspaceAsync(string sourceWorkspaceId, string destinationFolder, string newName, CancellationToken cancellationToken = default);
    Task<bool> SwitchAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<bool> DeleteReferenceAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<WorkspaceSummary?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
