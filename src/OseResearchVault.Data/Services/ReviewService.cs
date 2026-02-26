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
        => new(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString());

    private sealed record DocumentRow(string DocumentId, string Title, string CompanyName, string CreatedAt);
    private sealed record NoteRow(string NoteId, string Title, string CompanyName, string ActivityAt);
    private sealed record AgentRunRow(string RunId, string AgentName, string CompanyName, string Status, string StartedAt, int ArtifactCount);
    private sealed record EventRow(string EventId, string Title, string CompanyName, string OccurredAt, string EventType);
    private sealed record CatalystRow(string CatalystId, string Title, string CompanyName, string? ExpectedStart, string Status);
    private sealed record TradeRow(string TradeId, string CompanyName, string TradeDate, string Side, double Quantity, double Price, string Currency);
    private sealed record PositionDelta(string CompanyName, double NetQuantity);
}
