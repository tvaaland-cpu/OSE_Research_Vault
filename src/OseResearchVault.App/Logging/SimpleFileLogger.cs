using Microsoft.Extensions.Logging;

namespace OseResearchVault.App.Logging;

public sealed class SimpleFileLogger(string category, string logFilePath) : ILogger
{
    private static readonly object Sync = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = $"{DateTimeOffset.UtcNow:O} [{logLevel}] {category}: {formatter(state, exception)}";
        if (exception is not null)
        {
            message += Environment.NewLine + exception;
        }

        lock (Sync)
        {
            File.AppendAllText(logFilePath, message + Environment.NewLine);
        }
    }
}
