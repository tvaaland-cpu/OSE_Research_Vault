using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class AgentRunFlowTests
{
    [Fact]
    public async Task CanCreateTemplateRunAndPersistAuditableArtifacts()
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
            var companyService = new SqliteCompanyService(settingsService);
            var providerFactory = new LlmProviderFactory([new LocalEchoLlmProvider()]);
            var agentService = new SqliteAgentService(settingsService, providerFactory);

            var companyId = await companyService.CreateCompanyAsync(new CompanyUpsertRequest { Name = "ACME" }, []);

            var inputDirectory = Path.Combine(tempRoot, "inputs");
            Directory.CreateDirectory(inputDirectory);
            var txtPath = Path.Combine(inputDirectory, "memo.txt");
            await File.WriteAllTextAsync(txtPath, "internal memo changed guidance");
            var importResults = await docService.ImportFilesAsync([txtPath]);
            Assert.True(importResults[0].Succeeded);
            var doc = (await docService.GetDocumentsAsync()).Single();

            var agentId = await agentService.CreateAgentAsync(new AgentTemplateUpsertRequest
            {
                Name = "MVP Analyst",
                Goal = "Summarize",
                Instructions = "Use provided docs",
                AllowedToolsJson = "[\"local_docs\"]",
                OutputSchema = "text",
                EvidencePolicy = "strict"
            });

            var runId = await agentService.CreateRunAsync(new AgentRunRequest
            {
                AgentId = agentId,
                CompanyId = companyId,
                Query = "What changed?",
                SelectedDocumentIds = [doc.Id]
            });

            var artifacts = await agentService.GetArtifactsAsync(runId);
            var artifact = Assert.Single(artifacts);
            Assert.Contains("LocalEcho", artifact.Content);

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = settings.DatabaseFilePath,
                ForeignKeys = true
            }.ToString());
            await connection.OpenAsync();

            var run = await connection.QuerySingleAsync<(string Status, string ModelProvider, string ModelName, string ModelParametersJson)>(
                "SELECT status, model_provider as ModelProvider, model_name as ModelName, model_parameters_json as ModelParametersJson FROM agent_run WHERE id = @Id",
                new { Id = runId });
            Assert.Equal("success", run.Status);
            Assert.Equal("local", run.ModelProvider);
            Assert.Equal("local-echo", run.ModelName);
            Assert.Contains("TopDocumentChunks", run.ModelParametersJson);

            var toolCalls = await connection.QueryAsync<(string Name, string Status)>("SELECT name, status FROM tool_call WHERE agent_run_id = @Id", new { Id = runId });
            var call = Assert.Single(toolCalls);
            Assert.Equal("local_search", call.Name);
            Assert.Equal("success", call.Status);

            var evidenceCount = await connection.QuerySingleAsync<int>("SELECT COUNT(1) FROM evidence_link WHERE from_entity_type = 'agent_run' AND from_entity_id = @Id", new { Id = runId });
            Assert.True(evidenceCount > 0);
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
