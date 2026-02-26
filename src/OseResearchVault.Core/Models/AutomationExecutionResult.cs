namespace OseResearchVault.Core.Models;

public sealed class AutomationExecutionResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? CreatedRunId { get; init; }
}
