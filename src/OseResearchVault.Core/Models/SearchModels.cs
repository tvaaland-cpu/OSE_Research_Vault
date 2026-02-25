namespace OseResearchVault.Core.Models;

public sealed class SearchQuery
{
    public string QueryText { get; init; } = string.Empty;
    public string? WorkspaceId { get; init; }
    public string? CompanyId { get; init; }
    public string? Type { get; init; }
    public string? DateFromIso { get; init; }
    public string? DateToIso { get; init; }
}

public sealed class SearchResultRecord
{
    public string ResultType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string WorkspaceId { get; init; } = string.Empty;
    public string? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public string Title { get; init; } = string.Empty;
    public string MatchSnippet { get; init; } = string.Empty;
    public string OccurredAt { get; init; } = string.Empty;
    public double Rank { get; init; }
}

public sealed class WorkspaceRecord
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}
