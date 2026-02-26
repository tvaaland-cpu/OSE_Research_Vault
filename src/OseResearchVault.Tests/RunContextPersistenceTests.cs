using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class RunContextPersistenceTests
{
    [Fact]
    public async Task CreateRunAsync_StoresRunContextAndPrompt()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ftsSyncService = new SqliteFtsSyncService(settingsService);
            var docService = new SqliteDocumentImportService(settingsService, ftsSyncService, NullLogger<SqliteDocumentImportService>.Instance);
            var providerFactory = new LlmProviderFactory([new LocalEchoLlmProvider()]);
            var agentService = new SqliteAgentService(settingsService, providerFactory);

            var inputDirectory = Path.Combine(tempRoot, "inputs");
            Directory.CreateDirectory(inputDirectory);
            var txtPath = Path.Combine(inputDirectory, "memo.txt");
            await File.WriteAllTextAsync(txtPath, "internal memo changed guidance");
            await docService.ImportFilesAsync([txtPath]);
            var doc = (await docService.GetDocumentsAsync()).Single();

            var agentId = await agentService.CreateAgentAsync(new AgentTemplateUpsertRequest { Name = "MVP Analyst", Instructions = "Use docs" });
            var runId = await agentService.CreateRunAsync(new AgentRunRequest
            {
                AgentId = agentId,
                Query = "What changed?",
                SelectedDocumentIds = [doc.Id]
            });

            var runContext = await agentService.GetRunContextAsync(runId);
            Assert.NotNull(runContext);
            Assert.False(string.IsNullOrWhiteSpace(runContext!.PromptText));
            Assert.Contains("User query: What changed?", runContext.PromptText);

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = settings.DatabaseFilePath, ForeignKeys = true }.ToString());
            await connection.OpenAsync();

            var row = await connection.QuerySingleAsync<(string ContextJson, string PromptText)>("SELECT context_json AS ContextJson, prompt_text AS PromptText FROM run_context WHERE run_id = @RunId", new { RunId = runId });
            Assert.False(string.IsNullOrWhiteSpace(row.ContextJson));
            Assert.Equal(runContext.PromptText, row.PromptText);
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
            VaultStorageDirectory = Path.Combine(rootDirectory, "vault"),
            DefaultLlmSettings = new LlmGenerationSettings
            {
                Provider = "local",
                Model = "local-echo",
                TopDocumentChunks = 3
            }
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
