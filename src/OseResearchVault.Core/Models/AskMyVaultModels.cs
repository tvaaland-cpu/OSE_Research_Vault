namespace OseResearchVault.Core.Models;

public sealed class AskMyVaultPreviewRequest
{
    public string Query { get; init; } = string.Empty;
    public string? CompanyId { get; init; }
    public int MaxContextItems { get; init; } = 24;
}

public sealed class AskMyVaultContextItem
{
    public string ResultType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Excerpt { get; init; } = string.Empty;
    public string Citation { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
}

public sealed class AskMyVaultPreviewResult
{
    public IReadOnlyList<AskMyVaultContextItem> ContextItems { get; init; } = [];
    public string Prompt { get; init; } = string.Empty;
}
