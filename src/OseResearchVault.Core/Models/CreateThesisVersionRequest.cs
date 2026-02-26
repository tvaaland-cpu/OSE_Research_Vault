namespace OseResearchVault.Core.Models;

public sealed class CreateThesisVersionRequest
{
    public string CompanyId { get; init; } = string.Empty;
    public string? PositionId { get; init; }
    public string Title { get; init; } = "Thesis";
    public string Body { get; init; } = string.Empty;
    public string CreatedBy { get; init; } = "user";
    public string? SourceNoteId { get; init; }
}
