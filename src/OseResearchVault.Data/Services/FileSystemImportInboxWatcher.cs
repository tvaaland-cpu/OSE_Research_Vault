using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class FileSystemImportInboxWatcher : IImportInboxWatcher
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(250);
    private const int MaxLockAttempts = 20;

    private readonly IDocumentImportService _documentImportService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly ILogger<FileSystemImportInboxWatcher> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingImports = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _importGate = new(1, 1);
    private readonly object _watcherLock = new();

    private FileSystemWatcher? _watcher;

    public FileSystemImportInboxWatcher(
        IDocumentImportService documentImportService,
        IAppSettingsService appSettingsService,
        ILogger<FileSystemImportInboxWatcher> logger)
    {
        _documentImportService = documentImportService;
        _appSettingsService = appSettingsService;
        _logger = logger;
    }

    public event EventHandler<ImportInboxEvent>? FileImported;

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetSettingsAsync(cancellationToken);

        lock (_watcherLock)
        {
            _watcher?.Dispose();
            _watcher = null;

            if (!settings.ImportInboxEnabled || string.IsNullOrWhiteSpace(settings.ImportInboxFolderPath) || !Directory.Exists(settings.ImportInboxFolderPath))
            {
                return;
            }

            var watcher = new FileSystemWatcher(settings.ImportInboxFolderPath)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            watcher.Created += OnFileCreated;
            watcher.Renamed += OnFileRenamed;
            _watcher = watcher;
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        ScheduleImport(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        ScheduleImport(e.FullPath);
    }

    private void ScheduleImport(string path)
    {
        if (Directory.Exists(path))
        {
            return;
        }

        _pendingImports.AddOrUpdate(
            path,
            _ =>
            {
                var cts = new CancellationTokenSource();
                _ = ImportWhenReadyAsync(path, cts.Token);
                return cts;
            },
            (_, existing) =>
            {
                existing.Cancel();
                existing.Dispose();
                var cts = new CancellationTokenSource();
                _ = ImportWhenReadyAsync(path, cts.Token);
                return cts;
            });
    }

    private async Task ImportWhenReadyAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DebounceDelay, cancellationToken);

            var ready = await WaitForFileReadyAsync(path, cancellationToken);
            if (!ready)
            {
                _logger.LogWarning("Skipping import for file {Path} because it remained locked or unavailable", path);
                return;
            }

            await _importGate.WaitAsync(cancellationToken);
            try
            {
                var result = (await _documentImportService.ImportFilesAsync([path], cancellationToken)).FirstOrDefault();
                if (result is null)
                {
                    return;
                }

                FileImported?.Invoke(this, new ImportInboxEvent(
                    Path.GetFileName(path),
                    result.Succeeded,
                    result.ErrorMessage));
            }
            finally
            {
                _importGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import inbox file {Path}", path);
            FileImported?.Invoke(this, new ImportInboxEvent(Path.GetFileName(path), false, ex.Message));
        }
        finally
        {
            if (_pendingImports.TryRemove(path, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    private static async Task<bool> WaitForFileReadyAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxLockAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length >= 0)
                {
                    return true;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            await Task.Delay(LockRetryDelay, cancellationToken);
        }

        return false;
    }

    public void Dispose()
    {
        lock (_watcherLock)
        {
            _watcher?.Dispose();
            _watcher = null;
        }

        foreach (var entry in _pendingImports.Values)
        {
            entry.Cancel();
            entry.Dispose();
        }

        _pendingImports.Clear();
        _importGate.Dispose();
    }
}
