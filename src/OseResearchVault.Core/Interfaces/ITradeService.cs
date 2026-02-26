using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface ITradeService
{
    Task<TradeRecord> CreateTradeAsync(CreateTradeRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradeRecord>> ListTradesAsync(string workspaceId, string companyId, string? positionId = null, CancellationToken cancellationToken = default);
}
