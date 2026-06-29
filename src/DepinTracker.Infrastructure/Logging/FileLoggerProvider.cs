namespace DepinTracker.Infrastructure.Logging;

using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// A dependency-free file logger that writes to a daily file under the portable
/// <c>logs/</c> folder. Kept intentionally minimal (rather than pulling in a logging
/// framework) so the dependency surface stays exactly as specified. Writes are
/// serialized with a lock; rolling is by calendar day.
/// </summary>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly LogLevel _minLevel;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string logDirectory, LogLevel minLevel = LogLevel.Information)
    {
        _logDirectory = logDirectory;
        _minLevel = minLevel;
        Directory.CreateDirectory(_logDirectory);
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    public void Dispose() => _loggers.Clear();

    private void Write(string category, LogLevel level, string message, Exception? exception)
    {
        var line = new StringBuilder()
            .Append(DateTimeOffset.UtcNow.ToString("O"))
            .Append(" [").Append(level).Append("] ")
            .Append(category).Append(" - ").Append(message);

        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        var path = Path.Combine(_logDirectory, $"depin-tracker-{DateTime.UtcNow:yyyyMMdd}.log");
        lock (_gate)
        {
            File.AppendAllText(path, line.AppendLine().ToString());
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly FileLoggerProvider _provider;

        public FileLogger(string category, FileLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _provider._minLevel && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.Write(_category, logLevel, formatter(state, exception), exception);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
