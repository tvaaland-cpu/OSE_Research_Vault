namespace OseResearchVault.Core.Models;

public sealed class MetricCreateRequest
{
    public string CompanyId { get; init; } = string.Empty;
    public string MetricName { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public double Value { get; init; }
    public string? Unit { get; init; }
    public string? Currency { get; init; }
    public string SnippetId { get; init; } = string.Empty;
}
