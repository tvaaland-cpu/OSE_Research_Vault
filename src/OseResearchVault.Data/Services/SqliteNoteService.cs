using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteNoteService(IAppSettingsService appSettingsService) : INoteService
{
    public async Task<IReadOnlyList<NoteRecord>> GetNotesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<NoteRecord>(new CommandDefinition(
            @"SELECT n.id, n.title, n.content, n.company_id AS CompanyId, c.name AS CompanyName, n.created_at AS CreatedAt
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

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO note (id, workspace_id, company_id, title, content, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @CompanyId, @Title, @Content, @Now, @Now)",
            new { Id = noteId, WorkspaceId = workspaceId, request.CompanyId, request.Title, request.Content, Now = now }, cancellationToken: cancellationToken));

        return noteId;
    }

    public async Task UpdateNoteAsync(string noteId, NoteUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE note
                 SET company_id = @CompanyId,
                     title = @Title,
                     content = @Content,
                     updated_at = @Now
               WHERE id = @Id",
            new { Id = noteId, request.CompanyId, request.Title, request.Content, Now = DateTime.UtcNow.ToString("O") }, cancellationToken: cancellationToken));
    }

    public async Task DeleteNoteAsync(string noteId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM note WHERE id = @Id", new { Id = noteId }, cancellationToken: cancellationToken));
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
