namespace OseResearchVault.Core.Models;

public sealed class PriceImportResult
{
    public int InsertedOrUpdatedCount { get; init; }
    public int SkippedCount { get; init; }
    public string SourceId { get; init; } = string.Empty;
    public string DocumentId { get; init; } = string.Empty;
}
