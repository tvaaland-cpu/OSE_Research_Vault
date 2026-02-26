using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteDataQualityService(IAppSettingsService appSettingsService) : IDataQualityService
{
    public async Task<DataQualityReport> GetReportAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var duplicatesRows = await connection.QueryAsync<DuplicateRow>(new CommandDefinition(
            @"SELECT d.id,
                     d.title,
                     d.company_id AS CompanyId,
                     c.name AS CompanyName,
                     COALESCE(d.imported_at, d.created_at) AS ImportedAt,
                     COALESCE(d.content_hash, '') AS ContentHash,
                     COALESCE(d.is_archived, 0) AS IsArchived
                FROM document d
                LEFT JOIN company c ON c.id = d.company_id
               WHERE COALESCE(d.content_hash, '') <> ''
                 AND COALESCE(d.is_archived, 0) = 0
                 AND d.content_hash IN (
                     SELECT content_hash
                       FROM document
                      WHERE COALESCE(content_hash, '') <> ''
                        AND COALESCE(is_archived, 0) = 0
                   GROUP BY content_hash
                     HAVING COUNT(*) > 1)
            ORDER BY d.content_hash, COALESCE(d.imported_at, d.created_at) DESC",
            cancellationToken: cancellationToken));

        var duplicates = duplicatesRows
            .GroupBy(static x => x.ContentHash, StringComparer.Ordinal)
            .Select(group => new DuplicateDocumentGroup
            {
                ContentHash = group.Key,
                Documents = group.Select(item => new DataQualityDocumentItem
                {
                    Id = item.Id,
                    Title = item.Title,
                    CompanyId = item.CompanyId,
                    CompanyName = item.CompanyName,
                    ImportedAt = item.ImportedAt,
                    IsArchived = item.IsArchived
                }).ToList()
            })
            .ToList();

        var unlinkedDocuments = (await connection.QueryAsync<DataQualityUnlinkedItem>(new CommandDefinition(
            @"SELECT d.id,
                     d.title,
                     COALESCE(d.imported_at, d.created_at) AS CreatedAt
                FROM document d
               WHERE d.company_id IS NULL
                 AND COALESCE(d.is_archived, 0) = 0
            ORDER BY COALESCE(d.imported_at, d.created_at) DESC",
            cancellationToken: cancellationToken))).ToList();

        var unlinkedNotes = (await connection.QueryAsync<DataQualityUnlinkedItem>(new CommandDefinition(
            @"SELECT n.id,
                     n.title,
                     n.created_at AS CreatedAt
                FROM note n
               WHERE n.company_id IS NULL
            ORDER BY n.created_at DESC",
            cancellationToken: cancellationToken))).ToList();

        var evidenceGaps = (await connection.QueryAsync<DataQualityArtifactGap>(new CommandDefinition(
            @"SELECT a.id AS ArtifactId,
                     COALESCE(a.title, '(untitled artifact)') AS Title,
                     a.created_at AS CreatedAt
                FROM artifact a
               WHERE NOT EXISTS (
                     SELECT 1
                       FROM evidence_link el
                      WHERE el.from_entity_type = 'artifact'
                        AND el.from_entity_id = a.id)
            ORDER BY a.created_at DESC",
            cancellationToken: cancellationToken))).ToList();

        var hasMetricSnippetColumn = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM pragma_table_info('metric') WHERE name = 'snippet_id'",
            cancellationToken: cancellationToken)) > 0;

        var metricIssues = hasMetricSnippetColumn
            ? (await connection.QueryAsync<DataQualityMetricIssue>(new CommandDefinition(
                @"SELECT m.id AS MetricId,
                         m.metric_key AS MetricKey,
                         m.recorded_at AS RecordedAt
                    FROM metric m
                   WHERE m.snippet_id IS NULL
                ORDER BY m.recorded_at DESC",
                cancellationToken: cancellationToken))).ToList()
            : [];

        var snippetIssues = (await connection.QueryAsync<DataQualitySnippetIssue>(new CommandDefinition(
            @"SELECT s.id AS SnippetId,
                     COALESCE(s.locator, '') AS Locator,
                     s.document_id AS DocumentId,
                     s.source_id AS SourceId
                FROM snippet s
               WHERE COALESCE(s.locator, '') = ''
                  OR (s.document_id IS NULL AND s.source_id IS NULL)
            ORDER BY s.created_at DESC",
            cancellationToken: cancellationToken))).ToList();

        var enrichmentSuggestions = await LoadEnrichmentSuggestionsAsync(connection, cancellationToken);

        return new DataQualityReport
        {
            Duplicates = duplicates,
            UnlinkedDocuments = unlinkedDocuments,
            UnlinkedNotes = unlinkedNotes,
            EvidenceGaps = evidenceGaps,
            MetricEvidenceIssues = metricIssues,
            SnippetIssues = snippetIssues,
            EnrichmentSuggestions = enrichmentSuggestions
        };
    }

    public async Task LinkDocumentToCompanyAsync(string documentId, string? companyId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE document
                 SET company_id = @CompanyId,
                     updated_at = @Now
               WHERE id = @Id",
            new { Id = documentId, CompanyId = string.IsNullOrWhiteSpace(companyId) ? null : companyId, Now = DateTime.UtcNow.ToString("O") },
            cancellationToken: cancellationToken));
    }

    public async Task LinkNoteToCompanyAsync(string noteId, string? companyId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE note
                 SET company_id = @CompanyId,
                     updated_at = @Now
               WHERE id = @Id",
            new { Id = noteId, CompanyId = string.IsNullOrWhiteSpace(companyId) ? null : companyId, Now = DateTime.UtcNow.ToString("O") },
            cancellationToken: cancellationToken));
    }

    public async Task ApplyEnrichmentSuggestionAsync(string itemType, string itemId, string companyId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(itemType, "document", StringComparison.OrdinalIgnoreCase))
        {
            await LinkDocumentToCompanyAsync(itemId, companyId, cancellationToken);
            return;
        }

        if (string.Equals(itemType, "note", StringComparison.OrdinalIgnoreCase))
        {
            await LinkNoteToCompanyAsync(itemId, companyId, cancellationToken);
        }
    }

    public async Task ArchiveDuplicateDocumentsAsync(string contentHash, string keepDocumentId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE document
                 SET is_archived = CASE WHEN id = @KeepId THEN 0 ELSE 1 END,
                     updated_at = @Now
               WHERE content_hash = @ContentHash",
            new { KeepId = keepDocumentId, ContentHash = contentHash, Now = DateTime.UtcNow.ToString("O") },
            cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<DataQualityEnrichmentSuggestion>> LoadEnrichmentSuggestionsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var companies = (await connection.QueryAsync<CompanyMatchRow>(new CommandDefinition(
            @"SELECT id, workspace_id AS WorkspaceId, name, COALESCE(ticker, '') AS Ticker, COALESCE(isin, '') AS Isin
                FROM company",
            cancellationToken: cancellationToken))).ToList();

        var documentRows = (await connection.QueryAsync<UnlinkedDocumentMatchRow>(new CommandDefinition(
            @"SELECT d.id,
                     d.workspace_id AS WorkspaceId,
                     d.title,
                     COALESCE(d.imported_at, d.created_at) AS CreatedAt,
                     COALESCE(GROUP_CONCAT(dt.content, ' '), '') AS Body
                FROM document d
                LEFT JOIN document_text dt ON dt.document_id = d.id
               WHERE d.company_id IS NULL
                 AND COALESCE(d.is_archived, 0) = 0
            GROUP BY d.id, d.workspace_id, d.title, COALESCE(d.imported_at, d.created_at)",
            cancellationToken: cancellationToken))).ToList();

        var noteRows = (await connection.QueryAsync<UnlinkedNoteMatchRow>(new CommandDefinition(
            @"SELECT n.id,
                     n.workspace_id AS WorkspaceId,
                     n.title,
                     n.created_at AS CreatedAt,
                     COALESCE(n.content, '') AS Body
                FROM note n
               WHERE n.company_id IS NULL",
            cancellationToken: cancellationToken))).ToList();

        var suggestions = new List<DataQualityEnrichmentSuggestion>();

        foreach (var row in documentRows)
        {
            var suggestion = FindBestSuggestion(
                companies.Where(c => string.Equals(c.WorkspaceId, row.WorkspaceId, StringComparison.Ordinal)).ToList(),
                row.Title,
                row.Body,
                row.CreatedAt,
                "document",
                row.Id);
            if (suggestion is not null)
            {
                suggestions.Add(suggestion);
            }
        }

        foreach (var row in noteRows)
        {
            var suggestion = FindBestSuggestion(
                companies.Where(c => string.Equals(c.WorkspaceId, row.WorkspaceId, StringComparison.Ordinal)).ToList(),
                row.Title,
                row.Body,
                row.CreatedAt,
                "note",
                row.Id);
            if (suggestion is not null)
            {
                suggestions.Add(suggestion);
            }
        }

        return suggestions
            .OrderByDescending(s => s.CreatedAt, StringComparer.Ordinal)
            .ToList();
    }

    private static DataQualityEnrichmentSuggestion? FindBestSuggestion(
        IReadOnlyList<CompanyMatchRow> companies,
        string title,
        string body,
        string createdAt,
        string itemType,
        string itemId)
    {
        var text = string.Join(' ', new[] { title, body }.Where(s => !string.IsNullOrWhiteSpace(s)));

        DataQualityEnrichmentSuggestion? best = null;
        var bestScore = 0;

        foreach (var company in companies)
        {
            EvaluateCandidate(company.Ticker, "Ticker", 3);
            EvaluateCandidate(company.Isin, "ISIN", 3);
            EvaluateCandidate(company.Name, "Company name", 2);
        }

        return best;

        void EvaluateCandidate(string candidate, string reason, int score)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            if (text.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            if (score < bestScore)
            {
                return;
            }

            var matchingCompany = companies.First(c =>
                string.Equals(candidate, c.Ticker, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, c.Isin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, c.Name, StringComparison.OrdinalIgnoreCase));

            best = new DataQualityEnrichmentSuggestion
            {
                ItemType = itemType,
                ItemId = itemId,
                ItemTitle = title,
                CompanyId = matchingCompany.Id,
                CompanyName = matchingCompany.Name,
                MatchedTerm = candidate,
                MatchReason = reason,
                CreatedAt = createdAt
            };
            bestScore = score;
        }
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        return new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString());
    }

    private sealed class DuplicateRow
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? CompanyId { get; init; }
        public string? CompanyName { get; init; }
        public string ImportedAt { get; init; } = string.Empty;
        public string ContentHash { get; init; } = string.Empty;
        public bool IsArchived { get; init; }
    }

    private sealed class CompanyMatchRow
    {
        public string Id { get; init; } = string.Empty;
        public string WorkspaceId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Ticker { get; init; } = string.Empty;
        public string Isin { get; init; } = string.Empty;
    }

    private sealed class UnlinkedDocumentMatchRow
    {
        public string Id { get; init; } = string.Empty;
        public string WorkspaceId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
    }

    private sealed class UnlinkedNoteMatchRow
    {
        public string Id { get; init; } = string.Empty;
        public string WorkspaceId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
    }
}
