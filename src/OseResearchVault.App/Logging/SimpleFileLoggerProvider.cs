using Microsoft.Extensions.Logging;

namespace OseResearchVault.App.Logging;

public sealed class SimpleFileLoggerProvider : ILoggerProvider
{
    private readonly string _logFilePath;

    public SimpleFileLoggerProvider()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OSE Research Vault",
            "logs");
        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, $"app-{DateTime.UtcNow:yyyyMMdd}.log");
    }

    public ILogger CreateLogger(string categoryName) => new SimpleFileLogger(categoryName, _logFilePath);

    public void Dispose()
    {
    }
}
