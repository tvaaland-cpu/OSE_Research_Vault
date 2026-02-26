namespace OseResearchVault.Core.Models;

public sealed class InvestmentMemoResult
{
    public string RunId { get; init; } = string.Empty;
    public string NoteId { get; init; } = string.Empty;
    public bool CitationsDetected { get; init; }
    public string Prompt { get; init; } = string.Empty;
    public string MemoContent { get; init; } = string.Empty;
}
