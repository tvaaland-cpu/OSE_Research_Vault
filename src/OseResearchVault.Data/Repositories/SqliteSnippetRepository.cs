using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Repositories;

public sealed class SqliteSnippetRepository(IAppSettingsService appSettingsService) : ISnippetRepository
{
    public async Task<Snippet> CreateSnippetAsync(string workspaceId, string documentId, string? companyId, string? sourceId, string locator, string text, string createdBy, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var isDocumentInWorkspace = await connection.QuerySingleAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM document WHERE id = @DocumentId AND workspace_id = @WorkspaceId",
            new { DocumentId = documentId, WorkspaceId = workspaceId }, cancellationToken: cancellationToken));

        if (isDocumentInWorkspace == 0)
        {
            throw new InvalidOperationException("Document does not exist in the specified workspace.");
        }

        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            var isSourceInWorkspace = await connection.QuerySingleAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM source WHERE id = @SourceId AND workspace_id = @WorkspaceId",
                new { SourceId = sourceId, WorkspaceId = workspaceId }, cancellationToken: cancellationToken));

            if (isSourceInWorkspace == 0)
            {
                throw new InvalidOperationException("Source does not exist in the specified workspace.");
            }
        }

        var now = DateTime.UtcNow.ToString("O");
        var id = Guid.NewGuid().ToString();

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO snippet (id, workspace_id, document_id, source_id, quote_text, context, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @DocumentId, @SourceId, @Text, @Locator, @Now, @Now)",
            new
            {
                Id = id,
                WorkspaceId = workspaceId,
                DocumentId = documentId,
                SourceId = sourceId,
                Text = text,
                Locator = locator,
                Now = now
            }, cancellationToken: cancellationToken));

        return new Snippet
        {
            Id = id,
            WorkspaceId = workspaceId,
            DocumentId = documentId,
            CompanyId = companyId,
            SourceId = sourceId,
            Locator = locator,
            Text = text,
            CreatedBy = createdBy,
            CreatedAt = now
        };
    }

    public async Task<IReadOnlyList<Snippet>> ListSnippetsByDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<SnippetRow>(new CommandDefinition(
            @"SELECT s.id,
                     s.workspace_id AS WorkspaceId,
                     s.document_id AS DocumentId,
                     d.company_id AS CompanyId,
                     s.source_id AS SourceId,
                     s.context AS Locator,
                     s.quote_text AS Text,
                     s.created_at AS CreatedAt
                FROM snippet s
                LEFT JOIN document d ON d.id = s.document_id
               WHERE s.document_id = @DocumentId
            ORDER BY s.created_at DESC",
            new { DocumentId = documentId }, cancellationToken: cancellationToken));

        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<Snippet>> ListSnippetsByCompanyAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<SnippetRow>(new CommandDefinition(
            @"SELECT s.id,
                     s.workspace_id AS WorkspaceId,
                     s.document_id AS DocumentId,
                     d.company_id AS CompanyId,
                     s.source_id AS SourceId,
                     s.context AS Locator,
                     s.quote_text AS Text,
                     s.created_at AS CreatedAt
                FROM snippet s
                INNER JOIN document d ON d.id = s.document_id
               WHERE d.company_id = @CompanyId
            ORDER BY s.created_at DESC",
            new { CompanyId = companyId }, cancellationToken: cancellationToken));

        return rows.Select(Map).ToList();
    }

    private static Snippet Map(SnippetRow row) => new()
    {
        Id = row.Id,
        WorkspaceId = row.WorkspaceId,
        DocumentId = row.DocumentId,
        CompanyId = row.CompanyId,
        SourceId = row.SourceId,
        Locator = row.Locator ?? string.Empty,
        Text = row.Text,
        CreatedAt = row.CreatedAt
    };

    private static SqliteConnection OpenConnection(string databasePath)
        => new(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString());

    private sealed class SnippetRow
    {
        public string Id { get; init; } = string.Empty;
        public string WorkspaceId { get; init; } = string.Empty;
        public string? DocumentId { get; init; }
        public string? CompanyId { get; init; }
        public string? SourceId { get; init; }
        public string? Locator { get; init; }
        public string Text { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
    }
}
