namespace OseResearchVault.Core.Models;

public sealed class EvidenceLink
{
    public string Id { get; init; } = string.Empty;
    public string WorkspaceId { get; init; } = string.Empty;
    public string ArtifactId { get; init; } = string.Empty;
    public string? SnippetId { get; init; }
    public string? DocumentId { get; init; }
    public string? Locator { get; init; }
    public string? Quote { get; init; }
    public double? RelevanceScore { get; init; }
    public string CreatedAt { get; init; } = string.Empty;

    public string? SnippetText { get; init; }
    public string? DocumentTitle { get; init; }
}
