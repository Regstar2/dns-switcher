using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Tray;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Tray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private const int RefreshIntervalMilliseconds = 15000;

    private readonly WindowsDnsSwitcherHost host;
    private readonly ILogger<TrayApplicationContext> logger;
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip contextMenu;
    private readonly ToolStripMenuItem statusMenuItem;
    private readonly ToolStripMenuItem adapterMenuItem;
    private readonly ToolStripMenuItem enableDnsMenuItem;
    private readonly ToolStripMenuItem disableDnsMenuItem;
    private readonly ToolStripMenuItem switchNextMenuItem;
    private readonly ToolStripMenuItem testsMenuItem;
    private readonly ToolStripMenuItem testDnsMenuItem;
    private readonly ToolStripMenuItem testSitesMenuItem;
    private readonly ToolStripMenuItem profilesMenuItem;
    private readonly ToolStripMenuItem settingsMenuItem;
    private readonly ToolStripMenuItem notificationsMenuItem;
    private readonly ToolStripMenuItem showAdapterNameMenuItem;
    private readonly ToolStripMenuItem exitMenuItem;
    private readonly System.Windows.Forms.Timer refreshTimer;
    private readonly TrayIconProvider trayIconProvider = new();
    private readonly DnsProfileSelectionService profileSelectionService = new();
    private readonly JsonTraySettingsStore traySettingsStore;

    private string? preferredProfileId;
    private AppConfig? lastConfiguration;
    private DnsStatus? lastStatus;
    private Exception? lastRefreshError;
    private TraySettings traySettings = TraySettings.Default;
    private bool isRefreshing;
    private bool isActionInProgress;

    public TrayApplicationContext(WindowsDnsSwitcherHost host)
    {
        this.host = host;
        logger = host.LoggerFactory.CreateLogger<TrayApplicationContext>();
        traySettingsStore = new JsonTraySettingsStore(host.Paths, host.LoggerFactory.CreateLogger<JsonTraySettingsStore>());
        traySettings = LoadTraySettingsOrDefault();

        statusMenuItem = new ToolStripMenuItem("Status: loading...")
        {
            Enabled = false,
        };
        adapterMenuItem = new ToolStripMenuItem("Adapter: loading...")
        {
            Enabled = false,
            Visible = traySettings.ShowAdapterName,
        };

        enableDnsMenuItem = new ToolStripMenuItem("Enable DNS");
        disableDnsMenuItem = new ToolStripMenuItem("Disable DNS");
        switchNextMenuItem = new ToolStripMenuItem("Switch Next");
        testsMenuItem = new ToolStripMenuItem("Tests");
        testDnsMenuItem = new ToolStripMenuItem("Test DNS");
        testSitesMenuItem = new ToolStripMenuItem("Test Sites");
        profilesMenuItem = new ToolStripMenuItem("Show Profiles");
        settingsMenuItem = new ToolStripMenuItem("Settings");
        notificationsMenuItem = new ToolStripMenuItem("Show notifications");
        showAdapterNameMenuItem = new ToolStripMenuItem("Show adapter name");
        exitMenuItem = new ToolStripMenuItem("Exit");

        enableDnsMenuItem.Click += async (_, _) => await ExecuteActionAsync(EnableDnsAsync).ConfigureAwait(true);
        disableDnsMenuItem.Click += async (_, _) => await ExecuteActionAsync(DisableDnsAsync).ConfigureAwait(true);
        switchNextMenuItem.Click += async (_, _) => await ExecuteActionAsync(SwitchNextAsync).ConfigureAwait(true);
        testDnsMenuItem.Click += async (_, _) => await ExecuteActionAsync(TestDnsAsync).ConfigureAwait(true);
        testSitesMenuItem.Click += async (_, _) => await ExecuteActionAsync(TestSitesAsync).ConfigureAwait(true);
        notificationsMenuItem.Click += async (_, _) => await ToggleNotificationsAsync().ConfigureAwait(true);
        showAdapterNameMenuItem.Click += async (_, _) => await ToggleAdapterVisibilityAsync().ConfigureAwait(true);
        exitMenuItem.Click += (_, _) => ExitThread();

        settingsMenuItem.DropDownItems.Add(notificationsMenuItem);
        settingsMenuItem.DropDownItems.Add(showAdapterNameMenuItem);
        testsMenuItem.DropDownItems.Add(testDnsMenuItem);
        testsMenuItem.DropDownItems.Add(testSitesMenuItem);

        contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(statusMenuItem);
        contextMenu.Items.Add(adapterMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(enableDnsMenuItem);
        contextMenu.Items.Add(disableDnsMenuItem);
        contextMenu.Items.Add(switchNextMenuItem);
        contextMenu.Items.Add(testsMenuItem);
        contextMenu.Items.Add(profilesMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(settingsMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitMenuItem);
        contextMenu.Opening += async (_, _) => await RefreshStateAsync().ConfigureAwait(true);

        notifyIcon = new NotifyIcon
        {
            Icon = trayIconProvider.GetIcon(TrayIconState.Default),
            Text = "DnsSwitcher",
            ContextMenuStrip = contextMenu,
            Visible = true,
        };

        notifyIcon.DoubleClick += async (_, _) => await ShowStatusDialogAsync().ConfigureAwait(true);

        refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = RefreshIntervalMilliseconds,
            Enabled = true,
        };

        refreshTimer.Tick += async (_, _) => await RefreshStateAsync().ConfigureAwait(true);
        Application.Idle += HandleApplicationIdle;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Application.Idle -= HandleApplicationIdle;
            refreshTimer.Dispose();
            notifyIcon.Dispose();
            contextMenu.Dispose();
            trayIconProvider.Dispose();
        }

        base.Dispose(disposing);
    }

    private async void HandleApplicationIdle(object? sender, EventArgs eventArgs)
    {
        Application.Idle -= HandleApplicationIdle;
        await RefreshStateAsync().ConfigureAwait(true);
    }

    private async Task RefreshStateAsync()
    {
        if (isRefreshing || isActionInProgress)
        {
            return;
        }

        await RefreshStateCoreAsync().ConfigureAwait(true);
    }

    private async Task RefreshStateCoreAsync()
    {
        isRefreshing = true;

        try
        {
            var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
            var status = await host.DnsManager.GetStatusAsync().ConfigureAwait(true);

            SyncPreferredProfile(configuration, status);
            lastConfiguration = configuration;
            lastStatus = status;
            lastRefreshError = null;
            ApplyPresentationState();
        }
        catch (Exception exception)
        {
            lastConfiguration = null;
            lastStatus = null;
            lastRefreshError = exception;
            ApplyPresentationState();
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private async Task ExecuteActionAsync(Func<Task> action)
    {
        if (isActionInProgress)
        {
            return;
        }

        isActionInProgress = true;
        SetBusyMenuState();

        try
        {
            await action().ConfigureAwait(true);
        }
        catch (DnsSwitchException exception)
        {
            ShowError("DnsSwitcher", exception.Message);
        }
        catch (Exception exception)
        {
            ShowError("DnsSwitcher", exception.Message);
        }
        finally
        {
            isActionInProgress = false;
            await RefreshStateCoreAsync().ConfigureAwait(true);
        }
    }

    private async Task EnableDnsAsync()
    {
        var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
        var status = await host.DnsManager.GetStatusAsync().ConfigureAwait(true);

        SyncPreferredProfile(configuration, status);

        var profile = profileSelectionService.GetProfileToEnable(configuration, status, preferredProfileId)
            ?? throw new DnsOperationFailedException("No static DNS profiles are configured.");

        await host.AgentDnsSwitchService.ApplyProfileAsync(profile.Id).ConfigureAwait(true);

        preferredProfileId = profile.Id;
        ShowSuccess($"DNS enabled: {profile.Name}");
    }

    private async Task DisableDnsAsync()
    {
        var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
        var status = await host.DnsManager.GetStatusAsync().ConfigureAwait(true);

        SyncPreferredProfile(configuration, status);

        await host.AgentDnsSwitchService.ResetToDhcpAsync().ConfigureAwait(true);
        ShowSuccess("DNS disabled. DHCP is now active.");
    }

    private async Task SwitchNextAsync()
    {
        var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
        var status = await host.DnsManager.GetStatusAsync().ConfigureAwait(true);

        SyncPreferredProfile(configuration, status);

        var profile = profileSelectionService.GetNextProfile(configuration, status, preferredProfileId)
            ?? throw new DnsOperationFailedException("No static DNS profiles are configured.");

        await host.AgentDnsSwitchService.ApplyProfileAsync(profile.Id).ConfigureAwait(true);

        preferredProfileId = profile.Id;
        ShowSuccess($"DNS switched: {profile.Name}");
    }

    private async Task ApplyProfileAsync(string profileId)
    {
        await host.AgentDnsSwitchService.ApplyProfileAsync(profileId).ConfigureAwait(true);
        preferredProfileId = profileId;

        var profile = await host.ProfileService.GetRequiredProfileAsync(profileId).ConfigureAwait(true);
        ShowSuccess($"DNS switched: {profile.Name}");
    }

    private async Task TestDnsAsync()
    {
        var result = await host.DnsTester.TestCurrentDnsAsync().ConfigureAwait(true);
        var summary = BuildDnsTestSummary(result);

        if (!traySettings.NotificationsEnabled)
        {
            logger.LogInformation("Tray DNS test finished without balloon notification: {Summary}", summary);
            ShowInformation("DnsSwitcher DNS Test", BuildDnsTestDetails(result));
            return;
        }

        notifyIcon.BalloonTipTitle = "DnsSwitcher";
        notifyIcon.BalloonTipText = summary;
        notifyIcon.BalloonTipIcon = result.Status == DnsTestStatus.Failed
            ? ToolTipIcon.Error
            : result.Status == DnsTestStatus.Slow
                ? ToolTipIcon.Warning
                : ToolTipIcon.Info;
        notifyIcon.ShowBalloonTip(2500);
    }

    private async Task ToggleNotificationsAsync()
    {
        await UpdateTraySettingsAsync(traySettings with
        {
            NotificationsEnabled = !traySettings.NotificationsEnabled,
        }).ConfigureAwait(true);
    }

    private async Task ToggleAdapterVisibilityAsync()
    {
        await UpdateTraySettingsAsync(traySettings with
        {
            ShowAdapterName = !traySettings.ShowAdapterName,
        }).ConfigureAwait(true);
    }

    private async Task ShowStatusDialogAsync()
    {
        try
        {
            var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
            var status = await host.DnsManager.GetStatusAsync().ConfigureAwait(true);

            SyncPreferredProfile(configuration, status);

            var lines = new List<string>
            {
                $"Status: {TrayTextFormatter.BuildStatusLabel(configuration, status)}",
                $"Adapter: {status.AdapterName ?? "<none>"}",
                $"Mode: {status.Mode}",
                $"Matched profile: {status.MatchedProfileId ?? "<none>"}",
                $"Selected profile: {preferredProfileId ?? "<none>"}",
                $"IPv4 DNS: {FormatServers(status.Ipv4.NameServers)}",
                $"IPv6 DNS: {FormatServers(status.Ipv6.NameServers)}",
            };

            ShowInformation(
                "DnsSwitcher",
                string.Join(Environment.NewLine, lines));
        }
        catch (Exception exception)
        {
            ShowError("DnsSwitcher", exception.Message);
        }
    }

    private async Task TestSitesAsync()
    {
        var result = await host.ConnectivityTester.TestCurrentSitesAsync().ConfigureAwait(true);
        var summary = BuildSiteTestSummary(result);

        if (!traySettings.NotificationsEnabled)
        {
            ShowInformation("DnsSwitcher Site Test", BuildSiteTestDetails(result));
            return;
        }

        notifyIcon.BalloonTipTitle = "DnsSwitcher";
        notifyIcon.BalloonTipText = summary;
        notifyIcon.BalloonTipIcon = result.Status is ConnectivityTestStatus.Blocked or ConnectivityTestStatus.Failed
            ? ToolTipIcon.Warning
            : result.Status == ConnectivityTestStatus.Slow
                ? ToolTipIcon.Warning
                : ToolTipIcon.Info;
        notifyIcon.ShowBalloonTip(2500);
    }

    private void RebuildProfilesMenu(AppConfig configuration, DnsStatus status)
    {
        profilesMenuItem.DropDownItems.Clear();

        var profiles = profileSelectionService.GetSwitchableProfiles(configuration);

        if (profiles.Count == 0)
        {
            profilesMenuItem.DropDownItems.Add(new ToolStripMenuItem("No static DNS profiles")
            {
                Enabled = false,
            });
            return;
        }

        foreach (var profile in profiles)
        {
            var isCurrent = string.Equals(profile.Id, status.MatchedProfileId, StringComparison.OrdinalIgnoreCase);
            var isPreferred = !isCurrent
                && string.Equals(profile.Id, preferredProfileId, StringComparison.OrdinalIgnoreCase);
            var profileMenuItem = new ToolStripMenuItem(TrayTextFormatter.BuildProfileMenuText(profile, isCurrent, isPreferred))
            {
                Checked = isCurrent || isPreferred,
                Enabled = !isActionInProgress,
                Tag = profile.Id,
            };

            profileMenuItem.Click += async (_, _) =>
            {
                if (profileMenuItem.Tag is string profileId)
                {
                    await ExecuteActionAsync(() => ApplyProfileAsync(profileId)).ConfigureAwait(true);
                }
            };

            profilesMenuItem.DropDownItems.Add(profileMenuItem);
        }
    }

    private void UpdateMenuState(AppConfig configuration, DnsStatus status)
    {
        var enableProfile = profileSelectionService.GetProfileToEnable(configuration, status, preferredProfileId);
        var nextProfile = profileSelectionService.GetNextProfile(configuration, status, preferredProfileId);

        statusMenuItem.Text = TrayTextFormatter.BuildStatusMenuText(configuration, status);
        adapterMenuItem.Text = TrayTextFormatter.BuildAdapterMenuText(status, traySettings) ?? "Adapter: hidden";
        adapterMenuItem.Visible = traySettings.ShowAdapterName;
        enableDnsMenuItem.Text = TrayTextFormatter.BuildEnableMenuText(enableProfile);
        switchNextMenuItem.Text = TrayTextFormatter.BuildSwitchNextMenuText(nextProfile);

        enableDnsMenuItem.Enabled = !isActionInProgress && enableProfile is not null;
        disableDnsMenuItem.Enabled = !isActionInProgress && status.Mode != DnsMode.Dhcp;
        switchNextMenuItem.Enabled = !isActionInProgress && nextProfile is not null;
        testsMenuItem.Enabled = !isActionInProgress;
        testDnsMenuItem.Enabled = !isActionInProgress;
        testSitesMenuItem.Enabled = !isActionInProgress;
        profilesMenuItem.Enabled = !isActionInProgress && profileSelectionService.GetSwitchableProfiles(configuration).Count > 0;
        settingsMenuItem.Enabled = !isActionInProgress;
        notificationsMenuItem.Checked = traySettings.NotificationsEnabled;
        showAdapterNameMenuItem.Checked = traySettings.ShowAdapterName;
    }

    private void UpdateNotifyIcon(AppConfig? configuration, DnsStatus? status, Exception? error)
    {
        if (error is not null)
        {
            statusMenuItem.Text = "Status: error";
            adapterMenuItem.Visible = false;
            notifyIcon.Icon = trayIconProvider.GetIcon(TrayIconState.Error);
            notifyIcon.Text = TrayTextFormatter.BuildErrorNotifyIconText(error.Message);
            return;
        }

        if (configuration is null || status is null)
        {
            notifyIcon.Icon = trayIconProvider.GetIcon(TrayIconState.Default);
            notifyIcon.Text = "DnsSwitcher";
            return;
        }

        notifyIcon.Icon = trayIconProvider.GetIcon(ResolveTrayIconState(status));
        notifyIcon.Text = TrayTextFormatter.BuildNotifyIconText(configuration, status, traySettings);
    }

    private void SyncPreferredProfile(AppConfig configuration, DnsStatus status)
    {
        if (profileSelectionService.IsSwitchableProfile(configuration, status.MatchedProfileId))
        {
            preferredProfileId = status.MatchedProfileId;
            return;
        }

        if (profileSelectionService.IsSwitchableProfile(configuration, configuration.ActiveProfileId))
        {
            preferredProfileId = configuration.ActiveProfileId;
        }
    }

    private void SetBusyMenuState()
    {
        statusMenuItem.Text = "Status: working...";
        adapterMenuItem.Visible = traySettings.ShowAdapterName;
        enableDnsMenuItem.Enabled = false;
        disableDnsMenuItem.Enabled = false;
        switchNextMenuItem.Enabled = false;
        testsMenuItem.Enabled = false;
        testDnsMenuItem.Enabled = false;
        testSitesMenuItem.Enabled = false;
        profilesMenuItem.Enabled = false;
        settingsMenuItem.Enabled = false;
    }

    private void ApplyPresentationState()
    {
        if (lastRefreshError is not null)
        {
            SetBusyMenuState();
            profilesMenuItem.DropDownItems.Clear();
            profilesMenuItem.DropDownItems.Add(new ToolStripMenuItem("Unable to load profiles")
            {
                Enabled = false,
            });
            notificationsMenuItem.Checked = traySettings.NotificationsEnabled;
            showAdapterNameMenuItem.Checked = traySettings.ShowAdapterName;
            UpdateNotifyIcon(null, null, lastRefreshError);
            return;
        }

        if (lastConfiguration is null || lastStatus is null)
        {
            notifyIcon.Icon = trayIconProvider.GetIcon(TrayIconState.Default);
            notifyIcon.Text = "DnsSwitcher";
            return;
        }

        RebuildProfilesMenu(lastConfiguration, lastStatus);
        UpdateMenuState(lastConfiguration, lastStatus);
        UpdateNotifyIcon(lastConfiguration, lastStatus, error: null);
    }

    private static TrayIconState ResolveTrayIconState(DnsStatus status)
    {
        return status.Mode switch
        {
            DnsMode.Dhcp => TrayIconState.Dhcp,
            DnsMode.Manual when status.IsManaged => TrayIconState.Managed,
            DnsMode.Manual => TrayIconState.Warning,
            DnsMode.Mixed => TrayIconState.Warning,
            _ => TrayIconState.Default,
        };
    }

    private static string FormatServers(IReadOnlyList<string> servers)
    {
        return servers.Count == 0 ? "<none>" : string.Join(", ", servers);
    }

    private static string BuildDnsTestSummary(DnsTestResult result)
    {
        var averageLatency = result.AverageLatency is null
            ? "n/a"
            : $"{Math.Round(result.AverageLatency.Value.TotalMilliseconds, MidpointRounding.AwayFromZero):0} ms";

        return
            $"DNS test {result.Status}. " +
            $"Domains: {result.Domains.Count}. " +
            $"Average latency: {averageLatency}.";
    }

    private static string BuildSiteTestSummary(ConnectivityTestResult result)
    {
        var averageLatency = result.AverageLatency is null
            ? "n/a"
            : $"{Math.Round(result.AverageLatency.Value.TotalMilliseconds, MidpointRounding.AwayFromZero):0} ms";

        return
            $"Site test {result.Status}. " +
            $"URLs: {result.Urls.Count}. " +
            $"Average latency: {averageLatency}.";
    }

    private static string BuildSiteTestDetails(ConnectivityTestResult result)
    {
        var lines = new List<string>
        {
            $"Status: {result.Status}",
            $"Adapter: {result.AdapterName ?? "<none>"}",
            $"Profile: {FormatProfileLabel(result.ProfileName, result.ProfileId)}",
            $"Average latency: {FormatLatency(result.AverageLatency)}",
            string.Empty,
        };

        if (result.UrlResults.Count == 0)
        {
            lines.Add(result.Details);
            return string.Join(Environment.NewLine, lines);
        }

        foreach (var urlResult in result.UrlResults)
        {
            lines.Add($"{urlResult.Url}");
            lines.Add($"  Status: {urlResult.Status}");
            lines.Add($"  Attempts: {urlResult.SuccessfulAttempts}/{urlResult.TotalAttempts}");
            lines.Add($"  HTTP: {(urlResult.HttpStatusCode?.ToString() ?? "<none>")} via {urlResult.HttpMethod}");
            lines.Add($"  DNS: {urlResult.Dns.Details}");
            lines.Add($"  TCP: {urlResult.Connect.Details}");

            if (!string.Equals(urlResult.Tls.Details, "TLS not required.", StringComparison.Ordinal))
            {
                lines.Add($"  TLS: {urlResult.Tls.Details}");
            }

            lines.Add($"  HTTP details: {urlResult.Http.Details}");
            lines.Add($"  Summary: {urlResult.Details}");
            lines.Add(string.Empty);
        }

        lines.Add(result.Details);
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildDnsTestDetails(DnsTestResult result)
    {
        var lines = new List<string>
        {
            $"Status: {result.Status}",
            $"Adapter: {result.AdapterName ?? "<none>"}",
            $"Profile: {FormatProfileLabel(result.ProfileName, result.ProfileId)}",
            $"DNS servers: {(result.DnsServers.Count == 0 ? "<none>" : string.Join(", ", result.DnsServers))}",
            $"Average latency: {FormatLatency(result.AverageLatency)}",
            string.Empty,
            "Domains:",
        };

        if (result.DomainResults.Count == 0)
        {
            lines.Add("  <none>");
        }
        else
        {
            foreach (var domainResult in result.DomainResults)
            {
                lines.Add(
                    $"  {domainResult.Domain}: {domainResult.Status} | " +
                    $"{domainResult.SuccessfulAttempts}/{domainResult.TotalAttempts} | " +
                    $"avg {FormatLatency(domainResult.AverageLatency)}");
            }
        }

        lines.Add(string.Empty);
        lines.Add(result.Details);
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatLatency(TimeSpan? latency)
    {
        return latency is null
            ? "n/a"
            : $"{Math.Round(latency.Value.TotalMilliseconds, MidpointRounding.AwayFromZero):0} ms";
    }

    private static string FormatProfileLabel(string? profileName, string? profileId)
    {
        return string.IsNullOrWhiteSpace(profileId)
            ? "<none>"
            : string.IsNullOrWhiteSpace(profileName)
                ? profileId
                : $"{profileName} ({profileId})";
    }

    private TraySettings LoadTraySettingsOrDefault()
    {
        try
        {
            return traySettingsStore.LoadAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Tray settings could not be loaded. Default tray settings will be used.");
            return TraySettings.Default;
        }
    }

    private void ShowSuccess(string message)
    {
        if (!traySettings.NotificationsEnabled)
        {
            return;
        }

        notifyIcon.BalloonTipTitle = "DnsSwitcher";
        notifyIcon.BalloonTipText = message;
        notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        notifyIcon.ShowBalloonTip(2000);
    }

    private async Task UpdateTraySettingsAsync(TraySettings updatedSettings)
    {
        try
        {
            await traySettingsStore.SaveAsync(updatedSettings).ConfigureAwait(true);
            traySettings = updatedSettings;
            ApplyPresentationState();
        }
        catch (Exception exception)
        {
            ShowError("DnsSwitcher", exception.Message);
        }
    }

    private static void ShowError(string title, string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static void ShowInformation(string title, string message)
    {
        ResultDialog.ShowDialog(title, message);
    }
}
