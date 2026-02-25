using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IDocumentImportService
{
    Task<IReadOnlyList<DocumentImportResult>> ImportFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentRecord>> GetDocumentsAsync(CancellationToken cancellationToken = default);
    Task<DocumentRecord?> GetDocumentDetailsAsync(string documentId, CancellationToken cancellationToken = default);
    Task UpdateDocumentCompanyAsync(string documentId, string? companyId, CancellationToken cancellationToken = default);
}
