using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IThesisService
{
    Task<IReadOnlyList<ThesisVersionRecord>> GetThesisVersionsAsync(string companyId, string? positionId = null, CancellationToken cancellationToken = default);
    Task<ThesisVersionRecord?> GetLatestThesisVersionAsync(string companyId, string? positionId = null, CancellationToken cancellationToken = default);
    Task<string> CreateThesisVersionAsync(CreateThesisVersionRequest request, CancellationToken cancellationToken = default);
}
