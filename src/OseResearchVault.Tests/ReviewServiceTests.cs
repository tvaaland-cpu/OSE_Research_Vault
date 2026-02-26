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
