using Dapper;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;
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
            "SELECT metric_key || ' [' || COALESCE(period_end, 'n/a') || ']: ' || COALESCE(CAST(metric_value AS TEXT), 'n/a') || ' ' || COALESCE(unit, '') FROM metric WHERE company_id = @CompanyId ORDER BY recorded_at DESC",
            @"SELECT DISTINCT metric_key
                FROM metric
               WHERE company_id = @CompanyId
                 AND metric_key IS NOT NULL
                 AND TRIM(metric_key) <> ''
            ORDER BY metric_key",
            @"SELECT metric_key
                     || CASE WHEN COALESCE(period_label, '') = '' THEN '' ELSE ' (' || period_label || ')' END
                     || ': '
                     || COALESCE(CAST(metric_value AS TEXT), 'n/a')
                     || CASE WHEN COALESCE(unit, '') = '' THEN '' ELSE ' ' || unit END
                     || CASE WHEN COALESCE(currency, '') = '' THEN '' ELSE ' [' || currency || ']' END
                FROM metric
               WHERE company_id = @CompanyId
            ORDER BY recorded_at DESC",
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

    public async Task<IReadOnlyList<ScenarioRecord>> GetCompanyScenariosAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ScenarioRecord>(new CommandDefinition(
            @"SELECT scenario_id AS ScenarioId,
                     COALESCE(workspace_id, '') AS WorkspaceId,
                     company_id AS CompanyId,
                     name AS Name,
                     probability AS Probability,
                     assumptions AS Assumptions,
                     created_at AS CreatedAt,
                     updated_at AS UpdatedAt
                FROM scenario
               WHERE company_id = @CompanyId
            ORDER BY created_at, name",
            new { CompanyId = companyId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<string> CreateScenarioAsync(string companyId, ScenarioUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ValidateScenario(request);

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var scenarioId = Guid.NewGuid().ToString();

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO scenario (scenario_id, workspace_id, company_id, name, probability, assumptions, created_at, updated_at)
              VALUES (@ScenarioId, @WorkspaceId, @CompanyId, @Name, @Probability, @Assumptions, @Now, @Now)",
            new
            {
                ScenarioId = scenarioId,
                WorkspaceId = workspaceId,
                CompanyId = companyId,
                Name = request.Name.Trim(),
                request.Probability,
                Assumptions = string.IsNullOrWhiteSpace(request.Assumptions) ? null : request.Assumptions.Trim(),
                Now = now
            }, cancellationToken: cancellationToken));

        return scenarioId;
    }

    public async Task UpdateScenarioAsync(string scenarioId, ScenarioUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ValidateScenario(request);

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE scenario
                 SET name = @Name,
                     probability = @Probability,
                     assumptions = @Assumptions,
                     updated_at = @Now
               WHERE scenario_id = @ScenarioId",
            new
            {
                ScenarioId = scenarioId,
                Name = request.Name.Trim(),
                request.Probability,
                Assumptions = string.IsNullOrWhiteSpace(request.Assumptions) ? null : request.Assumptions.Trim(),
                Now = now
            }, cancellationToken: cancellationToken));
    }

    public async Task DeleteScenarioAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM scenario WHERE scenario_id = @ScenarioId", new { ScenarioId = scenarioId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ScenarioKpiRecord>> GetScenarioKpisAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ScenarioKpiRecord>(new CommandDefinition(
            @"SELECT scenario_kpi_id AS ScenarioKpiId,
                     COALESCE(workspace_id, '') AS WorkspaceId,
                     scenario_id AS ScenarioId,
                     kpi_name AS KpiName,
                     period AS Period,
                     value AS Value,
                     unit AS Unit,
                     currency AS Currency,
                     snippet_id AS SnippetId,
                     created_at AS CreatedAt
                FROM scenario_kpi
               WHERE scenario_id = @ScenarioId
            ORDER BY period, kpi_name",
            new { ScenarioId = scenarioId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<string> CreateScenarioKpiAsync(string scenarioId, ScenarioKpiUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ValidateScenarioKpi(request);

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var scenarioKpiId = Guid.NewGuid().ToString();
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO scenario_kpi (scenario_kpi_id, workspace_id, scenario_id, kpi_name, period, value, unit, currency, snippet_id, created_at)
              VALUES (@ScenarioKpiId, @WorkspaceId, @ScenarioId, @KpiName, @Period, @Value, @Unit, @Currency, @SnippetId, @Now)",
            new
            {
                ScenarioKpiId = scenarioKpiId,
                WorkspaceId = workspaceId,
                ScenarioId = scenarioId,
                KpiName = NormalizeKpiName(request.KpiName),
                Period = request.Period.Trim(),
                request.Value,
                Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
                Currency = string.IsNullOrWhiteSpace(request.Currency) ? null : request.Currency.Trim(),
                SnippetId = string.IsNullOrWhiteSpace(request.SnippetId) ? null : request.SnippetId.Trim(),
                Now = now
            }, cancellationToken: cancellationToken));

        return scenarioKpiId;
    }

    public async Task UpdateScenarioKpiAsync(string scenarioKpiId, ScenarioKpiUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ValidateScenarioKpi(request);

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE scenario_kpi
                 SET kpi_name = @KpiName,
                     period = @Period,
                     value = @Value,
                     unit = @Unit,
                     currency = @Currency,
                     snippet_id = @SnippetId
               WHERE scenario_kpi_id = @ScenarioKpiId",
            new
            {
                ScenarioKpiId = scenarioKpiId,
                KpiName = NormalizeKpiName(request.KpiName),
                Period = request.Period.Trim(),
                request.Value,
                Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
                Currency = string.IsNullOrWhiteSpace(request.Currency) ? null : request.Currency.Trim(),
                SnippetId = string.IsNullOrWhiteSpace(request.SnippetId) ? null : request.SnippetId.Trim()
            }, cancellationToken: cancellationToken));
    }

    public async Task DeleteScenarioKpiAsync(string scenarioKpiId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM scenario_kpi WHERE scenario_kpi_id = @ScenarioKpiId", new { ScenarioKpiId = scenarioKpiId }, cancellationToken: cancellationToken));
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

    public async Task<PriceImportResult> ImportCompanyDailyPricesCsvAsync(string companyId, string csvFilePath, string? dateColumn = null, string? closeColumn = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyId))
        {
            throw new ArgumentException("Company is required.", nameof(companyId));
        }

        if (string.IsNullOrWhiteSpace(csvFilePath) || !File.Exists(csvFilePath))
        {
            throw new FileNotFoundException("CSV file was not found.", csvFilePath);
        }

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var sourceId = Guid.NewGuid().ToString();
        var documentId = Guid.NewGuid().ToString();
        var csvText = await File.ReadAllTextAsync(csvFilePath, cancellationToken);
        var rows = ParseCsvRows(csvText);
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("CSV file is empty.");
        }

        var header = rows[0];
        var effectiveDateColumn = string.IsNullOrWhiteSpace(dateColumn) ? "date" : dateColumn.Trim();
        var effectiveCloseColumn = string.IsNullOrWhiteSpace(closeColumn) ? "close" : closeColumn.Trim();
        var dateIndex = FindColumnIndex(header, effectiveDateColumn);
        var closeIndex = FindColumnIndex(header, effectiveCloseColumn);
        var hasHeader = dateIndex >= 0 && closeIndex >= 0;
        if (!hasHeader)
        {
            dateIndex = 0;
            closeIndex = 1;
        }

        var importedCount = 0;
        var skippedCount = 0;
        var snapshotDirectory = Path.Combine(settings.VaultStorageDirectory, "snapshots");
        Directory.CreateDirectory(snapshotDirectory);
        var snapshotPath = Path.Combine(snapshotDirectory, $"price-import-{DateTime.UtcNow:yyyyMMddHHmmss}-{Path.GetFileName(csvFilePath)}");
        File.Copy(csvFilePath, snapshotPath, overwrite: true);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO source (id, workspace_id, company_id, name, source_type, url, fetched_at, retrieved_at, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @CompanyId, @Name, 'file', @Url, @Now, @Now, @Now, @Now)",
            new
            {
                Id = sourceId,
                WorkspaceId = workspaceId,
                CompanyId = companyId,
                Name = $"Price CSV import ({Path.GetFileName(csvFilePath)})",
                Url = $"file://{Path.GetFullPath(csvFilePath)}",
                Now = now
            }, transaction: tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO document (id, workspace_id, source_id, company_id, title, doc_type, document_type, mime_type, file_path, imported_at, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @SourceId, @CompanyId, @Title, 'csv', 'csv', 'text/csv', @FilePath, @Now, @Now, @Now)",
            new
            {
                Id = documentId,
                WorkspaceId = workspaceId,
                SourceId = sourceId,
                CompanyId = companyId,
                Title = $"Price CSV ({Path.GetFileName(csvFilePath)})",
                FilePath = snapshotPath,
                Now = now
            }, transaction: tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO document_text (id, document_id, chunk_index, language, content, created_at, updated_at)
              VALUES (@Id, @DocumentId, 0, 'en', @Content, @Now, @Now)",
            new { Id = Guid.NewGuid().ToString(), DocumentId = documentId, Content = csvText, Now = now }, transaction: tx, cancellationToken: cancellationToken));

        var startRow = hasHeader ? 1 : 0;
        for (var i = startRow; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Count <= Math.Max(dateIndex, closeIndex))
            {
                skippedCount++;
                continue;
            }

            var dateValue = row[dateIndex].Trim();
            var closeValue = row[closeIndex].Trim();
            if (!TryNormalizeDate(dateValue, out var normalizedDate) || !double.TryParse(closeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var close))
            {
                skippedCount++;
                continue;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO price_daily (price_id, workspace_id, company_id, price_date, close, currency, source_id, created_at)
                  VALUES (@PriceId, @WorkspaceId, @CompanyId, @PriceDate, @Close, @Currency, @SourceId, @CreatedAt)
                  ON CONFLICT(workspace_id, company_id, price_date)
                  DO UPDATE SET close = excluded.close, currency = excluded.currency, source_id = excluded.source_id",
                new
                {
                    PriceId = Guid.NewGuid().ToString(),
                    WorkspaceId = workspaceId,
                    CompanyId = companyId,
                    PriceDate = normalizedDate,
                    Close = close,
                    Currency = "NOK",
                    SourceId = sourceId,
                    CreatedAt = now
                }, transaction: tx, cancellationToken: cancellationToken));

            importedCount++;
        }

        await tx.CommitAsync(cancellationToken);
        return new PriceImportResult { InsertedOrUpdatedCount = importedCount, SkippedCount = skippedCount, SourceId = sourceId, DocumentId = documentId };
    }

    public async Task<PriceDailyRecord?> GetLatestCompanyPriceAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<PriceDailyRecord>(new CommandDefinition(
            @"SELECT price_id AS PriceId, workspace_id AS WorkspaceId, company_id AS CompanyId, price_date AS PriceDate,
                     close, currency, source_id AS SourceId, created_at AS CreatedAt
                FROM price_daily
               WHERE company_id = @CompanyId
            ORDER BY price_date DESC
               LIMIT 1",
            new { CompanyId = companyId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PriceDailyRecord>> GetCompanyDailyPricesAsync(string companyId, int days = 90, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<PriceDailyRecord>(new CommandDefinition(
            @"SELECT price_id AS PriceId, workspace_id AS WorkspaceId, company_id AS CompanyId, price_date AS PriceDate,
                     close, currency, source_id AS SourceId, created_at AS CreatedAt
                FROM price_daily
               WHERE company_id = @CompanyId
            ORDER BY price_date DESC
               LIMIT @Days",
            new { CompanyId = companyId, Days = days }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private static int FindColumnIndex(IReadOnlyList<string> header, string name)
    {
        for (var i = 0; i < header.Count; i++)
        {
            if (string.Equals(header[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryNormalizeDate(string value, out string isoDate)
    {
        isoDate = string.Empty;
        if (DateTime.TryParseExact(value, ["yyyy-MM-dd", "yyyy/MM/dd", "dd.MM.yyyy", "dd/MM/yyyy", "MM/dd/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            isoDate = parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static List<IReadOnlyList<string>> ParseCsvRows(string csv)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        value.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    value.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(value.ToString());
                    value.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(value.ToString());
                    value.Clear();
                    rows.Add(row.ToList());
                    row.Clear();
                    break;
                default:
                    value.Append(c);
                    break;
            }
        }

        if (value.Length > 0 || row.Count > 0)
        {
            row.Add(value.ToString());
            rows.Add(row.ToList());
        }

        return rows;
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
                 AND COALESCE(d.is_archived, 0) = 0
            ORDER BY COALESCE(d.imported_at, d.created_at) DESC", new { CompanyId = companyId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        return new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString());
    }

    private static void ValidateScenario(ScenarioUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Scenario name is required.");
        }

        if (request.Probability is < 0 or > 1)
        {
            throw new InvalidOperationException("Scenario probability must be between 0 and 1.");
        }
    }

    private static void ValidateScenarioKpi(ScenarioKpiUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.KpiName))
        {
            throw new InvalidOperationException("KPI name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Period))
        {
            throw new InvalidOperationException("KPI period is required.");
        }
    }

    private static string NormalizeKpiName(string kpiName)
    {
        var trimmed = kpiName.Trim().ToLowerInvariant();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(trimmed.Length);
        var previousUnderscore = false;
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousUnderscore = false;
                continue;
            }

            if (!previousUnderscore)
            {
                builder.Append('_');
                previousUnderscore = true;
            }
        }

        return builder.ToString().Trim('_');
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
