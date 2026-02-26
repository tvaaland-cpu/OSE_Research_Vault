namespace OseResearchVault.Core.Models;

public sealed class WeeklyReviewResult
{
    public string NoteId { get; init; } = string.Empty;
    public string NoteTitle { get; init; } = string.Empty;
    public int ImportedDocumentCount { get; init; }
    public int RecentNotesCount { get; init; }
    public int AgentRunCount { get; init; }
    public int UpcomingCatalystCount { get; init; }
    public int RecentTradeCount { get; init; }
}
