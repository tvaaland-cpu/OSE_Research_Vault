namespace OseResearchVault.App.ViewModels;

public sealed class PriceDailyListItemViewModel
{
    public string PriceDate { get; init; } = string.Empty;
    public string CloseDisplay { get; init; } = string.Empty;
    public string Currency { get; init; } = "NOK";
    public string? SourceId { get; init; }
    public string EvidenceDisplay => string.IsNullOrWhiteSpace(SourceId) ? "—" : $"Source {SourceId[..Math.Min(8, SourceId.Length)]}…";
}
