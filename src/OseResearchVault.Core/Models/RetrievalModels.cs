namespace OseResearchVault.Core.Models;

public sealed class ContextPack
{
    public string Query { get; init; } = string.Empty;
    public IReadOnlyList<ContextPackItem> Items { get; init; } = [];
    public RetrievalLog Log { get; init; } = new();
}

public sealed class ContextPackItem
{
    public string ItemType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string TextExcerpt { get; init; } = string.Empty;
    public string SourceRef { get; init; } = string.Empty;
    public string Locator { get; init; } = string.Empty;
    public string CitationLabel { get; init; } = string.Empty;
}

public sealed class RetrievalLog
{
    public string Query { get; init; } = string.Empty;
    public string WorkspaceId { get; init; } = string.Empty;
    public string? CompanyId { get; init; }
    public int NoteCount { get; init; }
    public int DocumentCount { get; init; }
    public int SnippetCount { get; init; }
    public int ArtifactCount { get; init; }
}
