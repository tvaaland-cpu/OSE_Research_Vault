namespace OseResearchVault.App.ViewModels;

public sealed class AgentRunListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string ParentRunId { get; init; } = string.Empty;
    public string AgentId { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public string CompanyId { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string Query { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StartedAt { get; init; } = string.Empty;
    public string SelectedDocumentIdsJson { get; init; } = "[]";
    public string ModelProfileId { get; init; } = string.Empty;
}
