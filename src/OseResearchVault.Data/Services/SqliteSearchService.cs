using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using System.Diagnostics;

namespace OseResearchVault.Data.Services;

public sealed class SqliteSearchService(IAppSettingsService appSettingsService, ILogger<SqliteSearchService> logger) : ISearchService
{
    public async Task<IReadOnlyList<SearchResultRecord>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.QueryText))
        {
            return [];
        }

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var pageNumber = Math.Max(1, query.PageNumber);
        var offset = (pageNumber - 1) * pageSize;
        var matchExpression = BuildMatchExpression(query.QueryText);
        var sql = @"
SELECT * FROM (
    SELECT 'note' AS ResultType,
           n.id AS EntityId,
           n.workspace_id AS WorkspaceId,
           n.company_id AS CompanyId,
           c.name AS CompanyName,
           n.title AS Title,
           snippet(note_fts, 2, '<mark>', '</mark>', ' â€¦ ', 16) AS MatchSnippet,
           n.created_at AS OccurredAt,
           bm25(note_fts) AS Rank
      FROM note_fts
      JOIN note n ON n.id = note_fts.id
 LEFT JOIN company c ON c.id = n.company_id
     WHERE note_fts MATCH @MatchExpression
       AND (@WorkspaceId IS NULL OR n.workspace_id = @WorkspaceId)
       AND (@CompanyId IS NULL OR n.company_id = @CompanyId)
       AND (@DateFrom IS NULL OR n.created_at >= @DateFrom)
       AND (@DateTo IS NULL OR n.created_at <= @DateTo)
       AND (@Type IS NULL OR @Type = 'note')

    UNION ALL

    SELECT 'document' AS ResultType,
           d.id AS EntityId,
           d.workspace_id AS WorkspaceId,
           d.company_id AS CompanyId,
           c.name AS CompanyName,
           d.title AS Title,
           snippet(document_text_fts, 2, '<mark>', '</mark>', ' â€¦ ', 16) AS MatchSnippet,
           COALESCE(d.imported_at, d.created_at) AS OccurredAt,
           bm25(document_text_fts) AS Rank
      FROM document_text_fts
      JOIN document d ON d.id = document_text_fts.id
 LEFT JOIN company c ON c.id = d.company_id
     WHERE document_text_fts MATCH @MatchExpression
       AND (@WorkspaceId IS NULL OR d.workspace_id = @WorkspaceId)
       AND (@CompanyId IS NULL OR d.company_id = @CompanyId)
       AND (@DateFrom IS NULL OR COALESCE(d.imported_at, d.created_at) >= @DateFrom)
       AND (@DateTo IS NULL OR COALESCE(d.imported_at, d.created_at) <= @DateTo)
       AND (@Type IS NULL OR @Type = 'document')

    UNION ALL

    SELECT 'snippet' AS ResultType,
           s.id AS EntityId,
           s.workspace_id AS WorkspaceId,
           COALESCE(n.company_id, d.company_id) AS CompanyId,
           c.name AS CompanyName,
           substr(s.quote_text, 1, 120) AS Title,
           snippet(snippet_fts, 1, '<mark>', '</mark>', ' â€¦ ', 16) AS MatchSnippet,
           s.created_at AS OccurredAt,
           bm25(snippet_fts) AS Rank
      FROM snippet_fts
      JOIN snippet s ON s.id = snippet_fts.id
 LEFT JOIN note n ON n.id = s.note_id
 LEFT JOIN document d ON d.id = s.document_id
 LEFT JOIN company c ON c.id = COALESCE(n.company_id, d.company_id)
     WHERE snippet_fts MATCH @MatchExpression
       AND (@WorkspaceId IS NULL OR s.workspace_id = @WorkspaceId)
       AND (@CompanyId IS NULL OR COALESCE(n.company_id, d.company_id) = @CompanyId)
       AND (@DateFrom IS NULL OR s.created_at >= @DateFrom)
       AND (@DateTo IS NULL OR s.created_at <= @DateTo)
       AND (@Type IS NULL OR @Type = 'snippet')

    UNION ALL

    SELECT 'artifact' AS ResultType,
           a.id AS EntityId,
           a.workspace_id AS WorkspaceId,
           NULL AS CompanyId,
           NULL AS CompanyName,
           COALESCE(a.title, '(artifact)') AS Title,
           snippet(artifact_fts, 1, '<mark>', '</mark>', ' â€¦ ', 16) AS MatchSnippet,
           a.created_at AS OccurredAt,
           bm25(artifact_fts) AS Rank
      FROM artifact_fts
      JOIN artifact a ON a.id = artifact_fts.id
     WHERE artifact_fts MATCH @MatchExpression
       AND (@WorkspaceId IS NULL OR a.workspace_id = @WorkspaceId)
       AND (@CompanyId IS NULL)
       AND (@DateFrom IS NULL OR a.created_at >= @DateFrom)
       AND (@DateTo IS NULL OR a.created_at <= @DateTo)
       AND (@Type IS NULL OR @Type = 'artifact')
) x
ORDER BY CASE x.ResultType WHEN 'note' THEN 0 WHEN 'document' THEN 1 WHEN 'snippet' THEN 2 ELSE 3 END,
         x.Rank ASC,
         x.OccurredAt DESC
LIMIT @PageSize
OFFSET @Offset";

        var timer = Stopwatch.StartNew();

        var rows = await connection.QueryAsync<SearchResultRecord>(new CommandDefinition(
            sql,
            new
            {
                MatchExpression = matchExpression,
                query.WorkspaceId,
                query.CompanyId,
                Type = NormalizeType(query.Type),
                DateFrom = query.DateFromIso,
                DateTo = query.DateToIso,
                PageSize = pageSize,
                Offset = offset
            },
            cancellationToken: cancellationToken));

        timer.Stop();
        logger.LogDebug(
            "Search query completed in {ElapsedMs} ms (page {PageNumber}, size {PageSize}, query='{Query}')",
            timer.ElapsedMilliseconds,
            pageNumber,
            pageSize,
            query.QueryText);

        return rows.ToList();
    }

    public async Task<IReadOnlyList<WorkspaceRecord>> GetWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<WorkspaceRecord>(new CommandDefinition(
            "SELECT id, name FROM workspace ORDER BY name", cancellationToken: cancellationToken));

        return rows.ToList();
    }

    private static string BuildMatchExpression(string queryText)
    {
        var trimmed = queryText.Trim();
        var escaped = trimmed.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string? NormalizeType(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "all" or "" => null,
        "notes" => "note",
        "docs" => "document",
        "documents" => "document",
        "snippets" => "snippet",
        "artifacts" => "artifact",
        var t => t
    };

    private static SqliteConnection OpenConnection(string databasePath)
    {
        return new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, Pooling = false }.ToString());
    }
}
