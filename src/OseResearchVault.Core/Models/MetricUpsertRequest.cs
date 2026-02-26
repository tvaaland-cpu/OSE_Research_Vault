namespace OseResearchVault.Core.Models;

public sealed class MetricUpsertRequest
{
    public required string CompanyId { get; init; }
    public required string MetricName { get; init; }
    public required string Period { get; init; }
    public double? Value { get; init; }
    public string? Unit { get; init; }
    public string? Currency { get; init; }
}

public enum MetricConflictResolution
{
    CreateOnly = 0,
    ReplaceExisting = 1,
    CreateAnyway = 2
}

public enum MetricUpsertStatus
{
    Created = 0,
    Replaced = 1,
    ConflictDetected = 2,
    CreatedAnyway = 3
}

public sealed class MetricUpsertResult
{
    public required MetricUpsertStatus Status { get; init; }
    public required string MetricId { get; init; }
    public required string NormalizedMetricName { get; init; }
}
