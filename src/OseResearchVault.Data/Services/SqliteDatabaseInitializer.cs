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
                version TEXT PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
            """);

        var applied = (await connection.QueryAsync<string>("SELECT version FROM schema_migrations"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var migration in MigrationCatalog.All.Where(m => !applied.Contains(m.Version)))
        {
            logger.LogInformation("Applying migration {MigrationVersion}", migration.Version);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await connection.ExecuteAsync(migration.Script, transaction: transaction);
            await connection.ExecuteAsync(
                "INSERT INTO schema_migrations (version, applied_at) VALUES (@Version, @AppliedAt)",
                new { migration.Version, AppliedAt = DateTimeOffset.UtcNow.ToString("O") },
                transaction: transaction);

            await transaction.CommitAsync(cancellationToken);
        }

        logger.LogInformation("Database initialized at {DatabasePath}", settings.DatabaseFilePath);
    }
}
