using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface ICompanyService
{
    Task<IReadOnlyList<CompanyRecord>> GetCompaniesAsync(CancellationToken cancellationToken = default);
    Task<string> CreateCompanyAsync(CompanyUpsertRequest request, IEnumerable<string> tagIds, CancellationToken cancellationToken = default);
    Task UpdateCompanyAsync(string companyId, CompanyUpsertRequest request, IEnumerable<string> tagIds, CancellationToken cancellationToken = default);
    Task DeleteCompanyAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TagRecord>> GetTagsAsync(CancellationToken cancellationToken = default);
    Task<string> CreateTagAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentRecord>> GetCompanyDocumentsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteRecord>> GetCompanyNotesAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCompanyEventsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCompanyMetricsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCompanyAgentRunsAsync(string companyId, CancellationToken cancellationToken = default);
}
