using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class CompanyManagementFlowTests
{
    [Fact]
    public async Task CanCreateCompanyAndLinkDocumentAndNote()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var companyService = new SqliteCompanyService(settingsService);
            var documentService = new SqliteDocumentImportService(settingsService, NullLogger<SqliteDocumentImportService>.Instance);
            var ftsSyncService = new SqliteFtsSyncService(settingsService);
            var noteService = new SqliteNoteService(settingsService, ftsSyncService);

            var tagId = await companyService.CreateTagAsync("Networking");
            var companyId = await companyService.CreateCompanyAsync(
                new CompanyUpsertRequest { Name = "Napatech ASA", Ticker = "NAPA.OL", Sector = "Technology" },
                [tagId]);

            var inputDirectory = Path.Combine(tempRoot, "inputs");
            Directory.CreateDirectory(inputDirectory);
            var txtPath = Path.Combine(inputDirectory, "napa-note.txt");
            await File.WriteAllTextAsync(txtPath, "napatech update");
            var importResults = await documentService.ImportFilesAsync([txtPath]);
            Assert.Single(importResults);
            Assert.True(importResults[0].Succeeded);

            var document = (await documentService.GetDocumentsAsync()).Single();
            await documentService.UpdateDocumentCompanyAsync(document.Id, companyId);

            await noteService.CreateNoteAsync(new NoteUpsertRequest
            {
                Title = "Investment thesis",
                Content = "Edge packet processing trend",
                CompanyId = companyId
            });

            var companies = await companyService.GetCompaniesAsync();
            var company = companies.Single();
            Assert.Equal("Napatech ASA", company.Name);
            Assert.Equal("NAPA.OL", company.Ticker);
            Assert.Contains("Networking", company.TagNames);

            var companyDocs = await companyService.GetCompanyDocumentsAsync(companyId);
            Assert.Single(companyDocs);
            Assert.Equal(document.Id, companyDocs[0].Id);

            var companyNotes = await companyService.GetCompanyNotesAsync(companyId);
            Assert.Single(companyNotes);
            Assert.Equal("Investment thesis", companyNotes[0].Title);

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = settings.DatabaseFilePath,
                ForeignKeys = true
            }.ToString());
            await connection.OpenAsync();

            var hasColumns = (await connection.QueryAsync<(string name)>("SELECT name FROM pragma_table_info('company')")).Select(x => x.name).ToList();
            Assert.Contains("isin", hasColumns, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("summary", hasColumns, StringComparer.OrdinalIgnoreCase);
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
