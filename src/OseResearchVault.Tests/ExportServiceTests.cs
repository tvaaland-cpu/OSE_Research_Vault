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
        var tempRoot = CreateTempRoot();

        try
        {
            var settingsService = await InitializeDatabaseAsync(tempRoot);
            var sourceDocPath = Path.Combine(tempRoot, "sample.txt");
            await File.WriteAllTextAsync(sourceDocPath, "sample document");

            await SeedBaselineCompanyAsync(settingsService, sourceDocPath);

            var service = new SqliteExportService(settingsService, new RegexRedactionService());
            var outputFolder = Path.Combine(tempRoot, "out");
            await service.ExportCompanyResearchPackAsync(string.Empty, "comp-1", outputFolder);

            Assert.True(File.Exists(Path.Combine(outputFolder, "index.md")));
            Assert.True(File.Exists(Path.Combine(outputFolder, "metrics.csv")));
            Assert.True(File.Exists(Path.Combine(outputFolder, "notes.md")));
        }
        finally
        {
            Cleanup(tempRoot);
        }
    }

    [Fact]
    public async Task ExportCompanyResearchPack_WithRedactionProfile_MasksSensitivePatterns()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var settingsService = await InitializeDatabaseAsync(tempRoot);
            var sourceDocPath = Path.Combine(tempRoot, "sample.txt");
            await File.WriteAllTextAsync(sourceDocPath, "sample document");

            await SeedBaselineCompanyAsync(settingsService, sourceDocPath);

            var settings = await settingsService.GetSettingsAsync();
            await using (var connection = new SqliteConnection($"Data Source={settings.DatabaseFilePath}"))
            {
                await connection.OpenAsync();
                var now = DateTime.UtcNow.ToString("O");
                await connection.ExecuteAsync(
                    "INSERT INTO event (id, workspace_id, company_id, event_type, title, payload_json, occurred_at, created_at) VALUES (@Id,@WorkspaceId,@CompanyId,@Type,@Title,@Payload,@OccurredAt,@Now)",
                    new
                    {
                        Id = "evt-1",
                        WorkspaceId = await connection.ExecuteScalarAsync<string>("SELECT id FROM workspace LIMIT 1"),
                        CompanyId = "comp-1",
                        Type = "memo",
                        Title = "Reach me at ceo@acme.com and +1 212 555 0199",
                        Payload = "{}",
                        OccurredAt = now,
                        Now = now
                    });
            }

            var service = new SqliteExportService(settingsService, new RegexRedactionService());
            var outputFolder = Path.Combine(tempRoot, "out-redacted");
            await service.ExportCompanyResearchPackAsync(
                string.Empty,
                "comp-1",
                outputFolder,
                new RedactionOptions
                {
                    MaskEmails = true,
                    MaskPhones = true,
                    MaskPaths = true,
                    MaskSecrets = true,
                    ExcludePrivateTaggedItems = false
                });

            var notesText = await File.ReadAllTextAsync(Path.Combine(outputFolder, "notes.md"));
            var eventsText = await File.ReadAllTextAsync(Path.Combine(outputFolder, "events.csv"));

            Assert.Contains("[REDACTED_EMAIL]", notesText);
            Assert.Contains("[REDACTED_PATH]", notesText);
            Assert.Contains("[REDACTED_EMAIL]", eventsText);
            Assert.Contains("[REDACTED_PHONE]", eventsText);
        }
        finally
        {
            Cleanup(tempRoot);
        }
    }

    [Fact]
    public async Task ExportCompanyResearchPack_ExcludePrivateTaggedItems_RemovesPrivateNotesAndDocuments()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var settingsService = await InitializeDatabaseAsync(tempRoot);
            var publicDocPath = Path.Combine(tempRoot, "public.txt");
            var privateDocPath = Path.Combine(tempRoot, "private.txt");
            await File.WriteAllTextAsync(publicDocPath, "public doc");
            await File.WriteAllTextAsync(privateDocPath, "private doc");

            var settings = await settingsService.GetSettingsAsync();
            await using (var connection = new SqliteConnection($"Data Source={settings.DatabaseFilePath}"))
            {
                await connection.OpenAsync();
                var workspaceId = await connection.ExecuteScalarAsync<string>("SELECT id FROM workspace LIMIT 1");
                var now = DateTime.UtcNow.ToString("O");

                await connection.ExecuteAsync("INSERT INTO company (id, workspace_id, name, ticker, isin, created_at, updated_at) VALUES (@Id,@WorkspaceId,@Name,@Ticker,@Isin,@Now,@Now)",
                    new { Id = "comp-1", WorkspaceId = workspaceId, Name = "Acme", Ticker = "ACM", Isin = "US0000000001", Now = now });
                await connection.ExecuteAsync("INSERT INTO note (id, workspace_id, company_id, title, content, note_type, created_at, updated_at) VALUES (@Id,@WorkspaceId,@CompanyId,@Title,@Content,@Type,@Now,@Now)",
                    new { Id = "note-public", WorkspaceId = workspaceId, CompanyId = "comp-1", Title = "Public note", Content = "visible", Type = "thesis", Now = now });
                await connection.ExecuteAsync("INSERT INTO note (id, workspace_id, company_id, title, content, note_type, created_at, updated_at) VALUES (@Id,@WorkspaceId,@CompanyId,@Title,@Content,@Type,@Now,@Now)",
                    new { Id = "note-private", WorkspaceId = workspaceId, CompanyId = "comp-1", Title = "Private note", Content = "hidden", Type = "thesis", Now = now });

                await connection.ExecuteAsync("INSERT INTO document (id, workspace_id, company_id, title, doc_type, file_path, content_hash, imported_at, created_at, updated_at, is_archived) VALUES (@Id,@WorkspaceId,@CompanyId,@Title,@Type,@Path,@Hash,@Now,@Now,@Now,0)",
                    new { Id = "doc-public", WorkspaceId = workspaceId, CompanyId = "comp-1", Title = "Public doc", Type = "filing", Path = publicDocPath, Hash = "hash-public", Now = now });
                await connection.ExecuteAsync("INSERT INTO document (id, workspace_id, company_id, title, doc_type, file_path, content_hash, imported_at, created_at, updated_at, is_archived) VALUES (@Id,@WorkspaceId,@CompanyId,@Title,@Type,@Path,@Hash,@Now,@Now,@Now,0)",
                    new { Id = "doc-private", WorkspaceId = workspaceId, CompanyId = "comp-1", Title = "Private doc", Type = "filing", Path = privateDocPath, Hash = "hash-private", Now = now });

                await connection.ExecuteAsync("INSERT INTO tag (id, workspace_id, name, created_at, updated_at) VALUES (@Id,@WorkspaceId,@Name,@Now,@Now)",
                    new { Id = "tag-private", WorkspaceId = workspaceId, Name = "private", Now = now });

                await connection.ExecuteAsync("INSERT INTO note_tag (note_id, tag_id) VALUES (@NoteId,@TagId)", new { NoteId = "note-private", TagId = "tag-private" });
                await connection.ExecuteAsync("INSERT INTO document_tag (document_id, tag_id) VALUES (@DocumentId,@TagId)", new { DocumentId = "doc-private", TagId = "tag-private" });
            }

            var service = new SqliteExportService(settingsService, new RegexRedactionService());
            var outputFolder = Path.Combine(tempRoot, "out-private");
            await service.ExportCompanyResearchPackAsync(
                string.Empty,
                "comp-1",
                outputFolder,
                new RedactionOptions
                {
                    MaskEmails = false,
                    MaskPhones = false,
                    MaskPaths = false,
                    MaskSecrets = false,
                    ExcludePrivateTaggedItems = true
                });

            var notesText = await File.ReadAllTextAsync(Path.Combine(outputFolder, "notes.md"));
            var exportedDocuments = Directory.GetFiles(Path.Combine(outputFolder, "documents"), "*.txt");

            Assert.Contains("Public note", notesText);
            Assert.DoesNotContain("Private note", notesText);
            Assert.Contains(exportedDocuments, f => Path.GetFileName(f).Contains("Public doc", StringComparison.Ordinal));
            Assert.DoesNotContain(exportedDocuments, f => Path.GetFileName(f).Contains("Private doc", StringComparison.Ordinal));
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

    private static async Task SeedBaselineCompanyAsync(TestAppSettingsService settingsService, string sourceDocPath)
    {
        var settings = await settingsService.GetSettingsAsync();
        await using var connection = new SqliteConnection($"Data Source={settings.DatabaseFilePath}");
        await connection.OpenAsync();
        var workspaceId = await connection.ExecuteScalarAsync<string>("SELECT id FROM workspace LIMIT 1");
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync("INSERT INTO company (id, workspace_id, name, ticker, isin, created_at, updated_at) VALUES (@Id,@WorkspaceId,@Name,@Ticker,@Isin,@Now,@Now)",
            new { Id = "comp-1", WorkspaceId = workspaceId, Name = "Acme", Ticker = "ACM", Isin = "US0000000001", Now = now });
        await connection.ExecuteAsync("INSERT INTO note (id, workspace_id, company_id, title, content, note_type, created_at, updated_at) VALUES (@Id,@WorkspaceId,@CompanyId,@Title,@Content,@Type,@Now,@Now)",
            new { Id = "note-1", WorkspaceId = workspaceId, CompanyId = "comp-1", Title = "Thesis", Content = "Contact ceo@acme.com in C:\\Vault", Type = "thesis", Now = now });
        await connection.ExecuteAsync("INSERT INTO document (id, workspace_id, company_id, title, doc_type, file_path, content_hash, imported_at, created_at, updated_at, is_archived) VALUES (@Id,@WorkspaceId,@CompanyId,@Title,@Type,@Path,@Hash,@Now,@Now,@Now,0)",
            new { Id = "doc-1", WorkspaceId = workspaceId, CompanyId = "comp-1", Title = "10-Q", Type = "filing", Path = sourceDocPath, Hash = "hash-a", Now = now });
        await connection.ExecuteAsync("INSERT INTO snippet (id, workspace_id, company_id, document_id, quote_text, context, created_at) VALUES (@Id,@WorkspaceId,@CompanyId,@DocumentId,@Quote,@Context,@Now)",
            new { Id = "snip-1", WorkspaceId = workspaceId, CompanyId = "comp-1", DocumentId = "doc-1", Quote = "Revenue up", Context = "p.3", Now = now });
        await connection.ExecuteAsync("INSERT INTO metric (id, workspace_id, company_id, metric_key, metric_value, unit, period_start, period_end, recorded_at, snippet_id, created_at) VALUES (@Id,@WorkspaceId,@CompanyId,@Key,@Value,@Unit,@Start,@End,@Now,@SnippetId,@Now)",
            new { Id = "met-1", WorkspaceId = workspaceId, CompanyId = "comp-1", Key = "Revenue", Value = 100.5, Unit = "USDm", Start = "2025-01-01", End = "2025-03-31", Now = now, SnippetId = "snip-1" });
    }

    private static void Cleanup(string tempRoot)
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
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
