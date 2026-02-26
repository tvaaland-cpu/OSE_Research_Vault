using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IRetrievalService
{
    Task<ContextPack> RetrieveAsync(
        string workspaceId,
        string query,
        string? companyId,
        int limitPerType,
        int maxTotalChars,
        CancellationToken cancellationToken = default);
}
