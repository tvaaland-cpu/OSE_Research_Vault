using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Repositories;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class CompanyJournalServiceTests
{
    [Fact]
    public async Task CreateJournalEntry_WithTradeAndSnippetLinks_Works()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var companyService = new SqliteCompanyService(settingsService);
            var tradeRepository = new SqliteTradeRepository(settingsService);
            var companyId = await companyService.CreateCompanyAsync(new CompanyUpsertRequest { Name = "JournalCo" }, []);

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = OpenConnection(settings.DatabaseFilePath);
            await connection.OpenAsync();

            var workspaceId = await connection.QuerySingleAsync<string>("SELECT workspace_id FROM company WHERE id = @CompanyId", new { CompanyId = companyId });

            var trade = await tradeRepository.CreateTradeAsync(new CreateTradeRequest
            {
                WorkspaceId = workspaceId,
                CompanyId = companyId,
                TradeDate = "2026-02-15",
                Side = "buy",
                Quantity = 10,
                Price = 100,
                Currency = "NOK"
            });

            var snippetId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow.ToString("O");
            await connection.ExecuteAsync(
                @"INSERT INTO snippet (id, workspace_id, quote_text, context, created_at, updated_at)
                  VALUES (@Id, @WorkspaceId, @QuoteText, @Context, @Now, @Now)",
                new { Id = snippetId, WorkspaceId = workspaceId, QuoteText = "Management guided improving margins.", Context = "Q4 call", Now = now });

            var journalEntryId = await companyService.CreateJournalEntryAsync(
                companyId,
                new JournalEntryUpsertRequest
                {
                    Action = "buy",
                    EntryDate = "2026-02-16",
                    Rationale = "# Thesis\nValuation and catalyst alignment.",
                    ExpectedOutcome = "Rerating over next quarter.",
                    ReviewDate = "2026-04-01"
                },
                [trade.TradeId],
                [snippetId]);

            var entry = Assert.Single(await companyService.GetCompanyJournalEntriesAsync(companyId));
            Assert.Equal(journalEntryId, entry.JournalEntryId);
            Assert.Equal("buy", entry.Action);
            Assert.Contains(trade.TradeId, entry.TradeIds);
            Assert.Contains(snippetId, entry.SnippetIds);

            var tradeLinkCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(1) FROM journal_trade WHERE journal_entry_id = @JournalEntryId AND trade_id = @TradeId",
                new { JournalEntryId = journalEntryId, TradeId = trade.TradeId });

            var snippetLinkCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(1) FROM journal_snippet WHERE journal_entry_id = @JournalEntryId AND snippet_id = @SnippetId",
                new { JournalEntryId = journalEntryId, SnippetId = snippetId });

            Assert.Equal(1, tradeLinkCount);
            Assert.Equal(1, snippetLinkCount);
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
    public async Task GetCompanyJournalEntries_FiltersByCompany()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var service = new SqliteCompanyService(settingsService);
            var firstCompanyId = await service.CreateCompanyAsync(new CompanyUpsertRequest { Name = "Alpha" }, []);
            var secondCompanyId = await service.CreateCompanyAsync(new CompanyUpsertRequest { Name = "Beta" }, []);

            await service.CreateJournalEntryAsync(firstCompanyId, new JournalEntryUpsertRequest
            {
                Action = "hold",
                EntryDate = "2026-01-02",
                Rationale = "Wait for execution evidence."
            });

            await service.CreateJournalEntryAsync(secondCompanyId, new JournalEntryUpsertRequest
            {
                Action = "sell",
                EntryDate = "2026-01-03",
                Rationale = "Risk/reward deteriorated."
            });

            var firstEntries = await service.GetCompanyJournalEntriesAsync(firstCompanyId);
            Assert.Single(firstEntries);
            Assert.Equal(firstCompanyId, firstEntries[0].CompanyId);
            Assert.Equal("hold", firstEntries[0].Action);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static SqliteConnection OpenConnection(string databasePath)
        => new(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, Pooling = false }.ToString());

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
}
