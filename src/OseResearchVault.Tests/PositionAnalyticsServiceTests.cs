using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class PositionAnalyticsServiceTests
{
    [Fact]
    public async Task AverageCostMath_AndFees_AreComputed()
    {
        var trades = new List<TradeRecord>
        {
            new() { TradeId = "t1", WorkspaceId = "w", CompanyId = "c", TradeDate = "2025-01-01", Side = "buy", Quantity = 10, Price = 100, Fee = 10, Currency = "NOK" },
            new() { TradeId = "t2", WorkspaceId = "w", CompanyId = "c", TradeDate = "2025-01-02", Side = "buy", Quantity = 10, Price = 120, Fee = 0, Currency = "NOK" },
            new() { TradeId = "t3", WorkspaceId = "w", CompanyId = "c", TradeDate = "2025-01-03", Side = "sell", Quantity = 5, Price = 130, Fee = 5, Currency = "NOK" }
        };

        var repository = new InMemoryTradeRepository(trades);
        var service = new PositionAnalyticsService(repository);

        var stats = await service.GetPositionStatsAsync("w", "c", latestPrice: 140);

        Assert.Equal(15d, stats.NetQuantity, 6);
        Assert.Equal(110.5d, stats.AverageCost, 6);
        Assert.Equal(92.5d, stats.RealizedPnl, 6);
        Assert.Equal(15d, stats.TotalFees, 6);
        Assert.Equal((double?)2100d, stats.CurrentExposure);
    }

    private sealed class InMemoryTradeRepository(IReadOnlyList<TradeRecord> trades) : ITradeRepository
    {
        public Task<TradeRecord> CreateTradeAsync(CreateTradeRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<TradeRecord>> ListTradesAsync(string workspaceId, string companyId, string? positionId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(trades);
    }
}
