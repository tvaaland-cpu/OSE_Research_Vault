namespace OseResearchVault.App.ViewModels;

public sealed class ArtifactListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
