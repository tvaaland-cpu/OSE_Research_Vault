namespace OseResearchVault.Core.Models;

public sealed class DocumentImportResult
{
    public string FilePath { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
}
