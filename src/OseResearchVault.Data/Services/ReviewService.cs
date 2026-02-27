using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class ReviewService(IAppSettingsService appSettingsService, IFtsSyncService ftsSyncService) : IReviewService
{
    public async Task<WeeklyReviewResult> GenerateWeeklyReviewAsync(string workspaceId, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var resolvedWorkspaceId = await ResolveWorkspaceIdAsync(connection, workspaceId, cancellationToken);
        var windowStart = asOfDate.AddDays(-6).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var windowEndExclusive = asOfDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dueSoonEndExclusive = asOfDate.AddDays(15).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var documents = (await connection.QueryAsync<DocumentRow>(new CommandDefinition(
            @"SELECT d.id AS DocumentId,
                     d.title AS Title,
                     COALESCE(c.name, 'Unassigned') AS CompanyName,
                     d.created_at AS CreatedAt
                FROM document d
                LEFT JOIN company c ON c.id = d.company_id
               WHERE d.workspace_id = @WorkspaceId
                 AND d.created_at >= @WindowStart
                 AND d.created_at < @WindowEnd
            ORDER BY COALESCE(c.name, 'Unassigned'), d.created_at DESC",
            new { WorkspaceId = resolvedWorkspaceId, WindowStart = windowStart.ToString("O"), WindowEnd = windowEndExclusive.ToString("O") },
            cancellationToken: cancellationToken))).ToList();

        var notes = (await connection.QueryAsync<NoteRow>(new CommandDefinition(
            @"SELECT n.id AS NoteId,
                     n.title AS Title,
                     COALESCE(c.name, 'Unassigned') AS CompanyName,
                     CASE WHEN n.updated_at > n.created_at THEN n.updated_at ELSE n.created_at END AS ActivityAt
                FROM note n
                LEFT JOIN company c ON c.id = n.company_id
               WHERE n.workspace_id = @WorkspaceId
                 AND n.note_type <> 'log'
                 AND (n.created_at >= @WindowStart OR n.updated_at >= @WindowStart)
                 AND (n.created_at < @WindowEnd OR n.updated_at < @WindowEnd)
            ORDER BY ActivityAt DESC",
            new { WorkspaceId = resolvedWorkspaceId, WindowStart = windowStart.ToString("O"), WindowEnd = windowEndExclusive.ToString("O") },
            cancellationToken: cancellationToken))).ToList();

        var agentRuns = (await connection.QueryAsync<AgentRunRow>(new CommandDefinition(
            @"SELECT ar.id AS RunId,
                     COALESCE(a.name, 'Agent') AS AgentName,
                     COALESCE(c.name, 'Unassigned') AS CompanyName,
                     ar.status AS Status,
                     ar.started_at AS StartedAt,
                     COUNT(DISTINCT art.id) AS ArtifactCount
                FROM agent_run ar
                LEFT JOIN agent a ON a.id = ar.agent_id
                LEFT JOIN company c ON c.id = ar.company_id
                LEFT JOIN artifact art ON art.agent_run_id = ar.id
               WHERE ar.workspace_id = @WorkspaceId
                 AND ar.started_at >= @WindowStart
                 AND ar.started_at < @WindowEnd
            GROUP BY ar.id, a.name, c.name, ar.status, ar.started_at
            ORDER BY ar.started_at DESC",
            new { WorkspaceId = resolvedWorkspaceId, WindowStart = windowStart.ToString("O"), WindowEnd = windowEndExclusive.ToString("O") },
            cancellationToken: cancellationToken))).ToList();

        var upcomingEvents = (await connection.QueryAsync<EventRow>(new CommandDefinition(
            @"SELECT e.id AS EventId,
                     e.title AS Title,
                     COALESCE(c.name, 'Unassigned') AS CompanyName,
                     e.occurred_at AS OccurredAt,
                     e.event_type AS EventType
                FROM event e
                LEFT JOIN company c ON c.id = e.company_id
               WHERE e.workspace_id = @WorkspaceId
                 AND e.occurred_at >= @AsOfStart
                 AND e.occurred_at < @DueSoonEnd
            ORDER BY e.occurred_at ASC",
            new { WorkspaceId = resolvedWorkspaceId, AsOfStart = asOfDate.ToString("yyyy-MM-dd"), DueSoonEnd = dueSoonEndExclusive.ToString("yyyy-MM-dd") },
            cancellationToken: cancellationToken))).ToList();

        var catalysts = (await connection.QueryAsync<CatalystRow>(new CommandDefinition(
            @"SELECT cat.catalyst_id AS CatalystId,
                     cat.title AS Title,
                     COALESCE(c.name, 'Unassigned') AS CompanyName,
                     cat.expected_start AS ExpectedStart,
                     cat.status AS Status
                FROM catalyst cat
                LEFT JOIN company c ON c.id = cat.company_id
               WHERE cat.workspace_id = @WorkspaceId
                 AND cat.status = 'open'
                 AND cat.expected_start IS NOT NULL
                 AND cat.expected_start >= @AsOfStart
                 AND cat.expected_start < @DueSoonEnd
            ORDER BY cat.expected_start ASC",
            new { WorkspaceId = resolvedWorkspaceId, AsOfStart = asOfDate.ToString("yyyy-MM-dd"), DueSoonEnd = dueSoonEndExclusive.ToString("yyyy-MM-dd") },
            cancellationToken: cancellationToken))).ToList();

        var trades = (await connection.QueryAsync<TradeRow>(new CommandDefinition(
            @"SELECT t.trade_id AS TradeId,
                     COALESCE(c.name, 'Unassigned') AS CompanyName,
                     t.trade_date AS TradeDate,
                     t.side AS Side,
                     t.quantity AS Quantity,
                     t.price AS Price,
                     t.currency AS Currency
                FROM trade t
                LEFT JOIN company c ON c.id = t.company_id
               WHERE t.workspace_id = @WorkspaceId
                 AND t.trade_date >= @WindowStartDate
                 AND t.trade_date < @WindowEndDate
            ORDER BY t.trade_date DESC, c.name",
            new { WorkspaceId = resolvedWorkspaceId, WindowStartDate = windowStart.ToString("yyyy-MM-dd"), WindowEndDate = windowEndExclusive.ToString("yyyy-MM-dd") },
            cancellationToken: cancellationToken))).ToList();

        var positionDeltas = trades
            .GroupBy(t => t.CompanyName)
            .Select(g => new PositionDelta(g.Key, g.Sum(x => string.Equals(x.Side, "buy", StringComparison.OrdinalIgnoreCase) ? x.Quantity : -x.Quantity)))
            .OrderByDescending(d => Math.Abs(d.NetQuantity))
            .ToList();

        var title = $"Weekly Review {asOfDate:yyyy-MM-dd}";
        var noteContent = BuildWeeklyReviewContent(asOfDate, windowStart, documents, notes, agentRuns, upcomingEvents, catalysts, trades, positionDeltas);
        var noteId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO note (id, workspace_id, title, content, note_type, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @Title, @Content, 'log', @Now, @Now)",
            new { Id = noteId, WorkspaceId = resolvedWorkspaceId, Title = title, Content = noteContent, Now = now },
            cancellationToken: cancellationToken));

        await ftsSyncService.UpsertNoteAsync(noteId, title, noteContent, cancellationToken);

        return new WeeklyReviewResult
        {
            NoteId = noteId,
            NoteTitle = title,
            ImportedDocumentCount = documents.Count,
            RecentNotesCount = notes.Count,
            AgentRunCount = agentRuns.Count,
            UpcomingCatalystCount = catalysts.Count,
            RecentTradeCount = trades.Count
        };
    }


    public async Task<QuarterlyReviewResult> GenerateQuarterlyCompanyReviewAsync(string workspaceId, string companyId, string periodLabel, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyId))
        {
            throw new InvalidOperationException("Company is required for quarterly review generation.");
        }

        var normalizedPeriod = NormalizeQuarterLabel(periodLabel);
        var (periodStart, periodEndExclusive) = ResolveQuarterWindow(normalizedPeriod);

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var resolvedWorkspaceId = await ResolveWorkspaceIdAsync(connection, workspaceId, cancellationToken);
        var company = await connection.QuerySingleOrDefaultAsync<CompanyRow>(new CommandDefinition(
            @"SELECT id AS CompanyId, name AS CompanyName
                FROM company
               WHERE id = @CompanyId
                 AND workspace_id = @WorkspaceId",
            new { CompanyId = companyId, WorkspaceId = resolvedWorkspaceId },
            cancellationToken: cancellationToken));

        if (company is null)
        {
            throw new InvalidOperationException("Company not found for quarterly review generation.");
        }

        var thesisVersions = (await connection.QueryAsync<ThesisRow>(new CommandDefinition(
            @"SELECT thesis_version_id AS ThesisVersionId,
                     title AS Title,
                     body AS Body,
                     created_at AS CreatedAt
                FROM thesis_version
               WHERE company_id = @CompanyId
            ORDER BY created_at DESC",
            new { CompanyId = companyId },
            cancellationToken: cancellationToken))).ToList();

        var journalEntries = (await connection.QueryAsync<JournalRow>(new CommandDefinition(
            @"SELECT journal_entry_id AS JournalEntryId,
                     entry_date AS EntryDate,
                     action AS Action,
                     rationale AS Rationale
                FROM journal_entry
               WHERE company_id = @CompanyId
                 AND entry_date >= @WindowStart
                 AND entry_date < @WindowEnd
            ORDER BY entry_date DESC",
            new { CompanyId = companyId, WindowStart = periodStart.ToString("yyyy-MM-dd"), WindowEnd = periodEndExclusive.ToString("yyyy-MM-dd") },
            cancellationToken: cancellationToken))).ToList();

        var metricRows = (await connection.QueryAsync<MetricRow>(new CommandDefinition(
            @"SELECT metric_id AS MetricId,
                     metric_name AS MetricKey,
                     COALESCE(NULLIF(TRIM(period), ''), substr(created_at, 1, 10)) AS PeriodLabel,
                     COALESCE(value, 0) AS MetricValue,
                     unit AS Unit,
                     currency AS Currency,
                     created_at AS RecordedAt
                FROM metric
               WHERE company_id = @CompanyId
            ORDER BY created_at DESC",
            new { CompanyId = companyId },
            cancellationToken: cancellationToken))).ToList();

        var scenarios = (await connection.QueryAsync<ScenarioRow>(new CommandDefinition(
            @"SELECT scenario_id AS ScenarioId,
                     name AS Name,
                     probability AS Probability,
                     assumptions AS Assumptions
                FROM scenario
               WHERE company_id = @CompanyId
            ORDER BY probability DESC, name",
            new { CompanyId = companyId },
            cancellationToken: cancellationToken))).ToList();

        var scenarioKpis = (await connection.QueryAsync<ScenarioKpiRow>(new CommandDefinition(
            @"SELECT sk.scenario_kpi_id AS ScenarioKpiId,
                     sk.scenario_id AS ScenarioId,
                     sk.kpi_name AS KpiName,
                     sk.period AS Period,
                     sk.value AS Value,
                     sk.unit AS Unit,
                     sk.currency AS Currency
                FROM scenario_kpi sk
                JOIN scenario s ON s.scenario_id = sk.scenario_id
               WHERE s.company_id = @CompanyId
            ORDER BY sk.period DESC, sk.kpi_name",
            new { CompanyId = companyId },
            cancellationToken: cancellationToken))).ToList();

        var catalysts = (await connection.QueryAsync<CatalystDetailRow>(new CommandDefinition(
            @"SELECT catalyst_id AS CatalystId,
                     title AS Title,
                     status AS Status,
                     impact AS Impact,
                     expected_start AS ExpectedStart
                FROM catalyst
               WHERE company_id = @CompanyId
            ORDER BY CASE status WHEN 'open' THEN 0 WHEN 'done' THEN 1 ELSE 2 END,
                     COALESCE(expected_start, '9999-12-31'),
                     title",
            new { CompanyId = companyId },
            cancellationToken: cancellationToken))).ToList();

        var documents = (await connection.QueryAsync<DocumentLinkRow>(new CommandDefinition(
            @"SELECT id AS ItemId,
                     title AS Title,
                     created_at AS CreatedAt
                FROM document
               WHERE company_id = @CompanyId
                 AND created_at >= @WindowStart
                 AND created_at < @WindowEnd
            ORDER BY created_at DESC",
            new { CompanyId = companyId, WindowStart = periodStart.ToString("O"), WindowEnd = periodEndExclusive.ToString("O") },
            cancellationToken: cancellationToken))).ToList();

        var evidences = (await connection.QueryAsync<EvidenceLinkRow>(new CommandDefinition(
            @"SELECT s.id AS SnippetId,
                     COALESCE(d.title, 'Untitled document') AS DocumentTitle,
                     s.created_at AS CreatedAt
                FROM snippet s
                LEFT JOIN document d ON d.id = s.document_id
               WHERE d.company_id = @CompanyId
                  AND s.created_at >= @WindowStart
                  AND s.created_at < @WindowEnd
             ORDER BY s.created_at DESC",
            new { CompanyId = companyId, WindowStart = periodStart.ToString("O"), WindowEnd = periodEndExclusive.ToString("O") },
            cancellationToken: cancellationToken))).ToList();

        var title = $"Quarterly Review {company.CompanyName} {normalizedPeriod}";
        var content = BuildQuarterlyReviewContent(company.CompanyName, normalizedPeriod, periodStart, periodEndExclusive, thesisVersions, journalEntries, metricRows, scenarios, scenarioKpis, catalysts, documents, evidences);
        var noteId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO note (id, workspace_id, company_id, title, content, note_type, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @CompanyId, @Title, @Content, 'log', @Now, @Now)",
            new { Id = noteId, WorkspaceId = resolvedWorkspaceId, CompanyId = companyId, Title = title, Content = content, Now = now },
            cancellationToken: cancellationToken));

        await ftsSyncService.UpsertNoteAsync(noteId, title, content, cancellationToken);

        return new QuarterlyReviewResult
        {
            NoteId = noteId,
            NoteTitle = title,
            JournalEntriesCount = journalEntries.Count,
            DocumentCount = documents.Count,
            EvidenceCount = evidences.Count,
            ScenarioCount = scenarios.Count,
            CatalystCount = catalysts.Count
        };
    }

    private static string BuildWeeklyReviewContent(
        DateOnly asOfDate,
        DateTime windowStart,
        IReadOnlyList<DocumentRow> documents,
        IReadOnlyList<NoteRow> notes,
        IReadOnlyList<AgentRunRow> agentRuns,
        IReadOnlyList<EventRow> upcomingEvents,
        IReadOnlyList<CatalystRow> catalysts,
        IReadOnlyList<TradeRow> trades,
        IReadOnlyList<PositionDelta> positionDeltas)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Weekly Review ({windowStart:yyyy-MM-dd} to {asOfDate:yyyy-MM-dd})");
        sb.AppendLine();

        sb.AppendLine("## New documents imported (last 7 days)");
        if (documents.Count == 0)
        {
            sb.AppendLine("- None");
        }
        else
        {
            foreach (var group in documents.GroupBy(d => d.CompanyName).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"- **{group.Key}**");
                foreach (var item in group)
                {
                    sb.AppendLine($"  - {item.Title} (`document:{item.DocumentId}`) imported {FormatDate(item.CreatedAt)}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("## New notes created or updated (last 7 days)");
        if (notes.Count == 0)
        {
            sb.AppendLine("- None");
        }
        else
        {
            foreach (var note in notes)
            {
                sb.AppendLine($"- {note.Title} ({note.CompanyName}) (`note:{note.NoteId}`) activity {FormatDate(note.ActivityAt)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## New agent runs and artifacts (last 7 days)");
        if (agentRuns.Count == 0)
        {
            sb.AppendLine("- None");
        }
        else
        {
            foreach (var run in agentRuns)
            {
                sb.AppendLine($"- {run.AgentName} on {run.CompanyName} (`agent_run:{run.RunId}`) status={run.Status}, artifacts={run.ArtifactCount}, started {FormatDate(run.StartedAt)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Upcoming events / catalysts due soon");
        if (upcomingEvents.Count == 0 && catalysts.Count == 0)
        {
            sb.AppendLine("- None");
        }
        else
        {
            foreach (var eventItem in upcomingEvents)
            {
                sb.AppendLine($"- {eventItem.CompanyName}: {eventItem.Title} (`event:{eventItem.EventId}`), type={eventItem.EventType}, date {FormatDate(eventItem.OccurredAt)}");
            }

            foreach (var catalyst in catalysts)
            {
                sb.AppendLine($"- {catalyst.CompanyName}: {catalyst.Title} (`catalyst:{catalyst.CatalystId}`), expected {catalyst.ExpectedStart}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Portfolio changes (last 7 days)");
        if (trades.Count == 0)
        {
            sb.AppendLine("- No trades recorded in the period.");
        }
        else
        {
            sb.AppendLine("### Trades");
            foreach (var trade in trades)
            {
                sb.AppendLine($"- {trade.TradeDate}: {trade.CompanyName} {trade.Side} {trade.Quantity:0.####} @ {trade.Price:0.####} {trade.Currency} (`trade:{trade.TradeId}`)");
            }

            sb.AppendLine();
            sb.AppendLine("### Position deltas");
            foreach (var delta in positionDeltas)
            {
                sb.AppendLine($"- {delta.CompanyName}: {(delta.NetQuantity >= 0 ? "+" : string.Empty)}{delta.NetQuantity:0.####} shares net");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildQuarterlyReviewContent(
        string companyName,
        string periodLabel,
        DateOnly periodStart,
        DateOnly periodEndExclusive,
        IReadOnlyList<ThesisRow> thesisVersions,
        IReadOnlyList<JournalRow> journalEntries,
        IReadOnlyList<MetricRow> metricRows,
        IReadOnlyList<ScenarioRow> scenarios,
        IReadOnlyList<ScenarioKpiRow> scenarioKpis,
        IReadOnlyList<CatalystDetailRow> catalysts,
        IReadOnlyList<DocumentLinkRow> documents,
        IReadOnlyList<EvidenceLinkRow> evidences)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Quarterly Review {companyName} {periodLabel}");
        sb.AppendLine($"Period window: {periodStart:yyyy-MM-dd} to {periodEndExclusive.AddDays(-1):yyyy-MM-dd}");
        sb.AppendLine();

        sb.AppendLine("## Latest thesis");
        if (thesisVersions.Count == 0)
        {
            sb.AppendLine("- None recorded.");
        }
        else
        {
            var latest = thesisVersions[0];
            sb.AppendLine($"- **{latest.Title}** (`thesis_version:{latest.ThesisVersionId}`), updated {FormatDate(latest.CreatedAt)}");
            sb.AppendLine($"- Summary: {latest.Body.Replace(Environment.NewLine, " ").Trim()}");
            sb.AppendLine("- Thesis history:");
            foreach (var thesis in thesisVersions.Take(5))
            {
                sb.AppendLine($"  - {thesis.Title} (`thesis_version:{thesis.ThesisVersionId}`) at {FormatDate(thesis.CreatedAt)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Journal entries since last quarter");
        if (journalEntries.Count == 0)
        {
            sb.AppendLine("- None");
        }
        else
        {
            foreach (var entry in journalEntries)
            {
                sb.AppendLine($"- {entry.EntryDate}: {entry.Action} (`journal_entry:{entry.JournalEntryId}`) â€” {entry.Rationale}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Metrics table (top metrics, last 4 periods)");
        var topMetricNames = metricRows.GroupBy(m => m.MetricKey)
            .OrderByDescending(g => g.Max(x => x.RecordedAt))
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        if (topMetricNames.Count == 0)
        {
            sb.AppendLine("- None");
        }
        else
        {
            sb.AppendLine("| Metric | P1 | P2 | P3 | P4 |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var metricName in topMetricNames)
            {
                var cells = metricRows.Where(m => string.Equals(m.MetricKey, metricName, StringComparison.OrdinalIgnoreCase))
                    .Take(4)
                    .Select(m => $"{m.PeriodLabel}: {m.MetricValue:0.####}{(string.IsNullOrWhiteSpace(m.Unit) ? string.Empty : $" {m.Unit}")}{(string.IsNullOrWhiteSpace(m.Currency) ? string.Empty : $" {m.Currency}")}")
                    .ToList();
                while (cells.Count < 4)
                {
                    cells.Add("-");
                }

                sb.AppendLine($"| {metricName} | {cells[0]} | {cells[1]} | {cells[2]} | {cells[3]} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Scenario probabilities + key KPIs");
        if (scenarios.Count == 0)
        {
            sb.AppendLine("- None");
        }
        else
        {
            foreach (var scenario in scenarios)
            {
                sb.AppendLine($"- **{scenario.Name}** (`scenario:{scenario.ScenarioId}`): probability {scenario.Probability:P1}");
                foreach (var kpi in scenarioKpis.Where(k => k.ScenarioId == scenario.ScenarioId).Take(3))
                {
                    sb.AppendLine($"  - KPI {kpi.KpiName} ({kpi.Period}) = {kpi.Value:0.####} {kpi.Unit} {kpi.Currency} (`scenario_kpi:{kpi.ScenarioKpiId}`)");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Catalysts status summary");
        if (catalysts.Count == 0)
        {
            sb.AppendLine("- None");
        }
        else
        {
            sb.AppendLine($"- Open: {catalysts.Count(c => c.Status == "open")}, Done: {catalysts.Count(c => c.Status == "done")}, Invalidated: {catalysts.Count(c => c.Status == "invalidated")}");
            foreach (var catalyst in catalysts.Take(8))
            {
                sb.AppendLine($"  - {catalyst.Title} (`catalyst:{catalyst.CatalystId}`), status={catalyst.Status}, impact={catalyst.Impact}, expected={catalyst.ExpectedStart}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## New evidence/documents since last quarter");
        sb.AppendLine($"- Documents added: {documents.Count}");
        foreach (var document in documents.Take(10))
        {
            sb.AppendLine($"  - {document.Title} (`document:{document.ItemId}`) on {FormatDate(document.CreatedAt)}");
        }

        sb.AppendLine($"- Evidence snippets added: {evidences.Count}");
        foreach (var evidence in evidences.Take(10))
        {
            sb.AppendLine($"  - {evidence.DocumentTitle} (`snippet:{evidence.SnippetId}`) on {FormatDate(evidence.CreatedAt)}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string NormalizeQuarterLabel(string periodLabel)
    {
        var value = (periodLabel ?? string.Empty).Trim().ToUpperInvariant().Replace("-", string.Empty).Replace(" ", string.Empty);
        if (value.Length != 6 || value[4] != 'Q' || !int.TryParse(value[..4], out _) || value[5] < '1' || value[5] > '4')
        {
            throw new InvalidOperationException("Period must use format YYYYQ1..YYYYQ4.");
        }

        return value;
    }

    private static (DateOnly Start, DateOnly EndExclusive) ResolveQuarterWindow(string periodLabel)
    {
        var year = int.Parse(periodLabel[..4]);
        var quarter = int.Parse(periodLabel[5..]);
        var startMonth = ((quarter - 1) * 3) + 1;
        var start = new DateOnly(year, startMonth, 1);
        return (start, start.AddMonths(3));
    }

    private static string FormatDate(string value)
    {
        return DateTime.TryParse(value, out var parsed)
            ? parsed.ToString("yyyy-MM-dd")
            : value;
    }

    private static async Task<string> ResolveWorkspaceIdAsync(SqliteConnection connection, string workspaceId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            return workspaceId;
        }

        var resolved = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT id FROM workspace ORDER BY created_at LIMIT 1",
            cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new InvalidOperationException("No workspace found for weekly review generation.");
        }

        return resolved;
    }

    private static SqliteConnection OpenConnection(string databasePath)
        => new(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, Pooling = false }.ToString());

    private sealed record DocumentRow(string DocumentId, string Title, string CompanyName, string CreatedAt);
    private sealed record NoteRow(string NoteId, string Title, string CompanyName, string ActivityAt);
    private sealed record AgentRunRow(string RunId, string AgentName, string CompanyName, string Status, string StartedAt, long ArtifactCount);
    private sealed record EventRow(string EventId, string Title, string CompanyName, string OccurredAt, string EventType);
    private sealed record CatalystRow(string CatalystId, string Title, string CompanyName, string? ExpectedStart, string Status);
    private sealed record TradeRow(string TradeId, string CompanyName, string TradeDate, string Side, double Quantity, double Price, string Currency);
    private sealed record PositionDelta(string CompanyName, double NetQuantity);
    private sealed record CompanyRow(string CompanyId, string CompanyName);
    private sealed record ThesisRow(string ThesisVersionId, string Title, string Body, string CreatedAt);
    private sealed record JournalRow(string JournalEntryId, string EntryDate, string Action, string Rationale);
    private sealed record MetricRow(string MetricId, string MetricKey, string PeriodLabel, double MetricValue, string? Unit, string? Currency, string RecordedAt);
    private sealed record ScenarioRow(string ScenarioId, string Name, double Probability, string? Assumptions);
    private sealed record ScenarioKpiRow(string ScenarioKpiId, string ScenarioId, string KpiName, string Period, double Value, string? Unit, string? Currency);
    private sealed record CatalystDetailRow(string CatalystId, string Title, string Status, string Impact, string? ExpectedStart);
    private sealed record DocumentLinkRow(string ItemId, string Title, string CreatedAt);
    private sealed record EvidenceLinkRow(string SnippetId, string DocumentTitle, string CreatedAt);
}
