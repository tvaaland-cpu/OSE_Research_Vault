namespace OseResearchVault.Core.Models;

public sealed class LlmGenerationSettings
{
    public string Provider { get; init; } = "local";
    public string Model { get; init; } = "local-echo";
    public double Temperature { get; init; } = 0.2;
    public int MaxTokens { get; init; } = 800;
    public int TopDocumentChunks { get; init; } = 5;
    public string ApiKeySecretName { get; init; } = string.Empty;
}
