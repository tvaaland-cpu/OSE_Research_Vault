namespace OseResearchVault.Core.Models;

public sealed class CatalystUpsertRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ExpectedStart { get; init; }
    public string? ExpectedEnd { get; init; }
    public string Status { get; init; } = "open";
    public string Impact { get; init; } = "med";
    public string? Notes { get; init; }
}
