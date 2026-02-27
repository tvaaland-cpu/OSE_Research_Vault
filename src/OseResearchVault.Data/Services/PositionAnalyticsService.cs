using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class PositionAnalyticsService(ITradeRepository tradeRepository) : IPositionAnalyticsService
{
    // Uses average-cost accounting: sell lots realize P&L against the current weighted average unit cost.
    public async Task<PositionStats> GetPositionStatsAsync(string workspaceId, string companyId, string? positionId = null, double? latestPrice = null, CancellationToken cancellationToken = default)
    {
        var trades = await tradeRepository.ListTradesAsync(workspaceId, companyId, positionId, cancellationToken);

        var netQuantity = 0d;
        var costPool = 0d;
        var realizedPnl = 0d;
        var totalFees = 0d;

        foreach (var trade in trades)
        {
            totalFees += trade.Fee;

            if (string.Equals(trade.Side, "buy", StringComparison.OrdinalIgnoreCase))
            {
                netQuantity += trade.Quantity;
                costPool += (trade.Quantity * trade.Price) + trade.Fee;
                continue;
            }

            if (netQuantity <= 0)
            {
                continue;
            }

            var avgCostBeforeSell = costPool / netQuantity;
            var sellQuantity = Math.Min(trade.Quantity, netQuantity);
            realizedPnl += ((trade.Price - avgCostBeforeSell) * sellQuantity) - trade.Fee;
            netQuantity -= sellQuantity;
            costPool -= avgCostBeforeSell * sellQuantity;
        }

        var averageCost = netQuantity > 0 ? costPool / netQuantity : 0d;
        var currentExposure = latestPrice.HasValue ? (double?)(latestPrice.Value * netQuantity) : null;

        return new PositionStats
        {
            NetQuantity = netQuantity,
            AverageCost = averageCost,
            RealizedPnl = realizedPnl,
            TotalFees = totalFees,
            CurrentExposure = currentExposure
        };
    }
}
