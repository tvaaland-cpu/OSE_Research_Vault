using Microsoft.Extensions.Logging;
using OseResearchVault.Core.Utilities;

namespace OseResearchVault.App.Logging;

public sealed class SimpleFileLoggerProvider : ILoggerProvider
{
    private const int MaxLogFiles = 10;
    private readonly string _logFilePath;

    public SimpleFileLoggerProvider()
    {
        var logDirectory = AppEnvironmentPaths.LogsDirectory;
        Directory.CreateDirectory(logDirectory);
        RotateLogs(logDirectory);
        _logFilePath = Path.Combine(logDirectory, $"app-{DateTime.UtcNow:yyyyMMddHHmmss}.log");
    }

    public ILogger CreateLogger(string categoryName) => new SimpleFileLogger(categoryName, _logFilePath);

    public void Dispose()
    {
    }

    private static void RotateLogs(string logDirectory)
    {
        var staleLogFiles = new DirectoryInfo(logDirectory)
            .GetFiles("*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Skip(MaxLogFiles - 1)
            .ToList();

        foreach (var logFile in staleLogFiles)
        {
            logFile.Delete();
        }
    }
}
