using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class ReviewServiceTests
{
    [Fact]
    public async Task GenerateWeeklyReviewAsync_IncludesOnlyRecentItems()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = OpenConnection(settings.DatabaseFilePath);
            await connection.OpenAsync();

            var workspaceId = await connection.QuerySingleAsync<string>("SELECT id FROM workspace LIMIT 1");
            var companyId = Guid.NewGuid().ToString();
            await connection.ExecuteAsync(
                @"INSERT INTO company (id, workspace_id, name, created_at, updated_at)
                  VALUES (@Id, @WorkspaceId, 'RecentCo', @Now, @Now)",
                new { Id = companyId, WorkspaceId = workspaceId, Now = "2026-04-10T00:00:00Z" });

            await SeedDataAsync(connection, workspaceId, companyId);

            var ftsSyncService = new CapturingFtsSyncService();
            var service = new ReviewService(settingsService, ftsSyncService);

            var result = await service.GenerateWeeklyReviewAsync(workspaceId, new DateOnly(2026, 4, 14));

            Assert.Equal(1, result.ImportedDocumentCount);
            Assert.Equal(1, result.RecentNotesCount);
            Assert.Equal(1, result.AgentRunCount);
            Assert.Equal(1, result.UpcomingCatalystCount);
            Assert.Equal(1, result.RecentTradeCount);

            var generated = await connection.QuerySingleAsync<GeneratedNoteRow>(
                "SELECT title AS Title, content AS Content, note_type AS NoteType FROM note WHERE id = @Id",
                new { Id = result.NoteId });

            Assert.Equal("log", generated.NoteType);
            Assert.Equal("Weekly Review 2026-04-14", generated.Title);
            Assert.Contains("Recent filing", generated.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("Old filing", generated.Content, StringComparison.Ordinal);
            Assert.Contains("Recent note", generated.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("Old note", generated.Content, StringComparison.Ordinal);
            Assert.Contains("run-recent", generated.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("run-old", generated.Content, StringComparison.Ordinal);
            Assert.Contains("trade-recent", generated.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("trade-old", generated.Content, StringComparison.Ordinal);
            Assert.Contains("Q1 earnings", generated.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("Old event", generated.Content, StringComparison.Ordinal);
            Assert.Contains("Catalyst next week", generated.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("Catalyst in two months", generated.Content, StringComparison.Ordinal);
            Assert.Equal(result.NoteId, ftsSyncService.LastNoteId);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GenerateQuarterlyCompanyReviewAsync_CreatesExpectedSections()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = OpenConnection(settings.DatabaseFilePath);
            await connection.OpenAsync();

            var workspaceId = await connection.QuerySingleAsync<string>("SELECT id FROM workspace LIMIT 1");
            var companyId = Guid.NewGuid().ToString();
            await connection.ExecuteAsync(
                @"INSERT INTO company (id, workspace_id, name, created_at, updated_at)
                  VALUES (@Id, @WorkspaceId, 'ReviewCo', '2025-12-31T00:00:00Z', '2025-12-31T00:00:00Z')",
                new { Id = companyId, WorkspaceId = workspaceId });

            await connection.ExecuteAsync(
                @"INSERT INTO thesis_version (thesis_version_id, workspace_id, company_id, title, body, created_at, created_by)
                  VALUES
                  ('thesis-new', @WorkspaceId, @CompanyId, 'Core thesis', 'Demand is accelerating.', '2026-01-12T00:00:00Z', 'user'),
                  ('thesis-old', @WorkspaceId, @CompanyId, 'Old thesis', 'Legacy body', '2025-10-01T00:00:00Z', 'user')",
                new { WorkspaceId = workspaceId, CompanyId = companyId });

            await connection.ExecuteAsync(
                @"INSERT INTO journal_entry (journal_entry_id, workspace_id, company_id, action, entry_date, rationale, created_at)
                  VALUES
                  ('journal-1', @WorkspaceId, @CompanyId, 'buy', '2026-01-15', 'New contract wins', '2026-01-15T00:00:00Z'),
                  ('journal-2', @WorkspaceId, @CompanyId, 'hold', '2026-02-12', 'Execution on track', '2026-02-12T00:00:00Z')",
                new { WorkspaceId = workspaceId, CompanyId = companyId });

            await connection.ExecuteAsync(
                @"INSERT INTO metric (id, workspace_id, company_id, metric_key, metric_value, period_end, period_label, unit, recorded_at, created_at)
                  VALUES
                  ('metric-1', @WorkspaceId, @CompanyId, 'Revenue', 120, '2025-12-31', '2025Q4', 'MNOK', '2026-01-20T00:00:00Z', '2026-01-20T00:00:00Z'),
                  ('metric-2', @WorkspaceId, @CompanyId, 'Revenue', 140, '2026-03-31', '2026Q1', 'MNOK', '2026-03-31T00:00:00Z', '2026-03-31T00:00:00Z'),
                  ('metric-3', @WorkspaceId, @CompanyId, 'EBITDA', 30, '2025-12-31', '2025Q4', 'MNOK', '2026-01-20T00:00:00Z', '2026-01-20T00:00:00Z')",
                new { WorkspaceId = workspaceId, CompanyId = companyId });

            await connection.ExecuteAsync(
                @"INSERT INTO scenario (scenario_id, workspace_id, company_id, name, probability, assumptions, created_at, updated_at)
                  VALUES
                  ('scenario-base', @WorkspaceId, @CompanyId, 'Base', 0.6, 'Steady execution', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z')",
                new { WorkspaceId = workspaceId, CompanyId = companyId });

            await connection.ExecuteAsync(
                @"INSERT INTO scenario_kpi (scenario_kpi_id, workspace_id, scenario_id, kpi_name, period, value, unit, created_at)
                  VALUES
                  ('kpi-1', @WorkspaceId, 'scenario-base', 'ARR', '2026Q1', 200, 'MNOK', '2026-02-01T00:00:00Z')",
                new { WorkspaceId = workspaceId });

            await connection.ExecuteAsync(
                @"INSERT INTO catalyst (catalyst_id, workspace_id, company_id, title, expected_start, status, impact, created_at, updated_at)
                  VALUES
                  ('cat-open', @WorkspaceId, @CompanyId, 'New product launch', '2026-02-15', 'open', 'high', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z'),
                  ('cat-done', @WorkspaceId, @CompanyId, 'Pricing update', '2026-01-15', 'done', 'med', '2026-01-01T00:00:00Z', '2026-01-20T00:00:00Z')",
                new { WorkspaceId = workspaceId, CompanyId = companyId });

            await connection.ExecuteAsync(
                @"INSERT INTO document (id, workspace_id, company_id, title, created_at, updated_at)
                  VALUES
                  ('doc-quarter', @WorkspaceId, @CompanyId, 'Q1 Report', '2026-02-10T00:00:00Z', '2026-02-10T00:00:00Z')",
                new { WorkspaceId = workspaceId, CompanyId = companyId });

            await connection.ExecuteAsync(
                @"INSERT INTO snippet (id, workspace_id, company_id, document_id, quote_text, locator, created_at)
                  VALUES
                  ('snippet-quarter', @WorkspaceId, @CompanyId, 'doc-quarter', 'Quote', 'p.1', '2026-02-11T00:00:00Z')",
                new { WorkspaceId = workspaceId, CompanyId = companyId });

            var ftsSyncService = new CapturingFtsSyncService();
            var service = new ReviewService(settingsService, ftsSyncService);

            var result = await service.GenerateQuarterlyCompanyReviewAsync(workspaceId, companyId, "2026Q1");

            var generated = await connection.QuerySingleAsync<GeneratedNoteRow>(
                "SELECT title AS Title, content AS Content, note_type AS NoteType FROM note WHERE id = @Id",
                new { Id = result.NoteId });

            Assert.Equal("log", generated.NoteType);
            Assert.Equal("Quarterly Review ReviewCo 2026Q1", generated.Title);
            Assert.Contains("## Latest thesis", generated.Content, StringComparison.Ordinal);
            Assert.Contains("## Journal entries since last quarter", generated.Content, StringComparison.Ordinal);
            Assert.Contains("journal_entry:journal-1", generated.Content, StringComparison.Ordinal);
            Assert.Contains("## Metrics table (top metrics, last 4 periods)", generated.Content, StringComparison.Ordinal);
            Assert.Contains("Revenue", generated.Content, StringComparison.Ordinal);
            Assert.Contains("## Scenario probabilities + key KPIs", generated.Content, StringComparison.Ordinal);
            Assert.Contains("scenario:scenario-base", generated.Content, StringComparison.Ordinal);
            Assert.Contains("scenario_kpi:kpi-1", generated.Content, StringComparison.Ordinal);
            Assert.Contains("## Catalysts status summary", generated.Content, StringComparison.Ordinal);
            Assert.Contains("Open: 1, Done: 1", generated.Content, StringComparison.Ordinal);
            Assert.Contains("## New evidence/documents since last quarter", generated.Content, StringComparison.Ordinal);
            Assert.Contains("Documents added: 1", generated.Content, StringComparison.Ordinal);
            Assert.Contains("Evidence snippets added: 1", generated.Content, StringComparison.Ordinal);
            Assert.Equal(result.NoteId, ftsSyncService.LastNoteId);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static async Task SeedDataAsync(SqliteConnection connection, string workspaceId, string companyId)
    {
        await connection.ExecuteAsync(
            @"INSERT INTO document (id, workspace_id, company_id, title, created_at, updated_at)
              VALUES
              ('doc-recent', @WorkspaceId, @CompanyId, 'Recent filing', '2026-04-12T10:00:00Z', '2026-04-12T10:00:00Z'),
              ('doc-old', @WorkspaceId, @CompanyId, 'Old filing', '2026-03-20T10:00:00Z', '2026-03-20T10:00:00Z')",
            new { WorkspaceId = workspaceId, CompanyId = companyId });

        await connection.ExecuteAsync(
            @"INSERT INTO note (id, workspace_id, company_id, title, content, note_type, created_at, updated_at)
              VALUES
              ('note-recent', @WorkspaceId, @CompanyId, 'Recent note', 'content', 'manual', '2026-04-12T10:00:00Z', '2026-04-12T10:00:00Z'),
              ('note-old', @WorkspaceId, @CompanyId, 'Old note', 'content', 'manual', '2026-03-01T10:00:00Z', '2026-03-01T10:00:00Z')",
            new { WorkspaceId = workspaceId, CompanyId = companyId });

        await connection.ExecuteAsync(
            @"INSERT INTO agent (id, workspace_id, name, created_at, updated_at)
              VALUES ('agent-1', @WorkspaceId, 'Weekly Agent', '2026-04-01T00:00:00Z', '2026-04-01T00:00:00Z')",
            new { WorkspaceId = workspaceId });

        await connection.ExecuteAsync(
            @"INSERT INTO agent_run (id, agent_id, workspace_id, company_id, status, started_at, finished_at)
              VALUES
              ('run-recent', 'agent-1', @WorkspaceId, @CompanyId, 'succeeded', '2026-04-12T10:00:00Z', '2026-04-12T10:10:00Z'),
              ('run-old', 'agent-1', @WorkspaceId, @CompanyId, 'succeeded', '2026-03-25T10:00:00Z', '2026-03-25T10:10:00Z')",
            new { WorkspaceId = workspaceId, CompanyId = companyId });

        await connection.ExecuteAsync(
            @"INSERT INTO artifact (id, workspace_id, agent_run_id, artifact_type, title, content, created_at, updated_at)
              VALUES
              ('artifact-recent', @WorkspaceId, 'run-recent', 'summary', 'Recent artifact', '...', '2026-04-12T10:11:00Z', '2026-04-12T10:11:00Z'),
              ('artifact-old', @WorkspaceId, 'run-old', 'summary', 'Old artifact', '...', '2026-03-25T10:11:00Z', '2026-03-25T10:11:00Z')",
            new { WorkspaceId = workspaceId });

        await connection.ExecuteAsync(
            @"INSERT INTO event (id, workspace_id, company_id, event_type, title, occurred_at, created_at)
              VALUES
              ('event-soon', @WorkspaceId, @CompanyId, 'earnings', 'Q1 earnings', '2026-04-21', '2026-04-12T00:00:00Z'),
              ('event-old', @WorkspaceId, @CompanyId, 'earnings', 'Old event', '2026-03-01', '2026-03-01T00:00:00Z')",
            new { WorkspaceId = workspaceId, CompanyId = companyId });

        await connection.ExecuteAsync(
            @"INSERT INTO catalyst (catalyst_id, workspace_id, company_id, title, expected_start, status, impact, created_at, updated_at)
              VALUES
              ('cat-soon', @WorkspaceId, @CompanyId, 'Catalyst next week', '2026-04-20', 'open', 'high', '2026-04-12T00:00:00Z', '2026-04-12T00:00:00Z'),
              ('cat-later', @WorkspaceId, @CompanyId, 'Catalyst in two months', '2026-06-20', 'open', 'med', '2026-04-12T00:00:00Z', '2026-04-12T00:00:00Z')",
            new { WorkspaceId = workspaceId, CompanyId = companyId });

        await connection.ExecuteAsync(
            @"INSERT INTO trade (trade_id, workspace_id, company_id, trade_date, side, quantity, price, currency, created_at)
              VALUES
              ('trade-recent', @WorkspaceId, @CompanyId, '2026-04-13', 'buy', 10, 100, 'NOK', '2026-04-13T00:00:00Z'),
              ('trade-old', @WorkspaceId, @CompanyId, '2026-03-30', 'sell', 5, 110, 'NOK', '2026-03-30T00:00:00Z')",
            new { WorkspaceId = workspaceId, CompanyId = companyId });
    }

    private static SqliteConnection OpenConnection(string databasePath)
        => new(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString());

    private sealed class TestAppSettingsService(string rootDirectory) : IAppSettingsService
    {
        private readonly AppSettings _settings = new()
        {
            DatabaseDirectory = Path.Combine(rootDirectory, "db"),
            VaultStorageDirectory = Path.Combine(rootDirectory, "vault")
        };

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(_settings.DatabaseDirectory);
            Directory.CreateDirectory(_settings.VaultStorageDirectory);
            return Task.FromResult(_settings);
        }

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class GeneratedNoteRow
    {
        public string Title { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string NoteType { get; init; } = string.Empty;
    }

    private sealed class CapturingFtsSyncService : IFtsSyncService
    {
        public string LastNoteId { get; private set; } = string.Empty;

        public Task DeleteArtifactAsync(string artifactId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteNoteAsync(string noteId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertArtifactAsync(string artifactId, string content, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertDocumentAsync(string documentId, string title, string content, string? companyName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpsertNoteAsync(string noteId, string title, string content, CancellationToken cancellationToken = default)
        {
            LastNoteId = noteId;
            return Task.CompletedTask;
        }
    }
}
