namespace OseResearchVault.Core.Models;

public sealed class RunContextRecord
{
    public string RunContextId { get; init; } = string.Empty;
    public string WorkspaceId { get; init; } = string.Empty;
    public string RunId { get; init; } = string.Empty;
    public string ContextJson { get; init; } = string.Empty;
    public string PromptText { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
}
