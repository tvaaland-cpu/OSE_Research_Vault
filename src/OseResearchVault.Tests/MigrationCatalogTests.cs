using OseResearchVault.Data.Migrations;

namespace OseResearchVault.Tests;

public sealed class MigrationCatalogTests
{
    [Fact]
    public void Catalog_LoadsOrderedSqlMigrations()
    {
        Assert.NotEmpty(MigrationCatalog.All);

        var versions = MigrationCatalog.All.Select(m => m.Version).ToList();
        var ordered = versions.OrderBy(version => version, StringComparer.Ordinal).ToList();
        Assert.Equal(ordered, versions);

        var firstMigration = MigrationCatalog.All[0];
        Assert.EndsWith("initial_schema", firstMigration.Version, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE IF NOT EXISTS workspace", firstMigration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE VIRTUAL TABLE IF NOT EXISTS note_fts", firstMigration.Script, StringComparison.OrdinalIgnoreCase);
    }
}
