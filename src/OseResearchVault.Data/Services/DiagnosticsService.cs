using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Utilities;
using OseResearchVault.Data.Migrations;

namespace OseResearchVault.Data.Services;

public sealed class DiagnosticsService : IDiagnosticsService
{
    private const int MaxLogsInBundle = 10;

    public async Task ExportAsync(string zipFilePath, bool includeMigrationList = true, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetDirectory = Path.GetDirectoryName(zipFilePath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);
        }

        using var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);
        AddLatestLogFiles(archive);
        await AddManifestAsync(archive, includeMigrationList, cancellationToken);
    }

    private static void AddLatestLogFiles(ZipArchive archive)
    {
        if (!Directory.Exists(AppEnvironmentPaths.LogsDirectory))
        {
            return;
        }

        var logFiles = new DirectoryInfo(AppEnvironmentPaths.LogsDirectory)
            .GetFiles("*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(MaxLogsInBundle)
            .ToList();

        foreach (var logFile in logFiles)
        {
            archive.CreateEntryFromFile(logFile.FullName, Path.Combine("logs", logFile.Name));
        }
    }

    private static async Task AddManifestAsync(ZipArchive archive, bool includeMigrationList, CancellationToken cancellationToken)
    {
        var manifest = new
        {
            appVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
            osVersion = RuntimeInformation.OSDescription,
            generatedAtUtc = DateTimeOffset.UtcNow,
            includesMigrationList = includeMigrationList,
            migrations = includeMigrationList ? MigrationCatalog.All.Select(migration => migration.Id).ToArray() : Array.Empty<string>()
        };

        var entry = archive.CreateEntry("manifest.json");
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        }, cancellationToken);
    }
}
