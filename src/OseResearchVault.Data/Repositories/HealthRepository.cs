using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;

namespace OseResearchVault.Data.Repositories;

public sealed class HealthRepository(IAppSettingsService appSettingsService) : IHealthRepository
{
    public async Task<int> GetCompanyCountAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = settings.DatabaseFilePath, ForeignKeys = true, Pooling = false }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM company");
    }
}
