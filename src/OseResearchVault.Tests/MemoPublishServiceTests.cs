using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class MemoPublishServiceTests
{
    [Fact]
    public async Task PublishAsync_Markdown_CreatesFileAndDocumentRecord()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var settingsService = await InitializeDatabaseAsync(tempRoot);
            await SeedCompanyAsync(settingsService);

            var service = new SqliteMemoPublishService(settingsService, new RegexRedactionService(), new SqliteFtsSyncService(settingsService));
            var result = await service.PublishAsync(new MemoPublishRequest
            {
                NoteId = "note-1",
                NoteTitle = "Investment Memo - Acme",
                NoteContent = "Call me at ceo@acme.com\nEvidence [DOC:doc-1|chunk:2]",
                CompanyId = "comp-1",
                CompanyName = "Acme ASA",
                IncludeCitationsList = true,
                IncludeEvidenceExcerpts = true,
                RedactionOptions = new RedactionOptions { MaskEmails = true, MaskPhones = false, MaskPaths = false, MaskSecrets = false, ExcludePrivateTaggedItems = false }
            });

            Assert.True(File.Exists(result.OutputFilePath));
            var text = await File.ReadAllTextAsync(result.OutputFilePath);
            Assert.Contains("Acme_ASA_Memo_", Path.GetFileName(result.OutputFilePath), StringComparison.Ordinal);
            Assert.Contains("[REDACTED:EMAIL]", text);
            Assert.Contains("## Appendix: Citations", text);

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection($"Data Source={settings.DatabaseFilePath}");
            await connection.OpenAsync();

            var docCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM document WHERE id = @Id", new { Id = result.DocumentId });
            var sourceType = await connection.ExecuteScalarAsync<string>("SELECT source_type FROM source WHERE id = @Id", new { Id = result.SourceId });

            Assert.Equal(1, docCount);
            Assert.Equal("file", sourceType);
        }
        finally
        {
            Cleanup(tempRoot);
        }
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static async Task<TestAppSettingsService> InitializeDatabaseAsync(string tempRoot)
    {
        var settingsService = new TestAppSettingsService(tempRoot);
        var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
        await initializer.InitializeAsync();
        return settingsService;
    }

    private static async Task SeedCompanyAsync(TestAppSettingsService settingsService)
    {
        var settings = await settingsService.GetSettingsAsync();
        await using var connection = new SqliteConnection($"Data Source={settings.DatabaseFilePath}");
        await connection.OpenAsync();
        var workspaceId = await connection.ExecuteScalarAsync<string>("SELECT id FROM workspace LIMIT 1");
        var now = DateTime.UtcNow.ToString("O");
        await connection.ExecuteAsync("INSERT INTO company (id, workspace_id, name, ticker, isin, created_at, updated_at) VALUES (@Id,@WorkspaceId,@Name,@Ticker,@Isin,@Now,@Now)",
            new { Id = "comp-1", WorkspaceId = workspaceId, Name = "Acme ASA", Ticker = "ACM", Isin = "US0000000001", Now = now });
    }

    private static void Cleanup(string tempRoot)
    {
        TestCleanup.DeleteDirectory(tempRoot);
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

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
