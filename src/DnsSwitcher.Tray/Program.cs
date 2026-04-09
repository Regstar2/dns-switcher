using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Logging;
using DnsSwitcher.Infrastructure.Windows.Presentation;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Tray;

internal static class Program
{
    private static ILogger? logger;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        using var host = CreateHost();
        logger = host.LoggerFactory.CreateLogger("DnsSwitcher.Tray");
        RegisterGlobalExceptionHandlers();

        try
        {
            logger.LogInformation("DnsSwitcher Tray starting. Profiles file: {ProfilesFilePath}", host.Paths.ProfilesFilePath);
            host.ProfileService.EnsureInitializedAsync().GetAwaiter().GetResult();
            Application.Run(new TrayApplicationContext(host));
            logger.LogInformation("DnsSwitcher Tray stopped.");
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "DnsSwitcher Tray failed during startup or execution.");
            MessageBox.Show(
                FriendlyExceptionFormatter.ToUserMessage(exception),
                "DnsSwitcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static WindowsDnsSwitcherHost CreateHost()
    {
        var paths = PortableAppPaths.CreateDefault();
        paths.EnsureDirectories();
        var loggerFactory = DnsLogging.CreateLoggerFactory(paths);

        return new WindowsDnsSwitcherHost(paths, loggerFactory);
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        Application.ThreadException += (_, eventArgs) =>
        {
            logger?.LogError(eventArgs.Exception, "Unhandled tray UI exception.");
            MessageBox.Show(
                FriendlyExceptionFormatter.ToUserMessage(eventArgs.Exception),
                "DnsSwitcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                logger?.LogCritical(exception, "Unhandled AppDomain exception in tray.");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            logger?.LogError(eventArgs.Exception, "Unobserved task exception in tray.");
            eventArgs.SetObserved();
        };
    }
}
