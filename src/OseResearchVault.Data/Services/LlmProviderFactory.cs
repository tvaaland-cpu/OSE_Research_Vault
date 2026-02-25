using OseResearchVault.Core.Interfaces;

namespace OseResearchVault.Data.Services;

public sealed class LlmProviderFactory(IEnumerable<ILLMProvider> providers) : ILLMProviderFactory
{
    private readonly Dictionary<string, ILLMProvider> _providers = providers.ToDictionary(x => x.ProviderName, StringComparer.OrdinalIgnoreCase);

    public ILLMProvider GetProvider(string providerName)
    {
        if (_providers.TryGetValue(providerName, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"LLM provider '{providerName}' is not registered in this build.");
    }
}
