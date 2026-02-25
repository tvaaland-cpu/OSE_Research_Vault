namespace OseResearchVault.Core.Models;

public sealed class DocumentRecord
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string DocType { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string? PublishedAt { get; init; }
    public string ImportedAt { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string ContentHash { get; init; } = string.Empty;
    public string? ExtractedText { get; init; }
}
