namespace OseResearchVault.Core.Models;

public sealed class ScenarioRecord
{
    public string ScenarioId { get; init; } = string.Empty;
    public string WorkspaceId { get; init; } = string.Empty;
    public string CompanyId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public double Probability { get; init; }
    public string? Assumptions { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}
