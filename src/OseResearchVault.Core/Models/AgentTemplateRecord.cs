namespace OseResearchVault.Core.Models;

public sealed class AgentTemplateRecord
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Goal { get; init; } = string.Empty;
    public string Instructions { get; init; } = string.Empty;
    public string AllowedToolsJson { get; init; } = "[]";
    public string OutputSchema { get; init; } = string.Empty;
    public string EvidencePolicy { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
}
