namespace OseResearchVault.App.ViewModels;

public sealed class ArtifactEvidenceLinkListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string DocumentTitle { get; init; } = string.Empty;
    public string Locator { get; init; } = string.Empty;
    public string SnippetId { get; init; } = string.Empty;
    public string Quote { get; init; } = string.Empty;
    public bool HasMissingLocator { get; init; }
    public string Citation { get; init; } = string.Empty;
}
