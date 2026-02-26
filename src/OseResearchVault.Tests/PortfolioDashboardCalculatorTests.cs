using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class PortfolioDashboardCalculatorTests
{
    [Fact]
    public void Build_ComputesTotals_Allocation_AndWinners()
    {
        var snapshot = PortfolioDashboardCalculator.Build(
        [
            new PortfolioDashboardInputRow
            {
                CompanyId = "c1",
                CompanyName = "Alpha",
                Currency = "NOK",
                PositionStats = new PositionStats { NetQuantity = 10, AverageCost = 100, RealizedPnl = 50 },
                LastPrice = 120
            },
            new PortfolioDashboardInputRow
            {
                CompanyId = "c2",
                CompanyName = "Beta",
                Currency = "NOK",
                PositionStats = new PositionStats { NetQuantity = 5, AverageCost = 200, RealizedPnl = -20 },
                LastPrice = 180
            }
        ]);

        Assert.Equal(2000, snapshot.TotalInvested);
        Assert.Equal(2100, snapshot.TotalMarketValue);
        Assert.Equal(100, snapshot.TotalUnrealizedPnl);
        Assert.Equal(30, snapshot.TotalRealizedPnl);

        var alpha = Assert.Single(snapshot.Rows.Where(r => r.CompanyId == "c1"));
        var beta = Assert.Single(snapshot.Rows.Where(r => r.CompanyId == "c2"));
        Assert.Equal(57.14, alpha.AllocationPercent, 2);
        Assert.Equal(42.86, beta.AllocationPercent, 2);
        Assert.Equal("Alpha", snapshot.BiggestWinner?.CompanyName);
        Assert.Equal("Beta", snapshot.BiggestLoser?.CompanyName);
    }
}
