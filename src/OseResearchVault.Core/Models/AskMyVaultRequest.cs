namespace OseResearchVault.Core.Models;

public sealed class AskMyVaultRequest
{
    public string? CompanyId { get; init; }
    public string Query { get; init; } = string.Empty;
    public IReadOnlyList<string> SelectedDocumentIds { get; init; } = [];
}
