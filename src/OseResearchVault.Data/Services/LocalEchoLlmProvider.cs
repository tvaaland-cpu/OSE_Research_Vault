using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class LocalEchoLlmProvider : ILLMProvider
{
    public string ProviderName => "local";

    public Task<string> GenerateAsync(string prompt, string contextDocsText, LlmGenerationSettings settings, CancellationToken cancellationToken = default)
    {
        var response = $"[LocalEcho:{settings.Model}]\nPrompt:\n{prompt}\n\nContext:\n{contextDocsText}";
        return Task.FromResult(response);
    }
}
