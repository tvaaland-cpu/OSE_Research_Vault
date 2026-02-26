namespace OseResearchVault.Core.Models;

public sealed class ModelProfileRecord
{
    public string ModelProfileId { get; init; } = string.Empty;
    public string? WorkspaceId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string ParametersJson { get; init; } = "{}";
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
}
