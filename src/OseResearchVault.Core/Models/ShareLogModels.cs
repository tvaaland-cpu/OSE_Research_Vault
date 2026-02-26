namespace OseResearchVault.Core.Models;

public sealed class ShareLogCreateRequest
{
    public string WorkspaceId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? TargetCompanyId { get; init; }
    public string? ProfileId { get; init; }
    public string OutputPath { get; init; } = string.Empty;
    public string? Summary { get; init; }
}

public sealed class ShareLogRecord
{
    public string ShareLogId { get; init; } = string.Empty;
    public string WorkspaceId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? TargetCompanyId { get; init; }
    public string? TargetCompanyName { get; init; }
    public string? ProfileId { get; init; }
    public string? ProfileName { get; init; }
    public string OutputPath { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public string? Summary { get; init; }
}
