namespace OseResearchVault.Core.Models;

public sealed class AutomationRunRecord
{
    public string Id { get; init; } = string.Empty;
    public string AutomationId { get; init; } = string.Empty;
    public string TriggerType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StartedAt { get; init; } = string.Empty;
    public string? FinishedAt { get; init; }
}
