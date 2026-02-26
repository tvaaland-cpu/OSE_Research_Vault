using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class RetrievalRegressionTests
{
    [Fact]
    public async Task RetrieveAsync_QueryReturnsExpectedTopResultIds()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var settingsService = new RegressionTestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = settings.DatabaseFilePath,
                ForeignKeys = true
            }.ToString());
            await connection.OpenAsync();

            var workspaceId = await connection.QuerySingleAsync<string>("SELECT id FROM workspace LIMIT 1");
            var companyId = Guid.NewGuid().ToString();
            var now = "2024-05-01T00:00:00.0000000Z";

            await connection.ExecuteAsync("INSERT INTO company(id, workspace_id, name, created_at, updated_at) VALUES (@Id, @WorkspaceId, @Name, @Now, @Now)",
                new { Id = companyId, WorkspaceId = workspaceId, Name = "Acme", Now = now });

            var noteId = "00000000-0000-0000-0000-000000000101";
            await connection.ExecuteAsync("INSERT INTO note(id, workspace_id, company_id, title, content, created_at, updated_at) VALUES (@Id, @WorkspaceId, @CompanyId, @Title, @Content, @Now, @Now)",
                new { Id = noteId, WorkspaceId = workspaceId, CompanyId = companyId, Title = "Margin note", Content = "Acme margin trend strong margin trend signal", Now = now });
            await connection.ExecuteAsync("INSERT INTO note_fts(id, title, body) VALUES (@Id, @Title, @Body)",
                new { Id = noteId, Title = "Margin note", Body = "Acme margin trend strong margin trend signal" });

            var docId = "00000000-0000-0000-0000-000000000102";
            await connection.ExecuteAsync("INSERT INTO document(id, workspace_id, company_id, title, created_at, updated_at) VALUES (@Id, @WorkspaceId, @CompanyId, @Title, @Now, @Now)",
                new { Id = docId, WorkspaceId = workspaceId, CompanyId = companyId, Title = "Earnings transcript", Now = now });
            await connection.ExecuteAsync("INSERT INTO document_text(id, workspace_id, document_id, chunk_index, content, created_at, updated_at) VALUES (@Id, @WorkspaceId, @DocumentId, 0, @Content, @Now, @Now)",
                new { Id = Guid.NewGuid().ToString(), WorkspaceId = workspaceId, DocumentId = docId, Content = "margin trend discussed in prepared remarks", Now = now });
            await connection.ExecuteAsync("INSERT INTO document_text_fts(id, title, content) VALUES (@Id, @Title, @Content)",
                new { Id = docId, Title = "Earnings transcript", Content = "margin trend discussed in prepared remarks" });

            var retrievalService = new SqliteRetrievalService(settingsService, NullLogger<SqliteRetrievalService>.Instance);
            var context = await retrievalService.RetrieveAsync(workspaceId, "margin trend", companyId, limitPerType: 1, maxTotalChars: 4000);

            var topCitations = context.Items.Select(x => x.CitationLabel).ToList();
            Assert.Equal(new[]
            {
                $"[NOTE:{noteId}|chunk:0]",
                $"[DOC:{docId}|chunk:0]"
            }, topCitations);
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

    private static void Cleanup(string tempRoot)
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}

public sealed class PromptBuilderRegressionTests
{
    [Fact]
    public void BuildAskVaultPrompt_MatchesStoredSnapshot()
    {
        var builder = new AskVaultPromptBuilder();
        var context = new ContextPack
        {
            Query = "Summarize margin trends.",
            Items =
            [
                new ContextPackItem
                {
                    ItemType = "doc",
                    CitationLabel = "[DOC:doc-1|chunk:0]",
                    SourceRef = "doc-1",
                    SourceDescription = "Q4 Transcript",
                    TextExcerpt = "Management reported gross margin expansion to 38% in Q4.",
                    Content = "Management reported gross margin expansion to 38% in Q4.",
                    Locator = "chunk:0",
                    Title = "Q4 Transcript"
                },
                new ContextPackItem
                {
                    ItemType = "snippet",
                    CitationLabel = "[SNIP:snip-1]",
                    SourceRef = "snip-1",
                    SourceDescription = "Analyst snippet",
                    TextExcerpt = "Channel checks support sustained pricing power.",
                    Content = "Channel checks support sustained pricing power.",
                    Locator = "snippet",
                    Title = "Analyst snippet"
                }
            ]
        };

        var prompt = builder.BuildAskVaultPrompt("Summarize margin trends.", "Acme Corp", context, new AskVaultStyleOptions
        {
            PreferBulletedAnswer = true,
            IncludeGapsSection = true
        });

        var snapshotPath = Path.Combine(AppContext.BaseDirectory, "TestData", "Regression", "expected-askvault-prompt.txt");
        var expected = File.ReadAllText(snapshotPath).TrimEnd();

        Assert.Equal(expected, prompt);
    }
}

public sealed class CitationParsingTests
{
    [Fact]
    public async Task ExecuteAskMyVaultAsync_CannedModelOutputParsesCitationLinks()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new RegressionTestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ftsSyncService = new SqliteFtsSyncService(settingsService);
            var docService = new SqliteDocumentImportService(settingsService, ftsSyncService, NullLogger<SqliteDocumentImportService>.Instance);
            var providerFactory = new LlmProviderFactory([new CannedResponseLlmProvider()]);
            var agentService = new SqliteAgentService(settingsService, providerFactory);

            var inputDirectory = Path.Combine(tempRoot, "inputs");
            Directory.CreateDirectory(inputDirectory);
            var txtPath = Path.Combine(inputDirectory, "evidence.txt");
            await File.WriteAllTextAsync(txtPath, "margin trend improved in q4 based on management call");
            await docService.ImportFilesAsync([txtPath]);
            var doc = (await docService.GetDocumentsAsync()).Single();

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = settings.DatabaseFilePath, ForeignKeys = true }.ToString());
            await connection.OpenAsync();

            var workspaceId = await connection.QuerySingleAsync<string>("SELECT id FROM workspace LIMIT 1");
            var snippetId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow.ToString("O");
            await connection.ExecuteAsync(
                "INSERT INTO snippet(id, workspace_id, document_id, quote_text, context, locator, created_at, updated_at) VALUES (@Id, @WorkspaceId, @DocumentId, @QuoteText, @Context, @Locator, @Now, @Now)",
                new { Id = snippetId, WorkspaceId = workspaceId, DocumentId = doc.Id, QuoteText = "pricing remained firm", Context = "channel checks", Locator = "p.4", Now = now });

            var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "Regression", "canned-model-output.txt");
            var template = await File.ReadAllTextAsync(templatePath);
            CannedResponseLlmProvider.ResponseText = template.Replace("{DOC_ID}", doc.Id, StringComparison.Ordinal).Replace("{SNIP_ID}", snippetId, StringComparison.Ordinal);

            var result = await agentService.ExecuteAskMyVaultAsync(new AskMyVaultRequest
            {
                Query = "What changed in margins?",
                SelectedDocumentIds = [doc.Id]
            });

            Assert.True(result.CitationsDetected);

            var artifactId = await connection.QuerySingleAsync<string>("SELECT id FROM artifact WHERE agent_run_id = @RunId", new { RunId = result.RunId });
            var links = (await connection.QueryAsync<(string ToEntityType, string ToEntityId)>(
                "SELECT to_entity_type as ToEntityType, to_entity_id as ToEntityId FROM evidence_link WHERE from_entity_type = 'artifact' AND from_entity_id = @ArtifactId ORDER BY to_entity_type, to_entity_id",
                new { ArtifactId = artifactId })).ToList();

            Assert.Contains(links, x => x.ToEntityType == "document" && x.ToEntityId == doc.Id);
            Assert.Contains(links, x => x.ToEntityType == "snippet" && x.ToEntityId == snippetId);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}

internal sealed class RegressionTestAppSettingsService(string rootDirectory) : IAppSettingsService
{
    private readonly AppSettings _settings = new()
    {
        WorkspaceRoot = Path.Combine(rootDirectory, "workspace"),
        DatabaseFilePath = Path.Combine(rootDirectory, "vault.db"),
        BackupDirectory = Path.Combine(rootDirectory, "backup"),
        FileInboxDirectory = Path.Combine(rootDirectory, "inbox")
    };

    public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_settings);

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal sealed class CannedResponseLlmProvider : ILLMProvider
{
    public static string ResponseText { get; set; } = string.Empty;

    public string ProviderName => "local";

    public Task<string> GenerateAsync(string prompt, string contextDocsText, LlmGenerationSettings settings, CancellationToken cancellationToken = default)
        => Task.FromResult(ResponseText);
}
