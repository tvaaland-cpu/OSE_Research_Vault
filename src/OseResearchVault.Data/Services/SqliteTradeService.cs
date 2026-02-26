using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteTradeService(ITradeRepository tradeRepository) : ITradeService
{
    public Task<TradeRecord> CreateTradeAsync(CreateTradeRequest request, CancellationToken cancellationToken = default)
        => tradeRepository.CreateTradeAsync(request, cancellationToken);

    public Task<IReadOnlyList<TradeRecord>> ListTradesAsync(string workspaceId, string companyId, string? positionId = null, CancellationToken cancellationToken = default)
        => tradeRepository.ListTradesAsync(workspaceId, companyId, positionId, cancellationToken);
}
