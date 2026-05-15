using Microsoft.Extensions.Logging;

namespace SteamWorkshopAgent;

public sealed class StderrLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new StderrLogger(categoryName);

    public void Dispose()
    {
    }
}

public sealed class StderrLogger(string categoryName) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception == null)
            return;

        Console.Error.WriteLine($"[{DateTimeOffset.UtcNow:O}] {logLevel} {categoryName}: {message}");
        if (exception != null)
            Console.Error.WriteLine(exception);
    }
}
