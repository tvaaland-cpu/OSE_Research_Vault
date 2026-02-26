using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IPromptBuilder
{
    string BuildAskVaultPrompt(string query, string? companyName, ContextPack contextPack, AskVaultStyleOptions? styleOptions = null);
}
