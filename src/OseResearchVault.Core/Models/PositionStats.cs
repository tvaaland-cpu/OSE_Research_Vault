namespace OseResearchVault.Core.Models;

public sealed class PositionStats
{
    public double NetQuantity { get; init; }
    public double AverageCost { get; init; }
    public double RealizedPnl { get; init; }
    public double TotalFees { get; init; }
    public double? CurrentExposure { get; init; }
}
