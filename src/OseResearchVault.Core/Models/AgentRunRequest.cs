namespace OseResearchVault.Core.Models;

public sealed class AgentRunRequest
{
    public required string AgentId { get; init; }
    public string? CompanyId { get; init; }
    public string? Query { get; init; }
    public IReadOnlyList<string> SelectedDocumentIds { get; init; } = [];
    public string? ModelProfileId { get; init; }
}
