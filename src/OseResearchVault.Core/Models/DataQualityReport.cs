namespace OseResearchVault.Core.Models;

public sealed class DataQualityReport
{
    public IReadOnlyList<DuplicateDocumentGroup> Duplicates { get; init; } = [];
    public IReadOnlyList<DataQualityUnlinkedItem> UnlinkedDocuments { get; init; } = [];
    public IReadOnlyList<DataQualityUnlinkedItem> UnlinkedNotes { get; init; } = [];
    public IReadOnlyList<DataQualityArtifactGap> EvidenceGaps { get; init; } = [];
    public IReadOnlyList<DataQualityMetricIssue> MetricEvidenceIssues { get; init; } = [];
    public IReadOnlyList<DataQualitySnippetIssue> SnippetIssues { get; init; } = [];
}

public sealed class DuplicateDocumentGroup
{
    public string ContentHash { get; init; } = string.Empty;
    public IReadOnlyList<DataQualityDocumentItem> Documents { get; init; } = [];
}

public sealed class DataQualityDocumentItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public string ImportedAt { get; init; } = string.Empty;
    public bool IsArchived { get; init; }
}

public sealed class DataQualityUnlinkedItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
}

public sealed class DataQualityArtifactGap
{
    public string ArtifactId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
}

public sealed class DataQualityMetricIssue
{
    public string MetricId { get; init; } = string.Empty;
    public string MetricKey { get; init; } = string.Empty;
    public string RecordedAt { get; init; } = string.Empty;
}

public sealed class DataQualitySnippetIssue
{
    public string SnippetId { get; init; } = string.Empty;
    public string Locator { get; init; } = string.Empty;
    public string? DocumentId { get; init; }
    public string? SourceId { get; init; }
}
