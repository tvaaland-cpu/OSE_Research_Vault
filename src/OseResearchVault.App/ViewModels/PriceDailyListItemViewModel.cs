namespace OseResearchVault.App.ViewModels;

public sealed class PriceDailyListItemViewModel
{
    public string PriceDate { get; init; } = string.Empty;
    public string CloseDisplay { get; init; } = string.Empty;
    public string Currency { get; init; } = "NOK";
}
