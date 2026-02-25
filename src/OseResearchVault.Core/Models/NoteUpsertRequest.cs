namespace OseResearchVault.Core.Models;

public sealed class NoteUpsertRequest
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string? CompanyId { get; init; }
}
