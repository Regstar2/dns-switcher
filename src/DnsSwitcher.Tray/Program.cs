using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Configuration;
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
            using var context = new TrayApplicationContext(host);
            using var updateMonitor = new AutomaticUpdateMonitor(host);
            updateMonitor.Start();
            Application.Run(context);
            logger.LogInformation("DnsSwitcher Tray stopped.");
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "DnsSwitcher Tray failed during startup or execution.");
            TrayDialogs.ShowError(
                "DnsSwitcher",
                FriendlyExceptionFormatter.ToUserMessage(exception),
                LoadThemePreference(host));
        }
    }

    private static WindowsDnsSwitcherHost CreateHost()
    {
        return WindowsDnsSwitcherHostFactory.CreateDefault();
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        Application.ThreadException += (_, eventArgs) =>
        {
            logger?.LogError(eventArgs.Exception, "Unhandled tray UI exception.");
            var host = WindowsDnsSwitcherHostFactory.CreateDefault();
            try
            {
                TrayDialogs.ShowError(
                    "DnsSwitcher",
                    FriendlyExceptionFormatter.ToUserMessage(eventArgs.Exception),
                    LoadThemePreference(host));
            }
            finally
            {
                host.Dispose();
            }
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

    private static AppTheme LoadThemePreference(WindowsDnsSwitcherHost host)
    {
        try
        {
            var store = new JsonAppPreferencesStore(
                host.Paths,
                host.LoggerFactory.CreateLogger<JsonAppPreferencesStore>());
            return store.LoadAsync().GetAwaiter().GetResult().Theme;
        }
        catch
        {
            return AppTheme.System;
        }
    }
}
