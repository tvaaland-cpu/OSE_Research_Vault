namespace OseResearchVault.Core.Models;

public sealed class CompanyUpsertRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Ticker { get; init; }
    public string? Isin { get; init; }
    public string? Sector { get; init; }
    public string? Industry { get; init; }
    public string? Currency { get; init; }
    public string? Summary { get; init; }
}
