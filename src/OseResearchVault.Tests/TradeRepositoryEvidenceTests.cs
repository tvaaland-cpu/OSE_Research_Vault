using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Repositories;
using OseResearchVault.Data.Services;
using Dapper;
using Microsoft.Data.Sqlite;

namespace OseResearchVault.Tests;

public sealed class TradeRepositoryEvidenceTests
{
    [Fact]
    public async Task CreateTradeAsync_PersistsSourceReference()
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

            var companyId = await companyService.CreateCompanyAsync(new CompanyUpsertRequest { Name = "Atea" }, []);
            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = settings.DatabaseFilePath, ForeignKeys = true, Pooling = false }.ToString());
            await connection.OpenAsync();
            var workspaceId = await connection.QuerySingleAsync<string>("SELECT id FROM workspace LIMIT 1");
            var sourceId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow.ToString("O");

            await connection.ExecuteAsync(
                @"INSERT INTO source (id, workspace_id, company_id, name, source_type, created_at, updated_at)
                  VALUES (@Id, @WorkspaceId, @CompanyId, @Name, @SourceType, @Now, @Now)",
                new
                {
                    Id = sourceId,
                    WorkspaceId = workspaceId,
                    CompanyId = companyId,
                    Name = "Broker Statement",
                    SourceType = "statement",
                    Now = now
                });

            await tradeRepository.CreateTradeAsync(new CreateTradeRequest
            {
                WorkspaceId = workspaceId,
                CompanyId = companyId,
                TradeDate = "2025-01-15",
                Side = "buy",
                Quantity = 10,
                Price = 100,
                Fee = 1,
                Currency = "NOK",
                SourceId = sourceId,
                Note = "Broker statement"
            });

            var trades = await tradeRepository.ListTradesAsync(workspaceId, companyId);
            var trade = Assert.Single(trades);
            Assert.Equal(sourceId, trade.SourceId);
        }
        finally
        {
            TestCleanup.DeleteDirectory(tempRoot);
        }
    }

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
