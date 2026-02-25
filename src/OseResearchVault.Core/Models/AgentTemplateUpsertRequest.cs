namespace OseResearchVault.Core.Models;

public sealed class AgentTemplateUpsertRequest
{
    public required string Name { get; init; }
    public string? Goal { get; init; }
    public string? Instructions { get; init; }
    public string? AllowedToolsJson { get; init; }
    public string? OutputSchema { get; init; }
    public string? EvidencePolicy { get; init; }
}
