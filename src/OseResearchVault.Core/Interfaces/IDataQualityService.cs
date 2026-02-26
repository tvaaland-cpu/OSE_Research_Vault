using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IDataQualityService
{
    Task<DataQualityReport> GetReportAsync(CancellationToken cancellationToken = default);
    Task LinkDocumentToCompanyAsync(string documentId, string? companyId, CancellationToken cancellationToken = default);
    Task LinkNoteToCompanyAsync(string noteId, string? companyId, CancellationToken cancellationToken = default);
    Task ApplyEnrichmentSuggestionAsync(string itemType, string itemId, string companyId, CancellationToken cancellationToken = default);
    Task ArchiveDuplicateDocumentsAsync(string contentHash, string keepDocumentId, CancellationToken cancellationToken = default);
}
