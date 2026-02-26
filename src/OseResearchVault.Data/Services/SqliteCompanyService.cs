using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteCompanyService(IAppSettingsService appSettingsService) : ICompanyService
{
    public async Task<IReadOnlyList<CompanyRecord>> GetCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<CompanyRow>(new CommandDefinition(
            @"SELECT c.id, c.name, c.ticker, c.isin, c.sector, c.industry, c.currency, c.summary,
                     COALESCE(group_concat(t.name, ', '), '') AS TagList
                FROM company c
                LEFT JOIN company_tag ct ON ct.company_id = c.id
                LEFT JOIN tag t ON t.id = ct.tag_id
            GROUP BY c.id, c.name, c.ticker, c.isin, c.sector, c.industry, c.currency, c.summary
            ORDER BY c.name", cancellationToken: cancellationToken));

        return rows.Select(static r => new CompanyRecord
        {
            Id = r.Id,
            Name = r.Name,
            Ticker = r.Ticker,
            Isin = r.Isin,
            Sector = r.Sector,
            Industry = r.Industry,
            Currency = r.Currency,
            Summary = r.Summary,
            TagNames = string.IsNullOrWhiteSpace(r.TagList)
                ? []
                : r.TagList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        }).ToList();
    }

    public async Task<string> CreateCompanyAsync(CompanyUpsertRequest request, IEnumerable<string> tagIds, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var companyId = Guid.NewGuid().ToString();

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO company (id, workspace_id, name, ticker, isin, sector, industry, currency, summary, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @Name, @Ticker, @Isin, @Sector, @Industry, @Currency, @Summary, @Now, @Now)",
            new
            {
                Id = companyId,
                WorkspaceId = workspaceId,
                request.Name,
                request.Ticker,
                request.Isin,
                request.Sector,
                request.Industry,
                request.Currency,
                request.Summary,
                Now = now
            }, cancellationToken: cancellationToken));

        await ReplaceCompanyTagsAsync(connection, companyId, tagIds, now, cancellationToken);
        return companyId;
    }

    public async Task UpdateCompanyAsync(string companyId, CompanyUpsertRequest request, IEnumerable<string> tagIds, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var now = DateTime.UtcNow.ToString("O");

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE company
                 SET name = @Name,
                     ticker = @Ticker,
                     isin = @Isin,
                     sector = @Sector,
                     industry = @Industry,
                     currency = @Currency,
                     summary = @Summary,
                     updated_at = @Now
               WHERE id = @Id",
            new
            {
                Id = companyId,
                request.Name,
                request.Ticker,
                request.Isin,
                request.Sector,
                request.Industry,
                request.Currency,
                request.Summary,
                Now = now
            }, cancellationToken: cancellationToken));

        await ReplaceCompanyTagsAsync(connection, companyId, tagIds, now, cancellationToken);
    }

    public async Task DeleteCompanyAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM company WHERE id = @Id", new { Id = companyId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TagRecord>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<TagRecord>(new CommandDefinition("SELECT id, name FROM tag ORDER BY name", cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<string> CreateTagAsync(string name, CancellationToken cancellationToken = default)
    {
        var cleaned = name.Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new InvalidOperationException("Tag name cannot be empty.");
        }

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var existingId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT id FROM tag WHERE workspace_id = @WorkspaceId AND lower(name) = lower(@Name)",
            new { WorkspaceId = workspaceId, Name = cleaned }, cancellationToken: cancellationToken));

        if (!string.IsNullOrWhiteSpace(existingId))
        {
            return existingId;
        }

        var now = DateTime.UtcNow.ToString("O");
        var tagId = Guid.NewGuid().ToString();
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO tag (id, workspace_id, name, created_at) VALUES (@Id, @WorkspaceId, @Name, @Now)",
            new { Id = tagId, WorkspaceId = workspaceId, Name = cleaned, Now = now }, cancellationToken: cancellationToken));

        return tagId;
    }

    public Task<IReadOnlyList<DocumentRecord>> GetCompanyDocumentsAsync(string companyId, CancellationToken cancellationToken = default)
        => QueryDocumentsByCompanyAsync(companyId, cancellationToken);

    public async Task<IReadOnlyList<NoteRecord>> GetCompanyNotesAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<NoteRecord>(new CommandDefinition(
            @"SELECT n.id, n.title, n.content, n.company_id AS CompanyId, c.name AS CompanyName, n.created_at AS CreatedAt
                FROM note n
                LEFT JOIN company c ON c.id = n.company_id
               WHERE n.company_id = @CompanyId
            ORDER BY n.created_at DESC", new { CompanyId = companyId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<string>> GetCompanyEventsAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT title || ' (' || event_type || ')' FROM event WHERE company_id = @CompanyId ORDER BY occurred_at DESC",
            new { CompanyId = companyId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CompanyMetricRecord>> GetCompanyMetricsAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<MetricRow>(new CommandDefinition(
            @"SELECT m.id,
                     m.metric_key AS MetricName,
                     TRIM(COALESCE(m.period_start, '') || CASE WHEN m.period_start IS NOT NULL AND m.period_end IS NOT NULL THEN ' - ' ELSE '' END || COALESCE(m.period_end, '')) AS Period,
                     m.metric_value AS Value,
                     m.unit,
                     m.currency,
                     m.snippet_id AS SnippetId,
                     s.document_id AS DocumentId,
                     d.title AS DocumentTitle,
                     COALESCE(s.context, '') AS Locator,
                     src.name AS SourceTitle,
                     s.quote_text AS SnippetText,
                     m.created_at AS CreatedAt
                FROM metric m
                LEFT JOIN snippet s ON s.id = m.snippet_id
                LEFT JOIN document d ON d.id = s.document_id
                LEFT JOIN source src ON src.id = s.source_id
               WHERE m.company_id = @CompanyId
            ORDER BY m.recorded_at DESC",
            new { CompanyId = companyId }, cancellationToken: cancellationToken));

        return rows.Select(static row => new CompanyMetricRecord
        {
            Id = row.Id,
            MetricName = row.MetricName,
            Period = row.Period,
            Value = row.Value,
            Unit = row.Unit,
            Currency = row.Currency,
            SnippetId = row.SnippetId,
            DocumentId = row.DocumentId,
            DocumentTitle = row.DocumentTitle,
            Locator = row.Locator,
            SourceTitle = row.SourceTitle,
            SnippetText = row.SnippetText,
            CreatedAt = row.CreatedAt
        }).ToList();
    }

    public async Task<IReadOnlyList<string>> GetCompanyMetricNamesAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<string>(new CommandDefinition(
            @"SELECT DISTINCT metric_key
                FROM metric
               WHERE company_id = @CompanyId
                 AND metric_key IS NOT NULL
                 AND TRIM(metric_key) <> ''
            ORDER BY metric_key",
            new { CompanyId = companyId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task UpdateCompanyMetricAsync(string metricId, CompanyMetricUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var now = DateTime.UtcNow.ToString("O");
        var periodStart = request.Period;
        var periodEnd = (string?)null;

        if (!string.IsNullOrWhiteSpace(request.Period) && request.Period.Contains('-', StringComparison.Ordinal))
        {
            var parts = request.Period.Split('-', 2, StringSplitOptions.TrimEntries);
            periodStart = parts[0];
            periodEnd = parts.Length > 1 ? parts[1] : null;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE metric
                 SET metric_key = @MetricName,
                     period_start = @PeriodStart,
                     period_end = @PeriodEnd,
                     metric_value = @Value,
                     unit = @Unit,
                     currency = @Currency,
                     recorded_at = @Now
               WHERE id = @Id",
            new
            {
                Id = metricId,
                request.MetricName,
                PeriodStart = string.IsNullOrWhiteSpace(periodStart) ? null : periodStart.Trim(),
                PeriodEnd = string.IsNullOrWhiteSpace(periodEnd) ? null : periodEnd.Trim(),
                request.Value,
                Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
                Currency = string.IsNullOrWhiteSpace(request.Currency) ? null : request.Currency.Trim(),
                Now = now
            }, cancellationToken: cancellationToken));
    }

    public async Task DeleteCompanyMetricAsync(string metricId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM metric WHERE id = @Id", new { Id = metricId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<string>> GetCompanyAgentRunsAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<string>(new CommandDefinition(
            @"SELECT COALESCE(ar.status, 'unknown') || ' @ ' || ar.started_at
                FROM agent_run ar
                INNER JOIN agent a ON a.id = ar.agent_id
               WHERE a.company_id = @CompanyId
            ORDER BY ar.started_at DESC",
            new { CompanyId = companyId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private static async Task ReplaceCompanyTagsAsync(SqliteConnection connection, string companyId, IEnumerable<string> tagIds, string now, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM company_tag WHERE company_id = @CompanyId", new { CompanyId = companyId }, cancellationToken: cancellationToken));
        foreach (var tagId in tagIds.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO company_tag (company_id, tag_id, created_at) VALUES (@CompanyId, @TagId, @Now)",
                new { CompanyId = companyId, TagId = tagId, Now = now }, cancellationToken: cancellationToken));
        }
    }

    private async Task<IReadOnlyList<DocumentRecord>> QueryDocumentsByCompanyAsync(string companyId, CancellationToken cancellationToken)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<DocumentRecord>(new CommandDefinition(
            @"SELECT d.id,
                     d.title,
                     COALESCE(d.doc_type, d.document_type, '') AS DocType,
                     d.company_id AS CompanyId,
                     c.name AS CompanyName,
                     d.published_at AS PublishedAt,
                     COALESCE(d.imported_at, d.created_at) AS ImportedAt,
                     d.file_path AS FilePath,
                     COALESCE(d.content_hash, '') AS ContentHash,
                     NULL AS ExtractedText
                FROM document d
                LEFT JOIN company c ON c.id = d.company_id
               WHERE d.company_id = @CompanyId
            ORDER BY COALESCE(d.imported_at, d.created_at) DESC", new { CompanyId = companyId }, cancellationToken: cancellationToken));
        return rows.ToList();
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


    private sealed class MetricRow
    {
        public string Id { get; init; } = string.Empty;
        public string MetricName { get; init; } = string.Empty;
        public string Period { get; init; } = string.Empty;
        public double? Value { get; init; }
        public string? Unit { get; init; }
        public string? Currency { get; init; }
        public string? SnippetId { get; init; }
        public string? DocumentId { get; init; }
        public string? DocumentTitle { get; init; }
        public string? Locator { get; init; }
        public string? SourceTitle { get; init; }
        public string? SnippetText { get; init; }
        public string CreatedAt { get; init; } = string.Empty;
    }

    private sealed class CompanyRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Ticker { get; init; }
        public string? Isin { get; init; }
        public string? Sector { get; init; }
        public string? Industry { get; init; }
        public string? Currency { get; init; }
        public string? Summary { get; init; }
        public string TagList { get; init; } = string.Empty;
    }
}
