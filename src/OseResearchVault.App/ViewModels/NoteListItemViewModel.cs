namespace OseResearchVault.App.ViewModels;

public sealed class NoteListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}
