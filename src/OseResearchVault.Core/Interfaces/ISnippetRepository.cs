using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface ISnippetRepository
{
    Task<Snippet> CreateSnippetAsync(
        string workspaceId,
        string documentId,
        string? companyId,
        string? sourceId,
        string locator,
        string text,
        string createdBy,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Snippet>> ListSnippetsByDocumentAsync(string documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Snippet>> ListSnippetsByCompanyAsync(string companyId, CancellationToken cancellationToken = default);
}
