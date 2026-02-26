namespace OseResearchVault.App.ViewModels;

public sealed class ScenarioListItemViewModel
{
    public string ScenarioId { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Probability { get; set; }
    public string ProbabilityDisplay => Probability.ToString("P1");
    public string Assumptions { get; set; } = string.Empty;
}
