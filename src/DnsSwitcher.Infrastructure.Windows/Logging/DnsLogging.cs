using DnsSwitcher.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Logging;

public static class DnsLogging
{
    public static ILoggerFactory CreateLoggerFactory(IAppPaths paths, LogLevel minimumLevel = LogLevel.Information)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            builder.AddProvider(new FileLoggerProvider(paths.LogFilePath));
        });
    }
}
