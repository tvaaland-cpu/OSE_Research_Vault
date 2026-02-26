namespace OseResearchVault.Core.Models;

public sealed class NotificationRecord
{
    public string NotificationId { get; init; } = string.Empty;
    public string WorkspaceId { get; init; } = string.Empty;
    public string Level { get; init; } = "info";
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public bool IsRead { get; init; }
}
