namespace OseResearchVault.Core.Models;

public sealed class CompanyMetricRecord
{
    public string Id { get; init; } = string.Empty;
    public string MetricName { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public double? Value { get; init; }
    public string? Unit { get; init; }
    public string? Currency { get; init; }
    public string? SnippetId { get; init; }
    public string? DocumentId { get; init; }
    public string? DocumentTitle { get; init; }
    public string? Locator { get; init; }
    public string? SourceTitle { get; init; }
    public string? SnippetText { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}
