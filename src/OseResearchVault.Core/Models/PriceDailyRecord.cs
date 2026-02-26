namespace OseResearchVault.Core.Models;

public sealed class PriceDailyRecord
{
    public string PriceId { get; init; } = string.Empty;
    public string? WorkspaceId { get; init; }
    public string CompanyId { get; init; } = string.Empty;
    public string PriceDate { get; init; } = string.Empty;
    public double Close { get; init; }
    public string Currency { get; init; } = "NOK";
    public string? SourceId { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}
