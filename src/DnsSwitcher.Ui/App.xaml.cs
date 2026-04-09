using System.Windows;
using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Logging;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Ui;

public partial class App : Application
{
    public static WindowsDnsSwitcherHost Host { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var paths = PortableAppPaths.CreateDefault();
        paths.EnsureDirectories();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(paths.LogFilePath));
        });

        Host = new WindowsDnsSwitcherHost(paths, loggerFactory);

        base.OnStartup(e);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Host.Dispose();
        base.OnExit(e);
    }
}
