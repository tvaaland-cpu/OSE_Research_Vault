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

        return new DataQualityReport
        {
            Duplicates = duplicates,
            UnlinkedDocuments = unlinkedDocuments,
            UnlinkedNotes = unlinkedNotes,
            EvidenceGaps = evidenceGaps,
            MetricEvidenceIssues = metricIssues,
            SnippetIssues = snippetIssues
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
}
