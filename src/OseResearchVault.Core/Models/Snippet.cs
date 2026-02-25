namespace OseResearchVault.Core.Models;

public sealed class Snippet
{
    public string Id { get; init; } = string.Empty;
    public string WorkspaceId { get; init; } = string.Empty;
    public string? DocumentId { get; init; }
    public string? CompanyId { get; init; }
    public string? SourceId { get; init; }
    public string Locator { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string? CreatedBy { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}
