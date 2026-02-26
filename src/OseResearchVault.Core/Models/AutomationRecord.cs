namespace OseResearchVault.Core.Models;

public sealed class AutomationRecord
{
    public string Name { get; init; } = string.Empty;
    public string ScheduleSummary { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
}
