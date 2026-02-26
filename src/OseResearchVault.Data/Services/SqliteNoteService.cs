using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteNoteService(IAppSettingsService appSettingsService, IFtsSyncService ftsSyncService) : INoteService
{
    public async Task<IReadOnlyList<NoteRecord>> GetNotesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<NoteRecord>(new CommandDefinition(
            @"SELECT n.id, n.title, n.content, n.note_type AS NoteType, n.company_id AS CompanyId, c.name AS CompanyName, n.created_at AS CreatedAt
                FROM note n
                LEFT JOIN company c ON c.id = n.company_id
            ORDER BY n.created_at DESC", cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<string> CreateNoteAsync(NoteUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var noteId = Guid.NewGuid().ToString();

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO note (id, workspace_id, company_id, title, content, note_type, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @CompanyId, @Title, @Content, @NoteType, @Now, @Now)",
            new { Id = noteId, WorkspaceId = workspaceId, request.CompanyId, request.Title, request.Content, request.NoteType, Now = now }, transaction, cancellationToken: cancellationToken));

        await SyncNoteTagsAsync(connection, transaction, noteId, workspaceId, request.Tags, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await ftsSyncService.UpsertNoteAsync(noteId, request.Title, request.Content, cancellationToken);
        return noteId;
    }

    public async Task UpdateNoteAsync(string noteId, NoteUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var workspaceId = await connection.QuerySingleAsync<string>(new CommandDefinition(
            "SELECT workspace_id FROM note WHERE id = @Id",
            new { Id = noteId },
            transaction,
            cancellationToken: cancellationToken));

        var now = DateTime.UtcNow.ToString("O");
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE note
                 SET company_id = @CompanyId,
                     title = @Title,
                     content = @Content,
                     note_type = @NoteType,
                     updated_at = @Now
               WHERE id = @Id",
            new { Id = noteId, request.CompanyId, request.Title, request.Content, request.NoteType, Now = now }, transaction, cancellationToken: cancellationToken));

        await SyncNoteTagsAsync(connection, transaction, noteId, workspaceId, request.Tags, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await ftsSyncService.UpsertNoteAsync(noteId, request.Title, request.Content, cancellationToken);
    }

    public async Task DeleteNoteAsync(string noteId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM note WHERE id = @Id", new { Id = noteId }, cancellationToken: cancellationToken));
        await ftsSyncService.DeleteNoteAsync(noteId, cancellationToken);
    }

    public async Task<string> ImportAiOutputAsync(AiImportRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var noteId = Guid.NewGuid().ToString();
        var artifactId = Guid.NewGuid().ToString();
        var title = $"AI Summary ({request.Model.Trim()})";
        var body = BuildAiSummaryBody(request);
        var metadata = JsonSerializer.Serialize(new { model = request.Model.Trim(), prompt = request.Prompt });

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO note (id, workspace_id, company_id, title, content, note_type, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @CompanyId, @Title, @Content, 'ai_summary', @Now, @Now)",
            new { Id = noteId, WorkspaceId = workspaceId, request.CompanyId, Title = title, Content = body, Now = now },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO artifact (id, workspace_id, artifact_type, title, content, content_format, metadata_json, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, 'summary', @Title, @Content, 'markdown', @MetadataJson, @Now, @Now)",
            new { Id = artifactId, WorkspaceId = workspaceId, Title = title, Content = request.Response, MetadataJson = metadata, Now = now },
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);

        await ftsSyncService.UpsertNoteAsync(noteId, title, body, cancellationToken);
        await ftsSyncService.UpsertArtifactAsync(artifactId, request.Response, cancellationToken);

        return noteId;
    }

    private static async Task SyncNoteTagsAsync(SqliteConnection connection, SqliteTransaction transaction, string noteId, string workspaceId, IReadOnlyList<string> tags, string now, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM note_tag WHERE note_id = @NoteId", new { NoteId = noteId }, transaction, cancellationToken: cancellationToken));

        foreach (var tagName in tags.Select(t => t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var existingTagId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                "SELECT id FROM tag WHERE workspace_id = @WorkspaceId AND lower(name) = lower(@Name)",
                new { WorkspaceId = workspaceId, Name = tagName },
                transaction,
                cancellationToken: cancellationToken));

            var tagId = existingTagId;
            if (string.IsNullOrWhiteSpace(tagId))
            {
                tagId = Guid.NewGuid().ToString();
                await connection.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO tag (id, workspace_id, name, created_at)
                      VALUES (@Id, @WorkspaceId, @Name, @Now)",
                    new { Id = tagId, WorkspaceId = workspaceId, Name = tagName, Now = now },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO note_tag (note_id, tag_id, created_at)
                  VALUES (@NoteId, @TagId, @Now)",
                new { NoteId = noteId, TagId = tagId, Now = now },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static string BuildAiSummaryBody(AiImportRequest request)
    {
        var sources = string.IsNullOrWhiteSpace(request.Sources) ? "(none)" : request.Sources.Trim();
        return $"## Prompt\n{request.Prompt.Trim()}\n\n## Response\n{request.Response.Trim()}\n\n## Sources\n{sources}";
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        return new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString());
    }

    private static async Task<string> EnsureWorkspaceAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection(databasePath);
        await connection.OpenAsync(cancellationToken);

        var workspaceId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT id FROM workspace ORDER BY created_at LIMIT 1", cancellationToken: cancellationToken));

        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            return workspaceId;
        }

        workspaceId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO workspace (id, name, description, created_at, updated_at)
              VALUES (@Id, @Name, @Description, @Now, @Now)",
            new { Id = workspaceId, Name = "Default Workspace", Description = "Auto-created workspace", Now = now }, cancellationToken: cancellationToken));

        return workspaceId;
    }
}
