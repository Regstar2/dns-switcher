using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Logging;

public sealed class FileLoggerProvider(string filePath) : ILoggerProvider
{
    private readonly object gate = new();
    private volatile bool isFaulted;

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(
            filePath,
            categoryName,
            gate,
            () => isFaulted,
            () => isFaulted = true);
    }

    public void Dispose()
    {
    }

    private sealed class FileLogger(string path, string categoryName, object gate, Func<bool> isFaulted, Action markFaulted) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (isFaulted())
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

                var line = string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"{DateTimeOffset.Now:O} [{logLevel}] {categoryName}: {formatter(state, exception)}");

                lock (gate)
                {
                    File.AppendAllText(path, line + Environment.NewLine);

                    if (exception is not null)
                    {
                        File.AppendAllText(path, exception + Environment.NewLine);
                    }
                }
            }
            catch (Exception)
            {
                markFaulted();
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
