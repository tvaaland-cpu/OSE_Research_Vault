namespace OseResearchVault.App.ViewModels;

public sealed class CompanyMetricListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string ValueDisplay { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string EvidenceDisplay { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string? DocumentId { get; init; }
    public string? SnippetId { get; init; }
    public string? Locator { get; init; }
    public string? SourceTitle { get; init; }
    public string? SnippetText { get; init; }
    public double? Value { get; set; }
}
