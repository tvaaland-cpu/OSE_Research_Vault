using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class GlobalSearchTests
{
    [Fact]
    public async Task SearchAsync_FindsTier1OemAcrossNotesAndDocuments_AndFiltersByCompany()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ftsSyncService = new SqliteFtsSyncService(settingsService);
            var noteService = new SqliteNoteService(settingsService, ftsSyncService);
            var documentService = new SqliteDocumentImportService(settingsService, ftsSyncService, NullLogger<SqliteDocumentImportService>.Instance);
            var searchService = new SqliteSearchService(settingsService);

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = settings.DatabaseFilePath, ForeignKeys = true }.ToString());
            await connection.OpenAsync();

            var workspaceId = await connection.QuerySingleAsync<string>("SELECT id FROM workspace LIMIT 1");
            var companyA = Guid.NewGuid().ToString();
            var companyB = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow.ToString("O");

            await connection.ExecuteAsync("INSERT INTO company(id, workspace_id, name, created_at, updated_at) VALUES (@Id, @WorkspaceId, @Name, @Now, @Now)", new { Id = companyA, WorkspaceId = workspaceId, Name = "Alpha OEM", Now = now });
            await connection.ExecuteAsync("INSERT INTO company(id, workspace_id, name, created_at, updated_at) VALUES (@Id, @WorkspaceId, @Name, @Now, @Now)", new { Id = companyB, WorkspaceId = workspaceId, Name = "Beta Supplier", Now = now });

            await noteService.CreateNoteAsync(new NoteUpsertRequest
            {
                Title = "OEM channel note",
                Content = "This quarter, tier-1 OEM demand rebounded strongly.",
                CompanyId = companyA
            });

            var inputDir = Path.Combine(tempRoot, "inputs");
            Directory.CreateDirectory(inputDir);
            var docPath = Path.Combine(inputDir, "oem.txt");
            await File.WriteAllTextAsync(docPath, "Program update: tier-1 OEM launch timing improved.");
            var importResult = await documentService.ImportFilesAsync([docPath]);
            Assert.Single(importResult);
            Assert.True(importResult[0].Succeeded);
            var importedDoc = (await documentService.GetDocumentsAsync()).Single();
            await documentService.UpdateDocumentCompanyAsync(importedDoc.Id, companyA);

            var matches = await searchService.SearchAsync(new SearchQuery
            {
                QueryText = "tier-1 OEM",
                Type = "All"
            });

            Assert.Contains(matches, r => r.ResultType == "note");
            Assert.Contains(matches, r => r.ResultType == "document");

            var filtered = await searchService.SearchAsync(new SearchQuery
            {
                QueryText = "tier-1 OEM",
                CompanyId = companyA,
                Type = "All"
            });

            Assert.NotEmpty(filtered);
            Assert.All(filtered, r => Assert.Equal(companyA, r.CompanyId));
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
