namespace OseResearchVault.Core.Models;

public sealed class AiImportRequest
{
    public string Model { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string Response { get; init; } = string.Empty;
    public string? Sources { get; init; }
    public string? CompanyId { get; init; }
}
