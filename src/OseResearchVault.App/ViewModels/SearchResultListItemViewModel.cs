namespace OseResearchVault.App.ViewModels;

public sealed class SearchResultListItemViewModel
{
    public string ResultType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string OccurredAt { get; init; } = string.Empty;
    public string Snippet { get; init; } = string.Empty;
}
