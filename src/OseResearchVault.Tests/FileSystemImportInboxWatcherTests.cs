using System.Collections.Concurrent;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class FileSystemImportInboxWatcherTests
{
    [Fact]
    public async Task ReloadAsync_WhenEnabled_ImportsNewFilesInWatchedFolder()
    {
        var inboxPath = Path.Combine(Path.GetTempPath(), $"ose-inbox-{Guid.NewGuid():N}");
        Directory.CreateDirectory(inboxPath);

        try
        {
            var importService = new FakeDocumentImportService();
            var settingsService = new FakeAppSettingsService(new AppSettings
            {
                DatabaseDirectory = inboxPath,
                VaultStorageDirectory = inboxPath,
                ImportInboxEnabled = true,
                ImportInboxFolderPath = inboxPath
            });

            using var watcher = new FileSystemImportInboxWatcher(importService, settingsService, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemImportInboxWatcher>.Instance);
            await watcher.ReloadAsync();

            var fileA = Path.Combine(inboxPath, "a.pdf");
            var fileB = Path.Combine(inboxPath, "b.pdf");
            await File.WriteAllTextAsync(fileA, "content-a");
            await File.WriteAllTextAsync(fileB, "content-b");

            await WaitForConditionAsync(() => importService.ImportedPaths.Count >= 2, TimeSpan.FromSeconds(8));

            Assert.Contains(fileA, importService.ImportedPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(fileB, importService.ImportedPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(inboxPath, recursive: true);
        }
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;
        while (!predicate())
        {
            if (DateTime.UtcNow - started > timeout)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(100);
        }
    }

    private sealed class FakeAppSettingsService(AppSettings settings) : IAppSettingsService
    {
        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);
        public Task SaveSettingsAsync(AppSettings updatedSettings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDocumentImportService : IDocumentImportService
    {
        public ConcurrentBag<string> ImportedPaths { get; } = [];

        public Task<IReadOnlyList<DocumentImportResult>> ImportFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
        {
            var results = new List<DocumentImportResult>();
            foreach (var path in filePaths)
            {
                ImportedPaths.Add(path);
                results.Add(new DocumentImportResult { FilePath = path, Succeeded = true });
            }

            return Task.FromResult<IReadOnlyList<DocumentImportResult>>(results);
        }

        public Task<IReadOnlyList<DocumentRecord>> GetDocumentsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DocumentRecord>>([]);
        public Task<DocumentRecord?> GetDocumentDetailsAsync(string documentId, CancellationToken cancellationToken = default) => Task.FromResult<DocumentRecord?>(null);
        public Task UpdateDocumentCompanyAsync(string documentId, string? companyId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
