using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Repositories;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class EvidenceServiceTests
{
    [Fact]
    public async Task CanCreateSnippetAndRetrieveByDocument()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ids = await SeedWorkspaceDataAsync(settingsService);

            var service = new EvidenceService(
                new SqliteSnippetRepository(settingsService),
                new SqliteEvidenceLinkRepository(settingsService));

            var snippet = await service.CreateSnippetAsync(
                ids.WorkspaceId,
                ids.DocumentId,
                ids.CompanyId,
                sourceId: null,
                locator: "p=12;sel=offset:123-456",
                text: "This quarter showed margin expansion.",
                createdBy: "unit-test");

            var snippets = await service.ListSnippetsByDocumentAsync(ids.DocumentId);

            Assert.Contains(snippets, s => s.Id == snippet.Id && s.Locator == "p=12;sel=offset:123-456" && s.Text.Contains("margin expansion", StringComparison.Ordinal));
        }
        finally
        {
            Cleanup(tempRoot);
        }
    }

    [Fact]
    public async Task CanCreateEvidenceLinkToSnippetAndListEvidenceByArtifact()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ids = await SeedWorkspaceDataAsync(settingsService);

            var service = new EvidenceService(
                new SqliteSnippetRepository(settingsService),
                new SqliteEvidenceLinkRepository(settingsService));

            await service.AddSnippetAndLinkToArtifactAsync(
                workspaceId: ids.WorkspaceId,
                artifactId: ids.ArtifactId,
                documentId: ids.DocumentId,
                companyId: ids.CompanyId,
                sourceId: null,
                locator: "p=3;quote=revenue grew 24%",
                text: "Revenue grew 24% year-over-year.",
                createdBy: "unit-test",
                relevanceScore: 0.91);

            var evidence = await service.ListEvidenceLinksByArtifactAsync(ids.ArtifactId);

            Assert.Single(evidence);
            Assert.Equal(ids.ArtifactId, evidence[0].ArtifactId);
            Assert.NotNull(evidence[0].SnippetId);
            Assert.Equal("Revenue grew 24% year-over-year.", evidence[0].SnippetText);
            Assert.Equal("Q1 Letter", evidence[0].DocumentTitle);
        }
        finally
        {
            Cleanup(tempRoot);
        }
    }

    [Fact]
    public async Task CreateSnippetAsync_RejectsTextShorterThanTenCharacters()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ids = await SeedWorkspaceDataAsync(settingsService);

            var service = new EvidenceService(
                new SqliteSnippetRepository(settingsService),
                new SqliteEvidenceLinkRepository(settingsService));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateSnippetAsync(
                    ids.WorkspaceId,
                    ids.DocumentId,
                    ids.CompanyId,
                    sourceId: null,
                    locator: "p=1",
                    text: "too short",
                    createdBy: "unit-test"));

            Assert.Equal("Snippet text must be at least 10 characters.", exception.Message);
        }
        finally
        {
            Cleanup(tempRoot);
        }
    }

    [Fact]
    public async Task CreateEvidenceLinkAsync_RejectsMissingSnippetAndDocumentLocator()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ids = await SeedWorkspaceDataAsync(settingsService);

            var service = new EvidenceService(
                new SqliteSnippetRepository(settingsService),
                new SqliteEvidenceLinkRepository(settingsService));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateEvidenceLinkAsync(
                    ids.ArtifactId,
                    snippetId: null,
                    documentId: null,
                    locator: null,
                    quote: "No reference",
                    relevanceScore: 0.5));

            Assert.Equal("Evidence link must include snippet_id or document_id + locator.", exception.Message);
        }
        finally
        {
            Cleanup(tempRoot);
        }
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task<(string WorkspaceId, string CompanyId, string DocumentId, string ArtifactId)> SeedWorkspaceDataAsync(IAppSettingsService settingsService)
    {
        var settings = await settingsService.GetSettingsAsync();
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = settings.DatabaseFilePath,
            ForeignKeys = true
        }.ToString());

        await connection.OpenAsync();

        var workspaceId = Guid.NewGuid().ToString();
        var companyId = Guid.NewGuid().ToString();
        var documentId = Guid.NewGuid().ToString();
        var artifactId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync("INSERT INTO workspace (id, name, created_at, updated_at) VALUES (@Id, @Name, @Now, @Now)",
            new { Id = workspaceId, Name = "Test Workspace", Now = now });

        await connection.ExecuteAsync("INSERT INTO company (id, workspace_id, name, created_at, updated_at) VALUES (@Id, @WorkspaceId, @Name, @Now, @Now)",
            new { Id = companyId, WorkspaceId = workspaceId, Name = "Acme Corp", Now = now });

        await connection.ExecuteAsync(
            @"INSERT INTO document (id, workspace_id, company_id, title, document_type, mime_type, file_path, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @CompanyId, @Title, 'report', 'text/plain', '/tmp/q1.txt', @Now, @Now)",
            new { Id = documentId, WorkspaceId = workspaceId, CompanyId = companyId, Title = "Q1 Letter", Now = now });

        await connection.ExecuteAsync(
            @"INSERT INTO artifact (id, workspace_id, artifact_type, title, content, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, 'summary', @Title, @Content, @Now, @Now)",
            new { Id = artifactId, WorkspaceId = workspaceId, Title = "Artifact", Content = "Summary", Now = now });

        return (workspaceId, companyId, documentId, artifactId);
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
