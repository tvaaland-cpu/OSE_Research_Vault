using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class ExportServiceTests
{
    [Fact]
    public async Task ExportCompanyResearchPack_WritesCoreFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var settings = await settingsService.GetSettingsAsync();
            var sourceDocPath = Path.Combine(tempRoot, "sample.txt");
            await File.WriteAllTextAsync(sourceDocPath, "sample document");

            await using (var connection = new SqliteConnection($"Data Source={settings.DatabaseFilePath}"))
            {
                await connection.OpenAsync();
                var workspaceId = await connection.ExecuteScalarAsync<string>("SELECT id FROM workspace LIMIT 1");
                var now = DateTime.UtcNow.ToString("O");

                await connection.ExecuteAsync("INSERT INTO company (id, workspace_id, name, ticker, isin, created_at, updated_at) VALUES (@Id,@WorkspaceId,@Name,@Ticker,@Isin,@Now,@Now)",
                    new { Id = "comp-1", WorkspaceId = workspaceId, Name = "Acme", Ticker = "ACM", Isin = "US0000000001", Now = now });
                await connection.ExecuteAsync("INSERT INTO note (id, workspace_id, company_id, title, content, note_type, created_at, updated_at) VALUES (@Id,@WorkspaceId,@CompanyId,@Title,@Content,@Type,@Now,@Now)",
                    new { Id = "note-1", WorkspaceId = workspaceId, CompanyId = "comp-1", Title = "Thesis", Content = "Long", Type = "thesis", Now = now });
                await connection.ExecuteAsync("INSERT INTO document (id, workspace_id, company_id, title, doc_type, file_path, content_hash, imported_at, created_at, updated_at, is_archived) VALUES (@Id,@WorkspaceId,@CompanyId,@Title,@Type,@Path,@Hash,@Now,@Now,@Now,0)",
                    new { Id = "doc-1", WorkspaceId = workspaceId, CompanyId = "comp-1", Title = "10-Q", Type = "filing", Path = sourceDocPath, Hash = "hash-a", Now = now });
                await connection.ExecuteAsync("INSERT INTO snippet (id, workspace_id, company_id, document_id, quote_text, context, created_at) VALUES (@Id,@WorkspaceId,@CompanyId,@DocumentId,@Quote,@Context,@Now)",
                    new { Id = "snip-1", WorkspaceId = workspaceId, CompanyId = "comp-1", DocumentId = "doc-1", Quote = "Revenue up", Context = "p.3", Now = now });
                await connection.ExecuteAsync("INSERT INTO metric (id, workspace_id, company_id, metric_key, metric_value, unit, period_start, period_end, recorded_at, snippet_id, created_at) VALUES (@Id,@WorkspaceId,@CompanyId,@Key,@Value,@Unit,@Start,@End,@Now,@SnippetId,@Now)",
                    new { Id = "met-1", WorkspaceId = workspaceId, CompanyId = "comp-1", Key = "Revenue", Value = 100.5, Unit = "USDm", Start = "2025-01-01", End = "2025-03-31", Now = now, SnippetId = "snip-1" });
            }

            var service = new SqliteExportService(settingsService, new RegexRedactionService());
            var outputFolder = Path.Combine(tempRoot, "out");
            await service.ExportCompanyResearchPackAsync(string.Empty, "comp-1", outputFolder);

            Assert.True(File.Exists(Path.Combine(outputFolder, "index.md")));
            Assert.True(File.Exists(Path.Combine(outputFolder, "metrics.csv")));
            Assert.True(File.Exists(Path.Combine(outputFolder, "notes.md")));
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
