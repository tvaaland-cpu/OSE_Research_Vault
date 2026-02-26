using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface ISnapshotService
{
    Task<SnapshotSaveResult> SaveUrlSnapshotAsync(string url, string workspaceId, string? companyId, string snapshotType, CancellationToken cancellationToken = default);
}
