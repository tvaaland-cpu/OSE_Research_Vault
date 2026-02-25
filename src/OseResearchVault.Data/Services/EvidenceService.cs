using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class EvidenceService(ISnippetRepository snippetRepository, IEvidenceLinkRepository evidenceLinkRepository) : IEvidenceService
{
    public async Task<Snippet> CreateSnippetAsync(string workspaceId, string documentId, string? companyId, string? sourceId, string locator, string text, string createdBy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new InvalidOperationException("Workspace id is required.");
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new InvalidOperationException("Document id is required.");
        }

        if (string.IsNullOrWhiteSpace(locator))
        {
            throw new InvalidOperationException("Snippet locator is required.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Snippet text cannot be empty.");
        }

        return await snippetRepository.CreateSnippetAsync(
            workspaceId,
            documentId,
            string.IsNullOrWhiteSpace(companyId) ? null : companyId.Trim(),
            string.IsNullOrWhiteSpace(sourceId) ? null : sourceId.Trim(),
            locator.Trim(),
            text.Trim(),
            string.IsNullOrWhiteSpace(createdBy) ? string.Empty : createdBy.Trim(),
            cancellationToken);
    }

    public async Task<EvidenceLink> CreateEvidenceLinkAsync(string artifactId, string? snippetId, string? documentId, string? locator, string? quote, double? relevanceScore, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            throw new InvalidOperationException("Artifact id is required.");
        }

        var hasSnippet = !string.IsNullOrWhiteSpace(snippetId);
        var hasDocumentLocator = !string.IsNullOrWhiteSpace(documentId) && !string.IsNullOrWhiteSpace(locator);

        if (hasSnippet == hasDocumentLocator)
        {
            throw new InvalidOperationException("Evidence link must target either snippet_id or document_id + locator.");
        }

        return await evidenceLinkRepository.CreateEvidenceLinkAsync(
            artifactId.Trim(),
            hasSnippet ? snippetId!.Trim() : null,
            hasDocumentLocator ? documentId!.Trim() : null,
            hasDocumentLocator ? locator!.Trim() : null,
            string.IsNullOrWhiteSpace(quote) ? null : quote.Trim(),
            relevanceScore,
            cancellationToken);
    }

    public async Task<EvidenceLink> AddSnippetAndLinkToArtifactAsync(string workspaceId, string artifactId, string documentId, string? companyId, string? sourceId, string locator, string text, string createdBy, double? relevanceScore, CancellationToken cancellationToken = default)
    {
        var snippet = await CreateSnippetAsync(workspaceId, documentId, companyId, sourceId, locator, text, createdBy, cancellationToken);
        return await CreateEvidenceLinkAsync(artifactId, snippet.Id, null, null, snippet.Text, relevanceScore, cancellationToken);
    }

    public Task<IReadOnlyList<EvidenceLink>> ListEvidenceLinksByArtifactAsync(string artifactId, CancellationToken cancellationToken = default)
        => evidenceLinkRepository.ListEvidenceLinksByArtifactAsync(artifactId, cancellationToken);

    public Task<IReadOnlyList<Snippet>> ListSnippetsByDocumentAsync(string documentId, CancellationToken cancellationToken = default)
        => snippetRepository.ListSnippetsByDocumentAsync(documentId, cancellationToken);

    public Task<IReadOnlyList<Snippet>> ListSnippetsByCompanyAsync(string companyId, CancellationToken cancellationToken = default)
        => snippetRepository.ListSnippetsByCompanyAsync(companyId, cancellationToken);

    public Task DeleteEvidenceLinkAsync(string evidenceLinkId, CancellationToken cancellationToken = default)
        => evidenceLinkRepository.DeleteEvidenceLinkAsync(evidenceLinkId, cancellationToken);
}
