namespace OseResearchVault.App.ViewModels;

public sealed class AutomationRunListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string TriggerType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StartedAt { get; init; } = string.Empty;
    public string FinishedAt { get; init; } = string.Empty;
}
