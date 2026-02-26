namespace OseResearchVault.Core.Models;

public sealed class AskMyVaultRequest
{
    public string? AgentId { get; init; }
    public string? CompanyId { get; init; }
    public string Query { get; init; } = string.Empty;
    public IReadOnlyList<string> SelectedDocumentIds { get; init; } = [];
    public string? ModelProfileId { get; init; }
}
