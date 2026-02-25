namespace OseResearchVault.Core.Models;

public sealed class NoteRecord
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}
