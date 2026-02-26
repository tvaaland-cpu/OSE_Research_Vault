namespace OseResearchVault.App.ViewModels;

public sealed class ScenarioKpiListItemViewModel
{
    public string ScenarioKpiId { get; init; } = string.Empty;
    public string KpiName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string ValueDisplay { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string SnippetId { get; set; } = string.Empty;
}
