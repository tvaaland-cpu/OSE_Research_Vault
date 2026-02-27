namespace OseResearchVault.Core.Models;

public sealed class AutomationRecord
{
    private string _id = string.Empty;
    private bool _enabled;
    private string _payload = string.Empty;

    public string Id
    {
        get => _id;
        init => _id = value ?? string.Empty;
    }

    public string AutomationId
    {
        get => _id;
        init => _id = value ?? string.Empty;
    }

    public string WorkspaceId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ScheduleSummary { get; init; } = string.Empty;
    public string ScheduleType { get; init; } = "interval";
    public int? IntervalMinutes { get; init; }
    public string? DailyTime { get; init; }

    public bool Enabled
    {
        get => _enabled;
        init => _enabled = value;
    }

    public bool IsEnabled
    {
        get => _enabled;
        init => _enabled = value;
    }

    public string Payload
    {
        get => _payload;
        init => _payload = value ?? string.Empty;
    }

    public string PayloadJson
    {
        get => _payload;
        init => _payload = value ?? string.Empty;
    }

    public string PayloadType { get; init; } = "AskMyVault";
    public string? AgentId { get; init; }
    public string CompanyScopeMode { get; init; } = "global";
    public string CompanyScopeIdsJson { get; init; } = "[]";
    public string QueryText { get; init; } = string.Empty;
    public string? LastRunAt { get; init; }
    public string? NextRunAt { get; init; }
    public string? LastStatus { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}
