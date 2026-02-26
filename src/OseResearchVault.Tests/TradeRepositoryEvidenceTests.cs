using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Repositories;
using OseResearchVault.Data.Services;

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
            var workspaceId = (await settingsService.GetSettingsAsync()).WorkspaceId;
            var sourceId = Guid.NewGuid().ToString();

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
