using System.Windows;
using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Logging;
using DnsSwitcher.Infrastructure.Windows.Presentation;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Ui;

public partial class App : Application
{
    public static WindowsDnsSwitcherHost Host { get; private set; } = null!;
    public static ILogger<App> Logger { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var paths = PortableAppPaths.CreateDefault();
        paths.EnsureDirectories();
        var loggerFactory = DnsLogging.CreateLoggerFactory(paths);

        try
        {
            Host = new WindowsDnsSwitcherHost(paths, loggerFactory);
            Logger = loggerFactory.CreateLogger<App>();

            RegisterGlobalExceptionHandlers();
            Logger.LogInformation("DnsSwitcher UI starting. Profiles file: {ProfilesFilePath}", paths.ProfilesFilePath);

            base.OnStartup(e);

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger<App>().LogCritical(exception, "DnsSwitcher UI failed during startup.");
            MessageBox.Show(
                FriendlyExceptionFormatter.ToUserMessage(exception),
                "DnsSwitcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            loggerFactory.Dispose();
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Logger is not null)
        {
            Logger.LogInformation("DnsSwitcher UI stopped with exit code {ExitCode}.", e.ApplicationExitCode);
        }

        if (Host is not null)
        {
            Host.Dispose();
        }

        base.OnExit(e);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.LogError(e.Exception, "Unhandled UI exception.");
        MessageBox.Show(
            FriendlyExceptionFormatter.ToUserMessage(e.Exception),
            "DnsSwitcher",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Logger.LogCritical(exception, "Unhandled AppDomain exception in UI.");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.LogError(e.Exception, "Unobserved task exception in UI.");
        e.SetObserved();
    }
}
