using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using System.Diagnostics;

namespace OseResearchVault.Data.Services;

public sealed class SqliteRetrievalService(IAppSettingsService appSettingsService, ILogger<SqliteRetrievalService> logger) : IRetrievalService
{
    private const int ChunkSize = 1200;
    private const int ChunkOverlap = 100;
    private const int MinChunkLength = 800;
    private const int MaxEffectiveTotalChars = 24000;
    private const int MaxEffectiveChunks = 48;

    public async Task<ContextPack> RetrieveAsync(
        string workspaceId,
        string query,
        string? companyId,
        int limitPerType,
        int maxTotalChars,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(workspaceId) || limitPerType <= 0 || maxTotalChars <= 0)
        {
            return new ContextPack
            {
                Query = query,
                Items = [],
                Log = new RetrievalLog { Query = query, WorkspaceId = workspaceId, CompanyId = companyId }
            };
        }

        var timer = Stopwatch.StartNew();
        var effectiveMaxTotalChars = Math.Min(maxTotalChars, MaxEffectiveTotalChars);
        var effectiveLimitPerType = Math.Max(1, Math.Min(limitPerType, MaxEffectiveChunks));

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var matchExpression = BuildMatchExpression(query);

        var noteItems = BuildChunkedItems(
            await QueryNotesAsync(connection, workspaceId, companyId, matchExpression, effectiveLimitPerType, cancellationToken),
            effectiveLimitPerType,
            "note",
            "NOTE",
            static row => row.Id,
            static row => row.Title,
            static row => row.Content,
            static (_, chunkIndex) => $"sel={chunkIndex}");

        var documentItems = BuildChunkedItems(
            await QueryDocumentsAsync(connection, workspaceId, companyId, matchExpression, effectiveLimitPerType, cancellationToken),
            effectiveLimitPerType,
            "doc",
            "DOC",
            static row => row.Id,
            static row => row.Title,
            static row => row.Content,
            static (_, chunkIndex) => $"sel={chunkIndex}");

        var artifactItems = BuildChunkedItems(
            await QueryArtifactsAsync(connection, workspaceId, matchExpression, effectiveLimitPerType, cancellationToken),
            effectiveLimitPerType,
            "artifact",
            "ART",
            static row => row.Id,
            static row => row.Title,
            static row => row.Content,
            static (_, chunkIndex) => $"sel={chunkIndex}");

        var snippetItems = (await QuerySnippetsAsync(connection, workspaceId, companyId, matchExpression, effectiveLimitPerType, cancellationToken))
            .OrderBy(r => r.Rank)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .Select(r => new RankedContextItem(
                r.Rank,
                new ContextPackItem
                {
                    ItemType = "snippet",
                    Title = string.IsNullOrWhiteSpace(r.Title) ? "(snippet)" : r.Title,
                    TextExcerpt = TrimExcerpt(r.Text, ChunkSize),
                    SourceRef = r.Id,
                    Locator = string.IsNullOrWhiteSpace(r.Locator) ? "snippet" : r.Locator,
                    CitationLabel = $"[SNIP:{r.Id}]"
                }))
            .Take(effectiveLimitPerType)
            .ToList();

        var merged = noteItems
            .Concat(documentItems)
            .Concat(snippetItems)
            .Concat(artifactItems)
            .OrderBy(i => i.Rank)
            .ThenBy(i => GetTypeOrder(i.Item.ItemType))
            .ThenBy(i => i.Item.SourceRef, StringComparer.Ordinal)
            .ThenBy(i => i.Item.CitationLabel, StringComparer.Ordinal)
            .ToList();

        var capped = new List<ContextPackItem>();
        var runningChars = 0;
        foreach (var entry in merged)
        {
            var excerptLength = entry.Item.TextExcerpt.Length;
            if (excerptLength == 0)
            {
                continue;
            }

            if (capped.Count >= MaxEffectiveChunks)
            {
                break;
            }

            if (runningChars + excerptLength > effectiveMaxTotalChars)
            {
                var remaining = effectiveMaxTotalChars - runningChars;
                if (remaining <= 0)
                {
                    break;
                }

                capped.Add(new ContextPackItem
                {
                    ItemType = entry.Item.ItemType,
                    Title = entry.Item.Title,
                    TextExcerpt = TrimExcerpt(entry.Item.TextExcerpt, remaining),
                    SourceRef = entry.Item.SourceRef,
                    Locator = entry.Item.Locator,
                    CitationLabel = entry.Item.CitationLabel
                });
                break;
            }

            capped.Add(entry.Item);
            runningChars += excerptLength;
        }

        timer.Stop();
        logger.LogDebug(
            "Retrieval pipeline completed in {ElapsedMs} ms with {ItemCount} item(s), {CharCount} chars",
            timer.ElapsedMilliseconds,
            capped.Count,
            runningChars);

        return new ContextPack
        {
            Query = query,
            Items = capped,
            Log = new RetrievalLog
            {
                Query = query,
                WorkspaceId = workspaceId,
                CompanyId = companyId,
                NoteCount = capped.Count(i => i.ItemType == "note"),
                DocumentCount = capped.Count(i => i.ItemType == "doc"),
                SnippetCount = capped.Count(i => i.ItemType == "snippet"),
                ArtifactCount = capped.Count(i => i.ItemType == "artifact")
            }
        };
    }

    private static IEnumerable<RankedContextItem> BuildChunkedItems<TRow>(
        IReadOnlyList<TRow> rows,
        int limitPerType,
        string itemType,
        string citationPrefix,
        Func<TRow, string> idSelector,
        Func<TRow, string> titleSelector,
        Func<TRow, string> textSelector,
        Func<TRow, int, string> locatorSelector)
        where TRow : IRankedRow
    {
        var rankedItems = new List<RankedContextItem>();

        foreach (var row in rows.OrderBy(r => r.Rank).ThenBy(idSelector, StringComparer.Ordinal))
        {
            var sourceId = idSelector(row);
            var sourceTitle = titleSelector(row);
            var chunks = ChunkText(textSelector(row));
            for (var i = 0; i < chunks.Count; i++)
            {
                rankedItems.Add(new RankedContextItem(
                    row.Rank,
                    new ContextPackItem
                    {
                        ItemType = itemType,
                        Title = string.IsNullOrWhiteSpace(sourceTitle) ? $"({itemType})" : sourceTitle,
                        TextExcerpt = chunks[i],
                        SourceRef = sourceId,
                        Locator = locatorSelector(row, i),
                        CitationLabel = $"[{citationPrefix}:{sourceId}|chunk:{i}]"
                    }));
            }
        }

        return rankedItems
            .OrderBy(i => i.Rank)
            .ThenBy(i => i.Item.SourceRef, StringComparer.Ordinal)
            .ThenBy(i => i.Item.CitationLabel, StringComparer.Ordinal)
            .Take(limitPerType)
            .ToList();
    }

    private async Task<IReadOnlyList<NoteRow>> QueryNotesAsync(SqliteConnection connection, string workspaceId, string? companyId, string matchExpression, int limitPerType, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT n.id AS Id,
       n.title AS Title,
       n.content AS Content,
       bm25(note_fts) AS Rank
  FROM note_fts
  JOIN note n ON n.id = note_fts.id
 WHERE note_fts MATCH @MatchExpression
   AND n.workspace_id = @WorkspaceId
   AND (@CompanyId IS NULL OR n.company_id = @CompanyId)
 ORDER BY Rank ASC, n.id ASC
 LIMIT @Limit";

        var rows = await connection.QueryAsync<NoteRow>(new CommandDefinition(sql, new { MatchExpression = matchExpression, WorkspaceId = workspaceId, CompanyId = companyId, Limit = limitPerType }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private async Task<IReadOnlyList<DocumentRow>> QueryDocumentsAsync(SqliteConnection connection, string workspaceId, string? companyId, string matchExpression, int limitPerType, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT d.id AS Id,
       d.title AS Title,
       COALESCE((SELECT group_concat(dt.content, char(10) || char(10))
                   FROM document_text dt
                  WHERE dt.document_id = d.id
               ORDER BY dt.chunk_index), '') AS Content,
       bm25(document_text_fts) AS Rank
  FROM document_text_fts
  JOIN document d ON d.id = document_text_fts.id
 WHERE document_text_fts MATCH @MatchExpression
   AND d.workspace_id = @WorkspaceId
   AND (@CompanyId IS NULL OR d.company_id = @CompanyId)
 ORDER BY Rank ASC, d.id ASC
 LIMIT @Limit";

        var rows = await connection.QueryAsync<DocumentRow>(new CommandDefinition(sql, new { MatchExpression = matchExpression, WorkspaceId = workspaceId, CompanyId = companyId, Limit = limitPerType }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private async Task<IReadOnlyList<SnippetRow>> QuerySnippetsAsync(SqliteConnection connection, string workspaceId, string? companyId, string matchExpression, int limitPerType, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT s.id AS Id,
       COALESCE(substr(s.quote_text, 1, 120), '(snippet)') AS Title,
       trim(COALESCE(s.quote_text, '') || ' ' || COALESCE(s.context, '')) AS Text,
       COALESCE(NULLIF(s.locator, ''), 'snippet') AS Locator,
       bm25(snippet_fts) AS Rank
  FROM snippet_fts
  JOIN snippet s ON s.id = snippet_fts.id
  LEFT JOIN note n ON n.id = s.note_id
  LEFT JOIN document d ON d.id = s.document_id
 WHERE snippet_fts MATCH @MatchExpression
   AND s.workspace_id = @WorkspaceId
   AND (@CompanyId IS NULL OR COALESCE(n.company_id, d.company_id) = @CompanyId)
 ORDER BY Rank ASC, s.id ASC
 LIMIT @Limit";

        var rows = await connection.QueryAsync<SnippetRow>(new CommandDefinition(sql, new { MatchExpression = matchExpression, WorkspaceId = workspaceId, CompanyId = companyId, Limit = limitPerType }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private async Task<IReadOnlyList<ArtifactRow>> QueryArtifactsAsync(SqliteConnection connection, string workspaceId, string matchExpression, int limitPerType, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT a.id AS Id,
       COALESCE(a.title, '(artifact)') AS Title,
       COALESCE(a.content, '') AS Content,
       bm25(artifact_fts) AS Rank
  FROM artifact_fts
  JOIN artifact a ON a.id = artifact_fts.id
 WHERE artifact_fts MATCH @MatchExpression
   AND a.workspace_id = @WorkspaceId
 ORDER BY Rank ASC, a.id ASC
 LIMIT @Limit";

        var rows = await connection.QueryAsync<ArtifactRow>(new CommandDefinition(sql, new { MatchExpression = matchExpression, WorkspaceId = workspaceId, Limit = limitPerType }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private static IReadOnlyList<string> ChunkText(string text)
    {
        var normalized = text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized))
        {
            return [];
        }

        if (normalized.Length <= MinChunkLength)
        {
            return [normalized];
        }

        var chunks = new List<string>();
        var step = ChunkSize - ChunkOverlap;
        for (var start = 0; start < normalized.Length; start += step)
        {
            var remaining = normalized.Length - start;
            var length = Math.Min(ChunkSize, remaining);
            if (remaining > ChunkSize && length < MinChunkLength)
            {
                length = Math.Min(remaining, MinChunkLength);
            }

            chunks.Add(normalized.Substring(start, length));
            if (start + length >= normalized.Length)
            {
                break;
            }
        }

        return chunks;
    }

    private static string TrimExcerpt(string text, int maxLength)
    {
        if (maxLength <= 0)
        {
            return string.Empty;
        }

        var trimmed = text.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed[..maxLength].TrimEnd();
    }

    private static string BuildMatchExpression(string query)
    {
        var escaped = query.Trim().Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static int GetTypeOrder(string itemType) => itemType switch
    {
        "note" => 0,
        "doc" => 1,
        "snippet" => 2,
        "artifact" => 3,
        _ => 4
    };

    private static SqliteConnection OpenConnection(string databasePath)
    {
        return new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString());
    }

    private interface IRankedRow
    {
        double Rank { get; }
    }

    private sealed record RankedContextItem(double Rank, ContextPackItem Item);

    private sealed class NoteRow : IRankedRow
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public double Rank { get; init; }
    }

    private sealed class DocumentRow : IRankedRow
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public double Rank { get; init; }
    }

    private sealed class SnippetRow : IRankedRow
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public string Locator { get; init; } = string.Empty;
        public double Rank { get; init; }
    }

    private sealed class ArtifactRow : IRankedRow
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public double Rank { get; init; }
    }
}
