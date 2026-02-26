using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IPositionAnalyticsService
{
    Task<PositionStats> GetPositionStatsAsync(string workspaceId, string companyId, string? positionId = null, double? latestPrice = null, CancellationToken cancellationToken = default);
}
