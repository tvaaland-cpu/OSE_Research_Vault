using System.Reflection;

namespace OseResearchVault.Data.Migrations;

public static class MigrationCatalog
{
    public static IReadOnlyList<SqlMigration> All { get; } = LoadMigrations();

    private static IReadOnlyList<SqlMigration> LoadMigrations()
    {
        var migrationDirectory = ResolveMigrationDirectory();
        if (!Directory.Exists(migrationDirectory))
        {
            throw new DirectoryNotFoundException($"Migration directory not found: {migrationDirectory}");
        }

        var migrationFiles = Directory
            .GetFiles(migrationDirectory, "*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        if (migrationFiles.Count == 0)
        {
            throw new InvalidOperationException($"No migration files found in {migrationDirectory}");
        }

        return migrationFiles
            .Select(file => new SqlMigration(
                Path.GetFileNameWithoutExtension(file),
                File.ReadAllText(file)))
            .ToList();
    }

    private static string ResolveMigrationDirectory()
    {
        var baseDirectoryCandidate = Path.Combine(AppContext.BaseDirectory, "Migrations");
        if (Directory.Exists(baseDirectoryCandidate))
        {
            return baseDirectoryCandidate;
        }

        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
        {
            var assemblyDirectoryCandidate = Path.Combine(assemblyLocation, "Migrations");
            if (Directory.Exists(assemblyDirectoryCandidate))
            {
                return assemblyDirectoryCandidate;
            }
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "src", "OseResearchVault.Data", "Migrations");
    }
}
