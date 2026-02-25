using OseResearchVault.Data.Migrations;

namespace OseResearchVault.Tests;

public sealed class MigrationCatalogTests
{
    [Fact]
    public void Catalog_HasInitialMigrationContainingFts()
    {
        var migration = Assert.Single(MigrationCatalog.All);

        Assert.Contains("fts5", migration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("schema", migration.Id, StringComparison.OrdinalIgnoreCase);
    }
}
