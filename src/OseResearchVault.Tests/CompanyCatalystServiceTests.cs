using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class CompanyCatalystServiceTests
{
    [Fact]
    public async Task CatalystCrudAndSnippetLinking_Work()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var service = new SqliteCompanyService(settingsService);
            var companyId = await service.CreateCompanyAsync(new CompanyUpsertRequest { Name = "CatalystCo" }, []);
            var snippetId = await CreateSnippetAsync(settingsService);

            var catalystId = await service.CreateCatalystAsync(companyId, new CatalystUpsertRequest
            {
                Title = "Q2 Trading Update",
                Description = "Management update",
                ExpectedStart = "2026-05-01",
                ExpectedEnd = "2026-05-15",
                Status = "open",
                Impact = "high",
                Notes = "Need margin commentary"
            }, [snippetId]);

            var catalyst = Assert.Single(await service.GetCompanyCatalystsAsync(companyId));
            Assert.Equal(catalystId, catalyst.CatalystId);
            Assert.Equal("high", catalyst.Impact);
            Assert.Equal([snippetId], catalyst.SnippetIds);

            await service.UpdateCatalystAsync(catalystId, new CatalystUpsertRequest
            {
                Title = "Q2 Trading Update",
                Description = "Management update completed",
                ExpectedStart = "2026-05-01",
                ExpectedEnd = "2026-05-15",
                Status = "done",
                Impact = "med",
                Notes = "Reviewed and closed"
            }, []);

            var updated = Assert.Single(await service.GetCompanyCatalystsAsync(companyId));
            Assert.Equal("done", updated.Status);
            Assert.Equal("med", updated.Impact);
            Assert.Empty(updated.SnippetIds);

            await service.DeleteCatalystAsync(catalystId);
            Assert.Empty(await service.GetCompanyCatalystsAsync(companyId));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static async Task<string> CreateSnippetAsync(IAppSettingsService settingsService)
    {
        var settings = await settingsService.GetSettingsAsync();
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = settings.DatabaseFilePath, ForeignKeys = true }.ToString());
        await connection.OpenAsync();

        var workspaceId = await connection.QuerySingleAsync<string>("SELECT id FROM workspace LIMIT 1");
        var snippetId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync(
            @"INSERT INTO snippet (id, workspace_id, quote_text, context, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @QuoteText, @Context, @Now, @Now)",
            new { Id = snippetId, WorkspaceId = workspaceId, QuoteText = "Evidence for catalyst", Context = "Test context", Now = now });

        return snippetId;
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
