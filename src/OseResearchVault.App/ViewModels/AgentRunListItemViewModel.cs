namespace OseResearchVault.App.ViewModels;

public sealed class AgentRunListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string Query { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StartedAt { get; init; } = string.Empty;
    public string SelectedDocumentIdsJson { get; init; } = "[]";
}
