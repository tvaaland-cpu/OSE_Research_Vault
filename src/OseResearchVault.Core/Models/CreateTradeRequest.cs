namespace OseResearchVault.Core.Models;

public sealed class CreateTradeRequest
{
    public string WorkspaceId { get; init; } = string.Empty;
    public string CompanyId { get; init; } = string.Empty;
    public string? PositionId { get; init; }
    public string TradeDate { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;
    public double Quantity { get; init; }
    public double Price { get; init; }
    public double Fee { get; init; }
    public string Currency { get; init; } = "NOK";
    public string? Note { get; init; }
    public string? SourceId { get; init; }
}
