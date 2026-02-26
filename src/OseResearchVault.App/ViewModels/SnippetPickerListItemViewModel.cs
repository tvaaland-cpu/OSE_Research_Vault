namespace OseResearchVault.App.ViewModels;

public sealed class SnippetPickerListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string DocumentId { get; init; } = string.Empty;
    public string DocumentTitle { get; init; } = string.Empty;
    public string Locator { get; init; } = string.Empty;
    public string TextPreview { get; init; } = string.Empty;
}
