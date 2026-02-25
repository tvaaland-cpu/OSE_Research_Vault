namespace OseResearchVault.Core.Interfaces;

public interface ILLMProviderFactory
{
    ILLMProvider GetProvider(string providerName);
}
