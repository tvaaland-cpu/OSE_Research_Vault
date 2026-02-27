using System.IO.Compression;
using System.Text.Json;
using OseResearchVault.Core.Utilities;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class DiagnosticsServiceTests
{
    [Fact]
    public async Task ExportAsync_CreatesZipWithManifestAndLogs()
    {
        var diagnosticsService = new DiagnosticsService();
        Directory.CreateDirectory(AppEnvironmentPaths.LogsDirectory);

        var marker = Guid.NewGuid().ToString("N");
        var logFilePath = Path.Combine(AppEnvironmentPaths.LogsDirectory, $"test-{marker}.log");
        await File.WriteAllTextAsync(logFilePath, "test log");

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"diag-{marker}.zip");

        try
        {
            await diagnosticsService.ExportAsync(tempZipPath);

            Assert.True(File.Exists(tempZipPath));

            using var archive = ZipFile.OpenRead(tempZipPath);
            Assert.Contains(archive.Entries, entry => entry.FullName == "manifest.json");
            Assert.Contains(archive.Entries, entry =>
                entry.FullName.Replace('\\', '/').Equals($"logs/{Path.GetFileName(logFilePath)}", StringComparison.Ordinal));

            var manifestEntry = archive.GetEntry("manifest.json");
            Assert.NotNull(manifestEntry);

            using var stream = manifestEntry!.Open();
            var manifest = await JsonDocument.ParseAsync(stream);
            Assert.True(manifest.RootElement.TryGetProperty("appVersion", out _));
            Assert.True(manifest.RootElement.TryGetProperty("osVersion", out _));
            Assert.True(manifest.RootElement.TryGetProperty("generatedAtUtc", out _));
            Assert.True(manifest.RootElement.TryGetProperty("migrations", out _));
        }
        finally
        {
            if (File.Exists(logFilePath))
            {
                File.Delete(logFilePath);
            }

            if (File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
            }
        }
    }
}
