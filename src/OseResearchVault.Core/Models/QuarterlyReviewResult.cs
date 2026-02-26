namespace OseResearchVault.Core.Models;

public sealed class QuarterlyReviewResult
{
    public string NoteId { get; init; } = string.Empty;
    public string NoteTitle { get; init; } = string.Empty;
    public int JournalEntriesCount { get; init; }
    public int DocumentCount { get; init; }
    public int EvidenceCount { get; init; }
    public int ScenarioCount { get; init; }
    public int CatalystCount { get; init; }
}
