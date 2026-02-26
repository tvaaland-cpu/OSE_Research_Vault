namespace OseResearchVault.App.ViewModels;

public sealed class AskVaultContextItemViewModel
{
    public string ResultType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Excerpt { get; init; } = string.Empty;
    public string Citation { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
}
