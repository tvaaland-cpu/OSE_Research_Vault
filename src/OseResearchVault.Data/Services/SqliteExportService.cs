using System.Globalization;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteExportService(IAppSettingsService appSettingsService, IRedactionService redactionService) : IExportService
{
    public async Task ExportCompanyResearchPackAsync(string workspaceId, string companyId, string outputFolder, RedactionOptions? redactionOptions = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);

        redactionOptions ??= new RedactionOptions();
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        Directory.CreateDirectory(outputFolder);
        var artifactsDir = Path.Combine(outputFolder, "artifacts");
        var documentsDir = Path.Combine(outputFolder, "documents");
        Directory.CreateDirectory(artifactsDir);
        Directory.CreateDirectory(documentsDir);

        await using var connection = new SqliteConnection($"Data Source={settings.DatabaseFilePath}");
        await connection.OpenAsync(cancellationToken);

        var company = await connection.QuerySingleOrDefaultAsync<CompanyRow>(new CommandDefinition(
            @"SELECT id, name, ticker, isin FROM company WHERE id = @CompanyId", new { CompanyId = companyId }, cancellationToken: cancellationToken));
        if (company is null)
        {
            throw new InvalidOperationException("Company not found.");
        }

        var notesSql = redactionOptions.ExcludePrivateTaggedItems
            ? @"SELECT n.id, n.title, n.note_type AS NoteType, n.content, n.updated_at AS UpdatedAt
                  FROM note n
                 WHERE n.company_id = @CompanyId
                   AND NOT EXISTS (
                        SELECT 1
                          FROM note_tag nt
                          INNER JOIN tag t ON t.id = nt.tag_id
                         WHERE nt.note_id = n.id
                           AND LOWER(t.name) = 'private')
              ORDER BY n.note_type, n.updated_at DESC"
            : @"SELECT id, title, note_type AS NoteType, content, updated_at AS UpdatedAt
                  FROM note
                 WHERE company_id = @CompanyId
              ORDER BY note_type, updated_at DESC";
        var notes = await connection.QueryAsync<NoteRow>(new CommandDefinition(notesSql, new { CompanyId = companyId }, cancellationToken: cancellationToken));

        var metrics = await connection.QueryAsync<MetricRow>(new CommandDefinition(
            @"SELECT m.metric_key AS MetricName,
                     TRIM(COALESCE(m.period_start, '') || CASE WHEN m.period_start IS NOT NULL AND m.period_end IS NOT NULL THEN ' - ' ELSE '' END || COALESCE(m.period_end, '')) AS Period,
                     m.metric_value AS Value,
                     m.unit,
                     m.currency,
                     d.title AS EvidenceDocumentTitle,
                     COALESCE(s.context, '') AS EvidenceLocator,
                     m.snippet_id AS SnippetId
                FROM metric m
                LEFT JOIN snippet s ON s.id = m.snippet_id
                LEFT JOIN document d ON d.id = s.document_id
               WHERE m.company_id = @CompanyId
            ORDER BY m.recorded_at DESC", new { CompanyId = companyId }, cancellationToken: cancellationToken));

        var events = await connection.QueryAsync<EventRow>(new CommandDefinition(
            @"SELECT e.occurred_at AS EventDate,
                     e.event_type AS EventType,
                     COALESCE(json_extract(e.payload_json, '$.confidence'), '') AS Confidence,
                     COALESCE(e.title, '') AS Description,
                     d.title AS EvidenceDocumentTitle,
                     COALESCE(s.context, '') AS EvidenceLocator
                FROM event e
                LEFT JOIN evidence_link el ON el.from_entity_type = 'event' AND el.from_entity_id = e.id AND el.to_entity_type = 'snippet'
                LEFT JOIN snippet s ON s.id = el.to_entity_id
                LEFT JOIN document d ON d.id = s.document_id
               WHERE e.company_id = @CompanyId
            ORDER BY e.occurred_at", new { CompanyId = companyId }, cancellationToken: cancellationToken));

        var artifactsSql = redactionOptions.ExcludePrivateTaggedItems
            ? @"SELECT a.id, a.title, a.content
                  FROM artifact a
                 WHERE a.company_id = @CompanyId
                   AND NOT EXISTS (
                        SELECT 1
                          FROM artifact_tag at
                          INNER JOIN tag t ON t.id = at.tag_id
                         WHERE at.artifact_id = a.id
                           AND LOWER(t.name) = 'private')
              ORDER BY a.created_at"
            : @"SELECT id, title, content FROM artifact WHERE company_id = @CompanyId ORDER BY created_at";
        var artifacts = await connection.QueryAsync<ArtifactRow>(new CommandDefinition(artifactsSql, new { CompanyId = companyId }, cancellationToken: cancellationToken));

        var hasArchivedFlag = await HasColumnAsync(connection, "document", "is_archived", cancellationToken);
        var documentsSql = redactionOptions.ExcludePrivateTaggedItems
            ? hasArchivedFlag
                ? @"SELECT d.id, d.title, d.file_path AS FilePath, COALESCE(d.content_hash, '') AS ContentHash
                      FROM document d
                     WHERE d.company_id = @CompanyId
                       AND COALESCE(d.is_archived, 0) = 0
                       AND NOT EXISTS (
                            SELECT 1
                              FROM document_tag dt
                              INNER JOIN tag t ON t.id = dt.tag_id
                             WHERE dt.document_id = d.id
                               AND LOWER(t.name) = 'private')"
                : @"SELECT d.id, d.title, d.file_path AS FilePath, COALESCE(d.content_hash, '') AS ContentHash
                      FROM document d
                     WHERE d.company_id = @CompanyId
                       AND NOT EXISTS (
                            SELECT 1
                              FROM document_tag dt
                              INNER JOIN tag t ON t.id = dt.tag_id
                             WHERE dt.document_id = d.id
                               AND LOWER(t.name) = 'private')"
            : hasArchivedFlag
                ? @"SELECT id, title, file_path AS FilePath, COALESCE(content_hash, '') AS ContentHash
                      FROM document
                     WHERE company_id = @CompanyId
                       AND COALESCE(is_archived, 0) = 0"
                : @"SELECT id, title, file_path AS FilePath, COALESCE(content_hash, '') AS ContentHash
                      FROM document
                     WHERE company_id = @CompanyId";
        var documents = (await connection.QueryAsync<DocumentRow>(new CommandDefinition(documentsSql, new { CompanyId = companyId }, cancellationToken: cancellationToken))).ToList();

        var index = Redact(BuildIndex(company, DateTimeOffset.UtcNow, workspaceId), redactionOptions);
        var notesText = Redact(BuildNotes(notes), redactionOptions);
        var metricsText = BuildMetricsCsv(metrics, redactionOptions);
        var eventsText = BuildEventsCsv(events, redactionOptions);

        await File.WriteAllTextAsync(Path.Combine(outputFolder, "index.md"), index, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputFolder, "notes.md"), notesText, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputFolder, "metrics.csv"), metricsText, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputFolder, "events.csv"), eventsText, cancellationToken);

        foreach (var artifact in artifacts)
        {
            var artifactText = Redact(BuildArtifactMarkdown(artifact), redactionOptions);
            var artifactFileName = SanitizeFileName(artifact.Title);
            await File.WriteAllTextAsync(Path.Combine(artifactsDir, $"{artifactFileName}.md"), artifactText, cancellationToken);
        }

        foreach (var document in documents)
        {
            var targetName = $"{SanitizeFileName(document.Title)}_{document.Id[..Math.Min(8, document.Id.Length)]}{Path.GetExtension(document.FilePath)}";
            var targetPath = Path.Combine(documentsDir, targetName);
            if (File.Exists(document.FilePath))
            {
                File.Copy(document.FilePath, targetPath, overwrite: true);
            }
        }
    }

    public async Task<IReadOnlyList<ExportProfileRecord>> GetExportProfilesAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = new SqliteConnection($"Data Source={settings.DatabaseFilePath}");
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ExportProfileRow>(new CommandDefinition(
            @"SELECT profile_id AS ProfileId, workspace_id AS WorkspaceId, name, options_json AS OptionsJson, created_at AS CreatedAt
                FROM export_profile
               WHERE workspace_id = @WorkspaceId
            ORDER BY created_at DESC", new { WorkspaceId = workspaceId }, cancellationToken: cancellationToken));

        return rows.Select(r => new ExportProfileRecord
        {
            ProfileId = r.ProfileId,
            WorkspaceId = r.WorkspaceId,
            Name = r.Name,
            CreatedAt = r.CreatedAt,
            Options = JsonSerializer.Deserialize<RedactionOptions>(r.OptionsJson) ?? new RedactionOptions()
        }).ToList();
    }

    public async Task<string> SaveExportProfileAsync(string workspaceId, ExportProfileUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = new SqliteConnection($"Data Source={settings.DatabaseFilePath}");
        await connection.OpenAsync(cancellationToken);

        var profileId = string.IsNullOrWhiteSpace(request.ProfileId) ? Guid.NewGuid().ToString() : request.ProfileId;
        var now = DateTime.UtcNow.ToString("O");
        var optionsJson = JsonSerializer.Serialize(request.Options ?? new RedactionOptions());

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO export_profile (profile_id, workspace_id, name, options_json, created_at)
              VALUES (@ProfileId, @WorkspaceId, @Name, @OptionsJson, @CreatedAt)
              ON CONFLICT(profile_id) DO UPDATE SET
                  name = excluded.name,
                  options_json = excluded.options_json",
            new { ProfileId = profileId, WorkspaceId = workspaceId, request.Name, OptionsJson = optionsJson, CreatedAt = now }, cancellationToken: cancellationToken));

        return profileId;
    }

    public async Task DeleteExportProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = new SqliteConnection($"Data Source={settings.DatabaseFilePath}");
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM export_profile WHERE profile_id = @ProfileId", new { ProfileId = profileId }, cancellationToken: cancellationToken));
    }

    private string Redact(string text, RedactionOptions options) => redactionService.Redact(text, options).RedactedText;

    private string? RedactIfSensitive(string? text, RedactionOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var redacted = Redact(text, options);
        return redacted == text ? text : redacted;
    }

    private static string BuildIndex(CompanyRow company, DateTimeOffset generatedAt, string workspaceId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Company Research Pack");
        sb.AppendLine();
        sb.AppendLine($"- Company: {company.Name}");
        sb.AppendLine($"- Ticker: {company.Ticker}");
        sb.AppendLine($"- ISIN: {company.Isin}");
        sb.AppendLine($"- Workspace: {workspaceId}");
        sb.AppendLine($"- Generated At (UTC): {generatedAt:O}");
        sb.AppendLine();
        sb.AppendLine("## Files");
        sb.AppendLine("- [notes.md](notes.md)");
        sb.AppendLine("- [metrics.csv](metrics.csv)");
        sb.AppendLine("- [events.csv](events.csv)");
        sb.AppendLine("- [artifacts/](artifacts)");
        sb.AppendLine("- [documents/](documents)");
        return sb.ToString();
    }

    private static string BuildNotes(IEnumerable<NoteRow> notes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Notes");

        foreach (var group in notes.GroupBy(n => string.IsNullOrWhiteSpace(n.NoteType) ? "manual" : n.NoteType))
        {
            sb.AppendLine();
            sb.AppendLine($"## {group.Key}");
            foreach (var note in group)
            {
                sb.AppendLine();
                sb.AppendLine($"### {note.Title}");
                sb.AppendLine($"Updated: {note.UpdatedAt}");
                sb.AppendLine();
                sb.AppendLine(note.Content);
            }
        }

        return sb.ToString();
    }

    private string BuildMetricsCsv(IEnumerable<MetricRow> metrics, RedactionOptions redactionOptions)
    {
        var rows = new StringBuilder();
        rows.AppendLine("metric_name,period,value,unit,currency,evidence_document_title,evidence_locator,snippet_id");
        foreach (var metric in metrics)
        {
            rows.AppendLine(string.Join(',', Csv(metric.MetricName), Csv(metric.Period), Csv(metric.Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty), Csv(metric.Unit), Csv(metric.Currency), Csv(RedactIfSensitive(metric.EvidenceDocumentTitle, redactionOptions)), Csv(RedactIfSensitive(metric.EvidenceLocator, redactionOptions)), Csv(RedactIfSensitive(metric.SnippetId, redactionOptions))));
        }

        return rows.ToString();
    }

    private string BuildEventsCsv(IEnumerable<EventRow> events, RedactionOptions redactionOptions)
    {
        var rows = new StringBuilder();
        rows.AppendLine("event_date,event_type,confidence,description,evidence_document_title,evidence_locator");
        foreach (var item in events)
        {
            rows.AppendLine(string.Join(',', Csv(item.EventDate), Csv(item.EventType), Csv(item.Confidence), Csv(Redact(item.Description, redactionOptions)), Csv(RedactIfSensitive(item.EvidenceDocumentTitle, redactionOptions)), Csv(RedactIfSensitive(item.EvidenceLocator, redactionOptions))));
        }

        return rows.ToString();
    }

    private static string BuildArtifactMarkdown(ArtifactRow artifact)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {artifact.Title}");
        sb.AppendLine();
        sb.AppendLine(artifact.Content);
        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = input.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return string.IsNullOrWhiteSpace(new string(chars)) ? "artifact" : new string(chars);
    }

    private static async Task<bool> HasColumnAsync(SqliteConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        var columns = await connection.QueryAsync<TableInfoRow>(new CommandDefinition($"PRAGMA table_info({tableName})", cancellationToken: cancellationToken));
        return columns.Any(c => string.Equals(c.Name, columnName, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ExportProfileRow
    {
        public string ProfileId { get; init; } = string.Empty;
        public string WorkspaceId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string OptionsJson { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
    }

    private sealed class TableInfoRow { public string Name { get; init; } = string.Empty; }
    private sealed class CompanyRow { public string Id { get; init; } = string.Empty; public string Name { get; init; } = string.Empty; public string? Ticker { get; init; } public string? Isin { get; init; } }
    private sealed class NoteRow { public string Id { get; init; } = string.Empty; public string Title { get; init; } = string.Empty; public string? NoteType { get; init; } public string Content { get; init; } = string.Empty; public string UpdatedAt { get; init; } = string.Empty; }
    private sealed class MetricRow { public string MetricName { get; init; } = string.Empty; public string? Period { get; init; } public double? Value { get; init; } public string? Unit { get; init; } public string? Currency { get; init; } public string? EvidenceDocumentTitle { get; init; } public string? EvidenceLocator { get; init; } public string? SnippetId { get; init; } }
    private sealed class EventRow { public string EventDate { get; init; } = string.Empty; public string EventType { get; init; } = string.Empty; public string? Confidence { get; init; } public string Description { get; init; } = string.Empty; public string? EvidenceDocumentTitle { get; init; } public string? EvidenceLocator { get; init; } }
    private sealed class ArtifactRow { public string Id { get; init; } = string.Empty; public string Title { get; init; } = string.Empty; public string Content { get; init; } = string.Empty; }
    private sealed class DocumentRow { public string Id { get; init; } = string.Empty; public string Title { get; init; } = string.Empty; public string FilePath { get; init; } = string.Empty; public string ContentHash { get; init; } = string.Empty; }
}
