namespace OseResearchVault.Core.Models;

public sealed class CompanyMetricUpdateRequest
{
    public string MetricName { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public double? Value { get; init; }
    public string? Unit { get; init; }
    public string? Currency { get; init; }
}
