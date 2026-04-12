using System.Windows;
using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Presentation;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DnsSwitcher.Ui;

public partial class App : System.Windows.Application
{
    private ResourceDictionary? currentThemeDictionary;
    private static AppTheme currentThemePreference = AppTheme.System;

    public static WindowsDnsSwitcherHost Host { get; private set; } = null!;
    public static ILogger<App> Logger { get; private set; } = null!;
    public static bool IsDarkThemeActive { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            Host = WindowsDnsSwitcherHostFactory.CreateDefault();
            Logger = Host.LoggerFactory.CreateLogger<App>();

            RegisterGlobalExceptionHandlers();
            LoadInitialPreferences();
            ApplyTheme(currentThemePreference);
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            Logger.LogInformation("DnsSwitcher UI starting. Profiles file: {ProfilesFilePath}", Host.Paths.ProfilesFilePath);

            base.OnStartup(e);

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            mainWindow.Show();
        }
        catch (Exception exception)
        {
            Host?.LoggerFactory.CreateLogger<App>().LogCritical(exception, "DnsSwitcher UI failed during startup.");
            System.Windows.MessageBox.Show(
                FriendlyExceptionFormatter.ToUserMessage(exception),
                "DnsSwitcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
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
        System.Windows.MessageBox.Show(
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

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (currentThemePreference == AppTheme.System)
        {
            ApplyTheme(currentThemePreference);
        }
    }

    public static void SetThemePreference(AppTheme theme)
    {
        currentThemePreference = theme;

        if (Current is App app)
        {
            app.ApplyTheme(theme);
        }
    }

    private void LoadInitialPreferences()
    {
        try
        {
            var appPreferencesStore = new JsonAppPreferencesStore(
                Host.Paths,
                Host.LoggerFactory.CreateLogger<JsonAppPreferencesStore>());
            var preferences = appPreferencesStore.LoadAsync().GetAwaiter().GetResult();
            currentThemePreference = preferences.Theme;
        }
        catch (Exception exception)
        {
            currentThemePreference = AppTheme.System;
            Logger.LogWarning(exception, "App preferences could not be loaded during startup. Default theme preference will be used.");
        }
    }

    private void ApplyTheme(AppTheme preferredTheme)
    {
        var isDarkTheme = preferredTheme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => WindowsThemeDetector.IsDarkModeEnabled(),
        };

        IsDarkThemeActive = isDarkTheme;
        var themeSource = new Uri(
            isDarkTheme ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml",
            UriKind.Relative);

        var newDictionary = new ResourceDictionary
        {
            Source = themeSource,
        };

        if (currentThemeDictionary is not null)
        {
            Resources.MergedDictionaries.Remove(currentThemeDictionary);
        }

        Resources.MergedDictionaries.Insert(0, newDictionary);
        currentThemeDictionary = newDictionary;

        foreach (Window window in Windows)
        {
            WindowThemeService.Apply(window);
        }
    }
}
