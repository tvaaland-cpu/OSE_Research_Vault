namespace OseResearchVault.Core.Models;

public sealed class AutomationTemplateRecord
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ScheduleSummary { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
}
