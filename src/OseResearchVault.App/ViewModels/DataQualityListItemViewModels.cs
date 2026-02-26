namespace OseResearchVault.App.ViewModels;

public sealed class DataQualityUnlinkedListItemViewModel : ViewModelBase
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
}

public sealed class DataQualityDuplicateGroupViewModel : ViewModelBase
{
    public string ContentHash { get; init; } = string.Empty;
    public string DisplayLabel { get; init; } = string.Empty;
    public IReadOnlyList<DataQualityDuplicateDocumentViewModel> Documents { get; init; } = [];
}

public sealed class DataQualityDuplicateDocumentViewModel : ViewModelBase
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string ImportedAt { get; init; } = string.Empty;
}

public sealed class DataQualityArtifactGapViewModel : ViewModelBase
{
    public string ArtifactId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
}

public sealed class DataQualityMetricIssueViewModel : ViewModelBase
{
    public string MetricId { get; init; } = string.Empty;
    public string MetricKey { get; init; } = string.Empty;
    public string RecordedAt { get; init; } = string.Empty;
}

public sealed class DataQualitySnippetIssueViewModel : ViewModelBase
{
    public string SnippetId { get; init; } = string.Empty;
    public string Locator { get; init; } = string.Empty;
    public string ParentReference { get; init; } = string.Empty;
}
