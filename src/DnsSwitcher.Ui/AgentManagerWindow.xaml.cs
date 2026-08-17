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
        ApplyLocalization();
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
            localizer["AgentUninstallConfirm"],
            localizer["AgentWindowTitle"],
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
        SetBusy(true, localizer.Format("AgentRunningCommandFormat", command));

        try
        {
            var cliPath = DesktopClientLayout.TryGetCliExecutablePath(AppContext.BaseDirectory)
                ?? throw new FileNotFoundException(localizer["AgentCliNotFound"]);
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
                AppendStatus(localizer["AgentFailedStartElevated"]);
                return;
            }

            await process.WaitForExitAsync().ConfigureAwait(true);
            AppendStatus(localizer.Format("AgentCommandFinishedFormat", $"service {command}", process.ExitCode));
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            AppendStatus(localizer["AgentUacCancelled"]);
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
        SetBusy(true, localizer["AgentRefreshingStatus"]);

        try
        {
            var info = await host.AgentServiceManager.GetInfoAsync().ConfigureAwait(true);
            var agentAvailable = await host.AgentDnsSwitchService.IsAgentAvailableAsync().ConfigureAwait(true);
            var isInstalled = !string.IsNullOrWhiteSpace(info.ServiceBinaryPath);
            var isRunning = string.Equals(info.Status.ToString(), "Running", StringComparison.OrdinalIgnoreCase);

            ServiceStatusValueTextBlock.Text = isRunning
                ? localizer["EnabledValue"]
                : localizer["DisabledValue"];
            AgentConnectionValueTextBlock.Text = agentAvailable ? localizer["YesValue"] : localizer["NoValue"];
            ServiceStatusValueTextBlock.Foreground = FindResource(isRunning ? "SuccessBrush" : "SecondaryTextBrush") as System.Windows.Media.Brush;
            AgentConnectionValueTextBlock.Foreground = FindResource(agentAvailable ? "SuccessBrush" : "WarningBrush") as System.Windows.Media.Brush;

            InstallButton.Visibility = isInstalled ? Visibility.Collapsed : Visibility.Visible;
            ReinstallButton.Visibility = isInstalled ? Visibility.Visible : Visibility.Collapsed;
            StartButton.Visibility = isInstalled && !isRunning ? Visibility.Visible : Visibility.Collapsed;
            StopButton.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
            UninstallButton.Visibility = isInstalled ? Visibility.Visible : Visibility.Collapsed;

            StatusTextBox.Text =
                $"{localizer["AgentServiceStatusLine"]} {info.Status}{Environment.NewLine}" +
                $"{localizer["AgentPipeAvailableLine"]} {(agentAvailable ? localizer["YesValue"] : localizer["NoValue"])}{Environment.NewLine}" +
                $"{localizer["AgentServicePathLine"]} {info.ServiceBinaryPath ?? localizer["NotInstalledValue"]}{Environment.NewLine}" +
                $"{localizer["AgentExpectedPathLine"]} {info.ExpectedBinaryPath}{Environment.NewLine}" +
                $"{localizer["AgentPathCurrentLine"]} {(info.PointsToExpectedPath ? localizer["YesValue"] : localizer["NoValue"])}{Environment.NewLine}" +
                $"{localizer["AgentPortableDataLine"]} {host.Paths.AppDirectory}{Environment.NewLine}" +
                $"{localizer["AgentLogsLine"]} {host.Paths.LogFilePath}{Environment.NewLine}";

            if (info.IsStalePath)
            {
                AppendStatus(localizer["AgentStalePathWarning"]);
            }
        }
        catch (Exception exception)
        {
            ServiceStatusValueTextBlock.Text = localizer["UnknownValue"];
            AgentConnectionValueTextBlock.Text = localizer["NoValue"];
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

    private void ApplyLocalization()
    {
        Title = localizer["AgentWindowTitle"];
        HintTextBlock.Text = localizer["AgentWindowHint"];
        ServiceStatusLabelTextBlock.Text = localizer["AgentServiceStatusLine"];
        AgentConnectionLabelTextBlock.Text = localizer["AgentPipeAvailableLine"];
        TechnicalHeaderTextBlock.Text = localizer["MoreButton"];
        InstallButton.Content = localizer["AgentInstallButton"];
        ReinstallButton.Content = localizer["AgentReinstallButton"];
        StartButton.Content = localizer["AgentStartButton"];
        StopButton.Content = localizer["AgentStopButton"];
        UninstallButton.Content = localizer["AgentUninstallButton"];
        RefreshButton.Content = localizer["AgentRefreshButton"];
        CloseButton.Content = localizer["CloseButton"];
    }
}
