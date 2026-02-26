namespace OseResearchVault.Core.Models;

public sealed class JournalEntryRecord
{
    public string JournalEntryId { get; init; } = string.Empty;
    public string WorkspaceId { get; init; } = string.Empty;
    public string CompanyId { get; init; } = string.Empty;
    public string? PositionId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string EntryDate { get; init; } = string.Empty;
    public string Rationale { get; init; } = string.Empty;
    public string? ExpectedOutcome { get; init; }
    public string? ReviewDate { get; init; }
    public string? ReviewNotes { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public IReadOnlyList<string> TradeIds { get; init; } = [];
    public IReadOnlyList<string> SnippetIds { get; init; } = [];
}
