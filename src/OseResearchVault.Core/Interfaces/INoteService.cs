using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface INoteService
{
    Task<IReadOnlyList<NoteRecord>> GetNotesAsync(CancellationToken cancellationToken = default);
    Task<string> CreateNoteAsync(NoteUpsertRequest request, CancellationToken cancellationToken = default);
    Task UpdateNoteAsync(string noteId, NoteUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteNoteAsync(string noteId, CancellationToken cancellationToken = default);
    Task<string> ImportAiOutputAsync(AiImportRequest request, CancellationToken cancellationToken = default);
}
