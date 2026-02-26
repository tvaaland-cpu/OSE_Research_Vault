namespace OseResearchVault.Core.Models;

public sealed class ScenarioKpiRecord
{
    public string ScenarioKpiId { get; init; } = string.Empty;
    public string WorkspaceId { get; init; } = string.Empty;
    public string ScenarioId { get; init; } = string.Empty;
    public string KpiName { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public double Value { get; init; }
    public string? Unit { get; init; }
    public string? Currency { get; init; }
    public string? SnippetId { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}
