namespace OseResearchVault.App.ViewModels;

public sealed class DocumentListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string PublishedDate { get; init; } = string.Empty;
    public string ImportedDate { get; init; } = string.Empty;
}
