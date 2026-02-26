namespace OseResearchVault.Core.Models;

public sealed class ModelProfileUpsertRequest
{
    public required string Name { get; init; }
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public string ParametersJson { get; init; } = "{}";
    public bool IsDefault { get; init; }
}
