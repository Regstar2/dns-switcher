using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Desktop;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Ui;

public partial class AgentManagerWindow : Window
{
    private readonly WindowsDnsSwitcherHost host;
    private readonly AppLocalizer localizer;

    public AgentManagerWindow(WindowsDnsSwitcherHost host, AppLocalizer localizer)
    {
        InitializeComponent();
        WindowThemeService.Attach(this);
        this.host = host;
        this.localizer = localizer;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await RefreshStatusAsync().ConfigureAwait(true);
    }

    private async void OnInstallClicked(object sender, RoutedEventArgs e)
    {
        await RunElevatedServiceCommandAsync("install").ConfigureAwait(true);
    }

    private async void OnReinstallClicked(object sender, RoutedEventArgs e)
    {
        await RunElevatedServiceCommandAsync("reinstall").ConfigureAwait(true);
    }

    private async void OnStartClicked(object sender, RoutedEventArgs e)
    {
        await RunElevatedServiceCommandAsync("start").ConfigureAwait(true);
    }

    private async void OnStopClicked(object sender, RoutedEventArgs e)
    {
        await RunElevatedServiceCommandAsync("stop").ConfigureAwait(true);
    }

    private async void OnUninstallClicked(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Uninstall DnsSwitcher Agent service?",
            "DnsSwitcher Agent",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            await RunElevatedServiceCommandAsync("uninstall").ConfigureAwait(true);
        }
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        await RefreshStatusAsync().ConfigureAwait(true);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private async Task RunElevatedServiceCommandAsync(string command)
    {
        SetBusy(true, $"Running service command '{command}' through elevated CLI...");

        try
        {
            var cliPath = DesktopClientLayout.TryGetCliExecutablePath(AppContext.BaseDirectory)
                ?? throw new FileNotFoundException("DnsSwitcher.Cli.exe could not be found. Rebuild or reinstall the package.");
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = $"service {command}",
                WorkingDirectory = Path.GetDirectoryName(cliPath) ?? AppContext.BaseDirectory,
                UseShellExecute = true,
                Verb = "runas",
            });

            if (process is null)
            {
                AppendStatus("Failed to start elevated CLI process.");
                return;
            }

            await process.WaitForExitAsync().ConfigureAwait(true);
            AppendStatus($"Command 'service {command}' finished with exit code {process.ExitCode}.");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            AppendStatus("UAC prompt was cancelled by the user.");
        }
        catch (Exception exception)
        {
            AppendStatus(FriendlyExceptionFormatter.ToUserMessage(exception));
        }
        finally
        {
            SetBusy(false);
            await RefreshStatusAsync().ConfigureAwait(true);
        }
    }

    private async Task RefreshStatusAsync()
    {
        SetBusy(true, "Refreshing agent status...");

        try
        {
            var info = await host.AgentServiceManager.GetInfoAsync().ConfigureAwait(true);
            var agentAvailable = await host.AgentDnsSwitchService.IsAgentAvailableAsync().ConfigureAwait(true);
            StatusTextBox.Text =
                $"Service status: {info.Status}{Environment.NewLine}" +
                $"Agent pipe available: {agentAvailable}{Environment.NewLine}" +
                $"Service binary path: {info.ServiceBinaryPath ?? "<not installed>"}{Environment.NewLine}" +
                $"Expected binary path: {info.ExpectedBinaryPath}{Environment.NewLine}" +
                $"Path current: {info.PointsToExpectedPath}{Environment.NewLine}" +
                $"Portable data: {host.Paths.AppDirectory}{Environment.NewLine}" +
                $"Logs: {host.Paths.LogFilePath}{Environment.NewLine}";

            if (info.IsStalePath)
            {
                AppendStatus("Warning: service points to a stale path. Use Reinstall Agent.");
            }
        }
        catch (Exception exception)
        {
            StatusTextBox.Text = FriendlyExceptionFormatter.ToUserMessage(exception);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        InstallButton.IsEnabled = !busy;
        ReinstallButton.IsEnabled = !busy;
        StartButton.IsEnabled = !busy;
        StopButton.IsEnabled = !busy;
        UninstallButton.IsEnabled = !busy;
        RefreshButton.IsEnabled = !busy;
        CloseButton.IsEnabled = !busy;

        if (!string.IsNullOrWhiteSpace(message))
        {
            AppendStatus(message);
        }
    }

    private void AppendStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(StatusTextBox.Text))
        {
            StatusTextBox.Text = message;
            return;
        }

        StatusTextBox.Text += $"{Environment.NewLine}{message}";
        StatusTextBox.ScrollToEnd();
    }
}
