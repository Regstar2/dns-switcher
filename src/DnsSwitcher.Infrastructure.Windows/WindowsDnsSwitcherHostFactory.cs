using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Logging;

namespace DnsSwitcher.Infrastructure.Windows;

public static class WindowsDnsSwitcherHostFactory
{
    public static WindowsDnsSwitcherHost CreateDefault()
    {
        return Create(configPath: null);
    }

    public static WindowsDnsSwitcherHost Create(string? configPath)
    {
        var paths = string.IsNullOrWhiteSpace(configPath)
            ? PortableAppPaths.CreateDefault()
            : PortableAppPaths.CreateFromConfigPath(configPath);

        paths.EnsureDirectories();

        return new WindowsDnsSwitcherHost(paths, DnsLogging.CreateLoggerFactory(paths));
    }
}
