namespace OseResearchVault.Core.Models;

public sealed class JournalEntryUpsertRequest
{
    public string? PositionId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string EntryDate { get; init; } = string.Empty;
    public string Rationale { get; init; } = string.Empty;
    public string? ExpectedOutcome { get; init; }
    public string? ReviewDate { get; init; }
    public string? ReviewNotes { get; init; }
}
