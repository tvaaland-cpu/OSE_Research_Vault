namespace OseResearchVault.Core.Models;

public enum MemoPublishFormat
{
    Markdown = 0,
    Pdf = 1
}

public sealed class MemoPublishRequest
{
    public required string NoteId { get; init; }
    public required string NoteTitle { get; init; }
    public required string NoteContent { get; init; }
    public string? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public MemoPublishFormat Format { get; init; } = MemoPublishFormat.Markdown;
    public RedactionOptions RedactionOptions { get; init; } = new();
    public bool IncludeCitationsList { get; init; } = true;
    public bool IncludeEvidenceExcerpts { get; init; }
}

public sealed class MemoPublishResult
{
    public string OutputFilePath { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string DocumentId { get; init; } = string.Empty;
}
