namespace OseResearchVault.App.ViewModels;

public sealed class TradeListItemViewModel
{
    public string TradeId { get; init; } = string.Empty;
    public string TradeDate { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;
    public double Quantity { get; init; }
    public double Price { get; init; }
    public double Fee { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? Note { get; init; }
    public string PositionLabel => string.IsNullOrWhiteSpace(Note) ? "" : Note;
}
