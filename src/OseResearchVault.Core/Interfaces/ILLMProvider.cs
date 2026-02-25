using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface ILLMProvider
{
    string ProviderName { get; }
    Task<string> GenerateAsync(string prompt, string contextDocsText, LlmGenerationSettings settings, CancellationToken cancellationToken = default);
}
