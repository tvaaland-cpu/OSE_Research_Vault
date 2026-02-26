using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IEvidenceService
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

    Task<EvidenceLink> CreateEvidenceLinkAsync(
        string artifactId,
        string? snippetId,
        string? documentId,
        string? locator,
        string? quote,
        double? relevanceScore,
        CancellationToken cancellationToken = default);

    Task<EvidenceLink> AddSnippetAndLinkToArtifactAsync(
        string workspaceId,
        string artifactId,
        string documentId,
        string? companyId,
        string? sourceId,
        string locator,
        string text,
        string createdBy,
        double? relevanceScore,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvidenceLink>> ListEvidenceLinksByArtifactAsync(string artifactId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Snippet>> ListSnippetsByDocumentAsync(string documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Snippet>> ListSnippetsByCompanyAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SnippetSearchResult>> SearchSnippetsAsync(
        string? companyId,
        string? documentId,
        string? query,
        CancellationToken cancellationToken = default);
    Task DeleteEvidenceLinkAsync(string evidenceLinkId, CancellationToken cancellationToken = default);
}
