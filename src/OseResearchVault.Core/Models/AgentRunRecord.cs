namespace OseResearchVault.Core.Models;

public sealed class AgentRunRecord
{
    public string Id { get; init; } = string.Empty;
    public string AgentId { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public string Query { get; init; } = string.Empty;
    public string SelectedDocumentIdsJson { get; init; } = "[]";
    public string ModelProvider { get; init; } = string.Empty;
    public string ModelName { get; init; } = string.Empty;
    public string ModelParametersJson { get; init; } = "{}";
    public string Error { get; init; } = string.Empty;
    public string StartedAt { get; init; } = string.Empty;
    public string? FinishedAt { get; init; }
}
