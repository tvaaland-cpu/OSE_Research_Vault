namespace OseResearchVault.Core.Models;

public sealed class AgentArtifactRecord
{
    public string Id { get; init; } = string.Empty;
    public string RunId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
}
