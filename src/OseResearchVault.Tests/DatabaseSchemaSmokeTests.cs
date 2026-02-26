using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class DatabaseSchemaSmokeTests
{
    [Fact]
    public async Task InitializeAsync_CreatesExpectedTables()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);

            await initializer.InitializeAsync();

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = settings.DatabaseFilePath,
                ForeignKeys = true
            }.ToString());
            await connection.OpenAsync();

            var tableNames = (await connection.QueryAsync<string>(
                "SELECT name FROM sqlite_master WHERE type='table' OR type='virtual table'"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var expectedTables = new[]
            {
                "schema_migrations",
                "workspace", "company", "position", "watchlist_item", "source", "document", "document_text",
                "note", "snippet", "agent", "agent_run", "tool_call", "artifact", "notification", "evidence_link", "tag",
                "note", "snippet", "agent", "agent_run", "tool_call", "artifact", "evidence_link", "automation", "automation_run", "tag",
                "note_tag", "snippet_tag", "artifact_tag", "document_tag", "company_tag", "event", "metric", "trade",
                "automation", "automation_run",
                "price_daily", "thesis_version", "scenario", "scenario_kpi", "journal_entry", "journal_trade", "journal_snippet", "note_fts", "snippet_fts", "artifact_fts", "document_text_fts"
            };

            foreach (var expectedTable in expectedTables)
            {
                Assert.Contains(expectedTable, tableNames);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class TestAppSettingsService(string rootDirectory) : IAppSettingsService
    {
        private readonly AppSettings _settings = new()
        {
            DatabaseDirectory = rootDirectory,
            VaultStorageDirectory = rootDirectory
        };

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
