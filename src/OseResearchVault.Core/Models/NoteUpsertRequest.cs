namespace OseResearchVault.Core.Models;

public sealed class NoteUpsertRequest
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string NoteType { get; init; } = "manual";
    public string? CompanyId { get; init; }
}
