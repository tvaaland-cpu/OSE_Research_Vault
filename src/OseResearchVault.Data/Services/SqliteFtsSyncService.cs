using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;

namespace OseResearchVault.Data.Services;

public sealed class SqliteFtsSyncService(IAppSettingsService appSettingsService) : IFtsSyncService
{
    public Task UpsertNoteAsync(string id, string title, string content, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            "note_fts",
            new { Id = id, Title = title, Content = content },
            "INSERT INTO note_fts(id, title, body) VALUES (@Id, @Title, @Content)",
            cancellationToken);

    public Task DeleteNoteAsync(string id, CancellationToken cancellationToken = default) =>
        DeleteAsync("note_fts", id, cancellationToken);

    public Task UpsertSnippetAsync(string id, string text, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            "snippet_fts",
            new { Id = id, Text = text },
            "INSERT INTO snippet_fts(id, text) VALUES (@Id, @Text)",
            cancellationToken);

    public Task DeleteSnippetAsync(string id, CancellationToken cancellationToken = default) =>
        DeleteAsync("snippet_fts", id, cancellationToken);

    public Task UpsertArtifactAsync(string id, string? content, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            "artifact_fts",
            new { Id = id, Content = content ?? string.Empty },
            "INSERT INTO artifact_fts(id, content) VALUES (@Id, @Content)",
            cancellationToken);

    public Task DeleteArtifactAsync(string id, CancellationToken cancellationToken = default) =>
        DeleteAsync("artifact_fts", id, cancellationToken);

    public Task UpsertDocumentTextAsync(string id, string title, string content, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            "document_text_fts",
            new { Id = id, Title = title, Content = content },
            "INSERT INTO document_text_fts(id, title, content) VALUES (@Id, @Title, @Content)",
            cancellationToken);

    public Task DeleteDocumentTextAsync(string id, CancellationToken cancellationToken = default) =>
        DeleteAsync("document_text_fts", id, cancellationToken);

    private async Task UpsertAsync(string table, object insertParameters, string insertSql, CancellationToken cancellationToken)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            await connection.ExecuteAsync($"DELETE FROM {table} WHERE id = @Id", insertParameters);
            await connection.ExecuteAsync(insertSql, insertParameters);
        }
    }

    private async Task DeleteAsync(string table, string id, CancellationToken cancellationToken)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            await connection.ExecuteAsync($"DELETE FROM {table} WHERE id = @Id", new { Id = id });
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = settings.DatabaseFilePath,
            ForeignKeys = true
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
