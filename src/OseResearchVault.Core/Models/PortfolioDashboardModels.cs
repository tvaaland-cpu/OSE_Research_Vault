namespace OseResearchVault.Core.Models;

public sealed class PortfolioDashboardSnapshot
{
    public double TotalInvested { get; init; }
    public double? TotalMarketValue { get; init; }
    public double? TotalUnrealizedPnl { get; init; }
    public double TotalRealizedPnl { get; init; }
    public IReadOnlyList<PortfolioDashboardRow> Rows { get; init; } = [];
    public PortfolioDashboardRow? BiggestWinner { get; init; }
    public PortfolioDashboardRow? BiggestLoser { get; init; }
}

public sealed class PortfolioDashboardRow
{
    public string CompanyId { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public double Quantity { get; init; }
    public double AverageCost { get; init; }
    public double CostBasis { get; init; }
    public double? LastPrice { get; init; }
    public double? MarketValue { get; init; }
    public double RealizedPnl { get; init; }
    public double? UnrealizedPnl { get; init; }
    public double? TotalPnl => UnrealizedPnl.HasValue ? UnrealizedPnl.Value + RealizedPnl : null;
    public double AllocationPercent { get; init; }
}

public sealed class PortfolioDashboardInputRow
{
    public string CompanyId { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public PositionStats PositionStats { get; init; } = new();
    public double? LastPrice { get; init; }
}
