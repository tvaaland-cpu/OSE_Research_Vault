using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class CompanyScenarioServiceTests
{
    [Fact]
    public async Task ScenarioCrudAndKpiNormalization_Work()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var service = new SqliteCompanyService(settingsService);
            var companyId = await service.CreateCompanyAsync(new CompanyUpsertRequest { Name = "ScenarioCo" }, []);

            var scenarioId = await service.CreateScenarioAsync(companyId, new ScenarioUpsertRequest
            {
                Name = "Base",
                Probability = 0.6,
                Assumptions = "- moderate growth"
            });

            var scenario = Assert.Single(await service.GetCompanyScenariosAsync(companyId));
            Assert.Equal(scenarioId, scenario.ScenarioId);
            Assert.Equal("Base", scenario.Name);

            await service.UpdateScenarioAsync(scenarioId, new ScenarioUpsertRequest
            {
                Name = "Bull",
                Probability = 0.7,
                Assumptions = "Stronger margin"
            });

            var updated = Assert.Single(await service.GetCompanyScenariosAsync(companyId));
            Assert.Equal("Bull", updated.Name);
            Assert.Equal(0.7d, updated.Probability);

            var scenarioKpiId = await service.CreateScenarioKpiAsync(scenarioId, new ScenarioKpiUpsertRequest
            {
                KpiName = " Revenue Growth ",
                Period = "2026",
                Value = 12.5,
                Unit = "%",
                Currency = "NOK"
            });

            var kpi = Assert.Single(await service.GetScenarioKpisAsync(scenarioId));
            Assert.Equal(scenarioKpiId, kpi.ScenarioKpiId);
            Assert.Equal("revenue_growth", kpi.KpiName);

            await service.DeleteScenarioKpiAsync(scenarioKpiId);
            Assert.Empty(await service.GetScenarioKpisAsync(scenarioId));

            await service.DeleteScenarioAsync(scenarioId);
            Assert.Empty(await service.GetCompanyScenariosAsync(companyId));
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
