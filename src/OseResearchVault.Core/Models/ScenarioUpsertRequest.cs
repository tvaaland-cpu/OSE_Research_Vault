namespace OseResearchVault.Core.Models;

public sealed class ScenarioUpsertRequest
{
    public string Name { get; init; } = string.Empty;
    public double Probability { get; init; }
    public string? Assumptions { get; init; }
}
