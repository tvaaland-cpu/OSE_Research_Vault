namespace OseResearchVault.Core.Models;

public sealed class AutomationRecord
{
    public required string AutomationId { get; init; }
    public required string WorkspaceId { get; init; }
    public required string Name { get; init; }
    public bool IsEnabled { get; init; } = true;
    public required string ScheduleType { get; init; }
    public int? IntervalMinutes { get; init; }
    public string? DailyTime { get; init; }
    public string? LastRunAt { get; init; }
    public string? NextRunAt { get; init; }
    public required string PayloadJson { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}
