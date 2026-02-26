namespace OseResearchVault.App.ViewModels;

public sealed class PortfolioAllocationRowViewModel
{
    public string Company { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public double Quantity { get; init; }
    public double AverageCost { get; init; }
    public string LastPrice { get; init; } = "N/A";
    public string MarketValue { get; init; } = "N/A";
    public string Pnl { get; init; } = "N/A";
    public string Allocation { get; init; } = "0.00%";
    public bool IsOpen { get; init; }
}
