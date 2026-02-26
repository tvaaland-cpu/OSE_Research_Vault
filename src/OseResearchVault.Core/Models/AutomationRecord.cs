namespace OseResearchVault.Core.Models;

public sealed class AutomationRecord
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string ScheduleType { get; init; } = "interval";
    public int? IntervalMinutes { get; init; }
    public string? DailyTime { get; init; }
    public string PayloadType { get; init; } = "AskMyVault";
    public string? AgentId { get; init; }
    public string CompanyScopeMode { get; init; } = "global";
    public string CompanyScopeIdsJson { get; init; } = "[]";
    public string QueryText { get; init; } = string.Empty;
    public string? NextRunAt { get; init; }
    public string? LastRunAt { get; init; }
    public string? LastStatus { get; init; }
}
