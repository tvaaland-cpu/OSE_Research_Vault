namespace OseResearchVault.Core.Models;

public sealed class AskMyVaultResult
{
    public string RunId { get; init; } = string.Empty;
    public bool CitationsDetected { get; init; }
}
