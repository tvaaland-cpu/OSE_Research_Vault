using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class ModelProfileServiceTests
{
    [Fact]
    public async Task CreatingTwoDefaultsLeavesOnlyOneDefaultInWorkspace()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var service = new SqliteAgentService(settingsService, new LlmProviderFactory([new LocalEchoLlmProvider()]));

            await service.CreateModelProfileAsync(new ModelProfileUpsertRequest
            {
                Name = "GPT factual",
                Provider = "openai",
                Model = "gpt-4.1",
                ParametersJson = "{\"temperature\":0.2}",
                IsDefault = true
            });

            var secondDefaultId = await service.CreateModelProfileAsync(new ModelProfileUpsertRequest
            {
                Name = "Claude fast",
                Provider = "anthropic",
                Model = "claude-3-5-sonnet",
                ParametersJson = "{\"temperature\":0.1}",
                IsDefault = true
            });

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = settings.DatabaseFilePath,
                ForeignKeys = true
            }.ToString());
            await connection.OpenAsync();

            var defaults = (await connection.QueryAsync<string>("SELECT model_profile_id FROM model_profile WHERE is_default = 1")).ToList();
            Assert.Single(defaults);
            Assert.Equal(secondDefaultId, defaults[0]);
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
