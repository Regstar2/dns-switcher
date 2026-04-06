using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Logging;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Tray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var host = CreateHost();
        host.ProfileService.EnsureInitializedAsync().GetAwaiter().GetResult();

        Application.Run(new TrayApplicationContext(host));
    }

    private static WindowsDnsSwitcherHost CreateHost()
    {
        var paths = PortableAppPaths.CreateDefault();
        paths.EnsureDirectories();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(paths.LogFilePath));
        });

        return new WindowsDnsSwitcherHost(paths, loggerFactory);
    }
}
