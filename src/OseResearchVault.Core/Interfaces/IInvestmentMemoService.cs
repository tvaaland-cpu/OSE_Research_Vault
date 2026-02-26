using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IInvestmentMemoService
{
    string BuildPrompt(string companyName);
    Task<InvestmentMemoResult> GenerateInvestmentMemoAsync(string companyId, string companyName, CancellationToken cancellationToken = default);
}
