using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteThesisService(IAppSettingsService appSettingsService) : IThesisService
{
    public async Task<IReadOnlyList<ThesisVersionRecord>> GetThesisVersionsAsync(string companyId, string? positionId = null, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ThesisVersionRecord>(new CommandDefinition(
            @"SELECT thesis_version_id AS ThesisVersionId,
                     workspace_id AS WorkspaceId,
                     company_id AS CompanyId,
                     position_id AS PositionId,
                     title AS Title,
                     body AS Body,
                     created_at AS CreatedAt,
                     created_by AS CreatedBy,
                     source_note_id AS SourceNoteId
                FROM thesis_version
               WHERE company_id = @CompanyId
                 AND (@PositionId IS NULL OR position_id = @PositionId)
            ORDER BY created_at DESC, thesis_version_id DESC",
            new { CompanyId = companyId, PositionId = positionId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<ThesisVersionRecord?> GetLatestThesisVersionAsync(string companyId, string? positionId = null, CancellationToken cancellationToken = default)
    {
        var versions = await GetThesisVersionsAsync(companyId, positionId, cancellationToken);
        return versions.FirstOrDefault();
    }

    public async Task<string> CreateThesisVersionAsync(CreateThesisVersionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyId))
        {
            throw new ArgumentException("Company is required.", nameof(request));
        }

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var thesisVersionId = Guid.NewGuid().ToString();

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO thesis_version (
                thesis_version_id, workspace_id, company_id, position_id, title, body, created_at, created_by, source_note_id)
              VALUES (
                @ThesisVersionId, @WorkspaceId, @CompanyId, @PositionId, @Title, @Body, @CreatedAt, @CreatedBy, @SourceNoteId)",
            new
            {
                ThesisVersionId = thesisVersionId,
                WorkspaceId = workspaceId,
                request.CompanyId,
                PositionId = string.IsNullOrWhiteSpace(request.PositionId) ? null : request.PositionId,
                Title = string.IsNullOrWhiteSpace(request.Title) ? "Thesis" : request.Title.Trim(),
                request.Body,
                CreatedAt = now,
                CreatedBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? "user" : request.CreatedBy.Trim(),
                request.SourceNoteId
            }, cancellationToken: cancellationToken));

        return thesisVersionId;
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        return new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, Pooling = false }.ToString());
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
