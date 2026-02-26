namespace OseResearchVault.App.ViewModels;

public sealed class ArtifactEvidenceListItemViewModel
{
    public string EvidenceLinkId { get; init; } = string.Empty;
    public string? SnippetId { get; init; }
    public string? DocumentId { get; init; }
    public string SnippetPreview { get; init; } = string.Empty;
    public string Locator { get; init; } = string.Empty;
    public string DocumentTitle { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
}
