namespace OseResearchVault.Core.Models;

public sealed class SnippetSearchResult
{
    public string Id { get; init; } = string.Empty;
    public string WorkspaceId { get; init; } = string.Empty;
    public string DocumentId { get; init; } = string.Empty;
    public string? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public string DocumentTitle { get; init; } = string.Empty;
    public string Locator { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
}
