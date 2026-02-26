namespace OseResearchVault.App.ViewModels;

public sealed class DocumentSnippetListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string? CompanyId { get; init; }
    public string DocumentTitle { get; init; } = string.Empty;
    public string Locator { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
}
