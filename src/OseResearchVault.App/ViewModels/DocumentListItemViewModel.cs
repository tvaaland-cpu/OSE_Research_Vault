namespace OseResearchVault.App.ViewModels;

public sealed class DocumentListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string PublishedDate { get; init; } = string.Empty;
    public string ImportedDate { get; init; } = string.Empty;
    public bool IsSelected { get; set; }
}
