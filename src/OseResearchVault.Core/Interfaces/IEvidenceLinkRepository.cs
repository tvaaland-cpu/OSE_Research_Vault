using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IEvidenceLinkRepository
{
    Task<EvidenceLink> CreateEvidenceLinkAsync(
        string artifactId,
        string? snippetId,
        string? documentId,
        string? locator,
        string? quote,
        double? relevanceScore,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvidenceLink>> ListEvidenceLinksByArtifactAsync(string artifactId, CancellationToken cancellationToken = default);

    Task DeleteEvidenceLinkAsync(string evidenceLinkId, CancellationToken cancellationToken = default);
}
