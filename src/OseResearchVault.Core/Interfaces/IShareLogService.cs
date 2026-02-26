using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IShareLogService
{
    Task AddAsync(ShareLogCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShareLogRecord>> GetRecentAsync(string workspaceId, int limit = 200, CancellationToken cancellationToken = default);
}
