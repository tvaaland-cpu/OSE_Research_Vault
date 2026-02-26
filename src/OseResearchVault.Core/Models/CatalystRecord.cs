namespace OseResearchVault.Core.Models;

public sealed class CatalystRecord
{
    public string CatalystId { get; init; } = string.Empty;
    public string WorkspaceId { get; init; } = string.Empty;
    public string CompanyId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ExpectedStart { get; init; }
    public string? ExpectedEnd { get; init; }
    public string Status { get; init; } = "open";
    public string Impact { get; init; } = "med";
    public string? Notes { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
    public IReadOnlyList<string> SnippetIds { get; init; } = [];
}
