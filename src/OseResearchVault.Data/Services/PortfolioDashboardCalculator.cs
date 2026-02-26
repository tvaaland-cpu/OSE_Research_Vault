using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public static class PortfolioDashboardCalculator
{
    public static PortfolioDashboardSnapshot Build(IReadOnlyList<PortfolioDashboardInputRow> rows)
    {
        if (rows.Count == 0)
        {
            return new PortfolioDashboardSnapshot();
        }

        var materialized = rows.Select(row =>
        {
            var costBasis = row.PositionStats.NetQuantity * row.PositionStats.AverageCost;
            var marketValue = row.LastPrice.HasValue ? row.LastPrice.Value * row.PositionStats.NetQuantity : null;
            var unrealized = marketValue.HasValue ? marketValue.Value - costBasis : null;

            return new PortfolioDashboardRow
            {
                CompanyId = row.CompanyId,
                CompanyName = row.CompanyName,
                Currency = row.Currency,
                Quantity = row.PositionStats.NetQuantity,
                AverageCost = row.PositionStats.AverageCost,
                CostBasis = costBasis,
                LastPrice = row.LastPrice,
                MarketValue = marketValue,
                RealizedPnl = row.PositionStats.RealizedPnl,
                UnrealizedPnl = unrealized
            };
        }).ToList();

        var hasAnyPrice = materialized.Any(r => r.LastPrice.HasValue);
        var allocationDenominator = hasAnyPrice
            ? materialized.Sum(r => r.MarketValue ?? 0d)
            : materialized.Sum(r => r.CostBasis);

        var rowsWithAllocation = materialized.Select(row =>
        {
            var allocationBase = hasAnyPrice ? row.MarketValue ?? 0d : row.CostBasis;
            var allocationPercent = allocationDenominator > 0d ? (allocationBase / allocationDenominator) * 100d : 0d;
            return new PortfolioDashboardRow
            {
                CompanyId = row.CompanyId,
                CompanyName = row.CompanyName,
                Currency = row.Currency,
                Quantity = row.Quantity,
                AverageCost = row.AverageCost,
                CostBasis = row.CostBasis,
                LastPrice = row.LastPrice,
                MarketValue = row.MarketValue,
                RealizedPnl = row.RealizedPnl,
                UnrealizedPnl = row.UnrealizedPnl,
                AllocationPercent = allocationPercent
            };
        }).ToList();

        var pricedRows = rowsWithAllocation.Where(r => r.UnrealizedPnl.HasValue).ToList();
        var hasCompletePricing = rowsWithAllocation.All(r => r.LastPrice.HasValue);

        return new PortfolioDashboardSnapshot
        {
            TotalInvested = rowsWithAllocation.Sum(r => r.CostBasis),
            TotalMarketValue = hasCompletePricing ? rowsWithAllocation.Sum(r => r.MarketValue ?? 0d) : null,
            TotalUnrealizedPnl = hasCompletePricing ? rowsWithAllocation.Sum(r => r.UnrealizedPnl ?? 0d) : null,
            TotalRealizedPnl = rowsWithAllocation.Sum(r => r.RealizedPnl),
            Rows = rowsWithAllocation,
            BiggestWinner = pricedRows.OrderByDescending(r => r.UnrealizedPnl).FirstOrDefault(),
            BiggestLoser = pricedRows.OrderBy(r => r.UnrealizedPnl).FirstOrDefault()
        };
    }
}
