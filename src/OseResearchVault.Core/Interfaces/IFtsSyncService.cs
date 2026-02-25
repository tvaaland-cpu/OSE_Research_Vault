namespace OseResearchVault.Core.Interfaces;

public interface IFtsSyncService
{
    Task UpsertNoteAsync(string id, string title, string content, CancellationToken cancellationToken = default);
    Task DeleteNoteAsync(string id, CancellationToken cancellationToken = default);

    Task UpsertSnippetAsync(string id, string quoteText, string? context, CancellationToken cancellationToken = default);
    Task DeleteSnippetAsync(string id, CancellationToken cancellationToken = default);

    Task UpsertArtifactAsync(string id, string? title, string? content, CancellationToken cancellationToken = default);
    Task DeleteArtifactAsync(string id, CancellationToken cancellationToken = default);

    Task UpsertDocumentTextAsync(string id, string content, CancellationToken cancellationToken = default);
    Task DeleteDocumentTextAsync(string id, CancellationToken cancellationToken = default);
}
