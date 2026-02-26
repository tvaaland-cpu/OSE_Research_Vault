namespace OseResearchVault.App.ViewModels;

public sealed class ModelProfileListItemViewModel
{
    public string ModelProfileId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string ParametersJson { get; init; } = "{}";
    public bool IsDefault { get; init; }
}
