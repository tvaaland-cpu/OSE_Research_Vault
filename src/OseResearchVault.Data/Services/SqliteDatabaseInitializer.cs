using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Data.Migrations;

namespace OseResearchVault.Data.Services;

public sealed class SqliteDatabaseInitializer(
    IAppSettingsService appSettingsService,
    ILogger<SqliteDatabaseInitializer> logger) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = settings.DatabaseFilePath,
            ForeignKeys = true
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                id TEXT PRIMARY KEY,
                applied_utc TEXT NOT NULL
            );
            """);

        var applied = (await connection.QueryAsync<string>("SELECT id FROM schema_migrations"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var migration in MigrationCatalog.All.Where(m => !applied.Contains(m.Id)))
        {
            logger.LogInformation("Applying migration {MigrationId}", migration.Id);
            await connection.ExecuteAsync(migration.Script);
            await connection.ExecuteAsync(
                "INSERT INTO schema_migrations (id, applied_utc) VALUES (@Id, @AppliedUtc)",
                new { migration.Id, AppliedUtc = DateTimeOffset.UtcNow.ToString("O") });
        }

        logger.LogInformation("Database initialized at {DatabasePath}", settings.DatabaseFilePath);
    }
}
