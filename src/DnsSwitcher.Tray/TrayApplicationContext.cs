using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows;

namespace DnsSwitcher.Tray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private const int RefreshIntervalMilliseconds = 15000;
    private const int MaxTrayTextLength = 63;

    private readonly WindowsDnsSwitcherHost host;
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip contextMenu;
    private readonly ToolStripMenuItem statusMenuItem;
    private readonly ToolStripMenuItem enableDnsMenuItem;
    private readonly ToolStripMenuItem disableDnsMenuItem;
    private readonly ToolStripMenuItem switchNextMenuItem;
    private readonly ToolStripMenuItem profilesMenuItem;
    private readonly ToolStripMenuItem exitMenuItem;
    private readonly System.Windows.Forms.Timer refreshTimer;
    private readonly TrayIconProvider trayIconProvider = new();
    private readonly DnsProfileSelectionService profileSelectionService = new();

    private string? preferredProfileId;
    private bool isRefreshing;
    private bool isActionInProgress;

    public TrayApplicationContext(WindowsDnsSwitcherHost host)
    {
        this.host = host;

        statusMenuItem = new ToolStripMenuItem("Status: loading...")
        {
            Enabled = false,
        };

        enableDnsMenuItem = new ToolStripMenuItem("Enable DNS");
        disableDnsMenuItem = new ToolStripMenuItem("Disable DNS");
        switchNextMenuItem = new ToolStripMenuItem("Switch Next");
        profilesMenuItem = new ToolStripMenuItem("Show Profiles");
        exitMenuItem = new ToolStripMenuItem("Exit");

        enableDnsMenuItem.Click += async (_, _) => await ExecuteActionAsync(EnableDnsAsync).ConfigureAwait(true);
        disableDnsMenuItem.Click += async (_, _) => await ExecuteActionAsync(DisableDnsAsync).ConfigureAwait(true);
        switchNextMenuItem.Click += async (_, _) => await ExecuteActionAsync(SwitchNextAsync).ConfigureAwait(true);
        exitMenuItem.Click += (_, _) => ExitThread();

        contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(statusMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(enableDnsMenuItem);
        contextMenu.Items.Add(disableDnsMenuItem);
        contextMenu.Items.Add(switchNextMenuItem);
        contextMenu.Items.Add(profilesMenuItem);
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
            RebuildProfilesMenu(configuration, status);
            UpdateMenuState(configuration, status);
            UpdateNotifyIcon(configuration, status, error: null);
        }
        catch (Exception exception)
        {
            SetBusyMenuState();
            profilesMenuItem.DropDownItems.Clear();
            profilesMenuItem.DropDownItems.Add(new ToolStripMenuItem("Unable to load profiles")
            {
                Enabled = false,
            });
            UpdateNotifyIcon(null, null, exception);
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

    private async Task ShowStatusDialogAsync()
    {
        try
        {
            var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
            var status = await host.DnsManager.GetStatusAsync().ConfigureAwait(true);

            SyncPreferredProfile(configuration, status);

            var lines = new List<string>
            {
                BuildStatusText(configuration, status),
                $"Adapter: {status.AdapterName ?? "<none>"}",
                $"Mode: {status.Mode}",
                $"Matched profile: {status.MatchedProfileId ?? "<none>"}",
                $"Selected profile: {preferredProfileId ?? "<none>"}",
                $"IPv4 DNS: {FormatServers(status.Ipv4.NameServers)}",
                $"IPv6 DNS: {FormatServers(status.Ipv6.NameServers)}",
            };

            MessageBox.Show(
                string.Join(Environment.NewLine, lines),
                "DnsSwitcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            ShowError("DnsSwitcher", exception.Message);
        }
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

            var menuText = isCurrent
                ? $"{profile.Name} [active]"
                : isPreferred
                    ? $"{profile.Name} [selected]"
                    : profile.Name;

            var profileMenuItem = new ToolStripMenuItem(menuText)
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

        statusMenuItem.Text = BuildStatusText(configuration, status);
        enableDnsMenuItem.Text = enableProfile is null
            ? "Enable DNS"
            : $"Enable DNS ({enableProfile.Name})";
        switchNextMenuItem.Text = nextProfile is null
            ? "Switch Next"
            : $"Switch Next ({nextProfile.Name})";

        enableDnsMenuItem.Enabled = !isActionInProgress && enableProfile is not null;
        disableDnsMenuItem.Enabled = !isActionInProgress && status.Mode != DnsMode.Dhcp;
        switchNextMenuItem.Enabled = !isActionInProgress && nextProfile is not null;
        profilesMenuItem.Enabled = !isActionInProgress && profileSelectionService.GetSwitchableProfiles(configuration).Count > 0;
    }

    private void UpdateNotifyIcon(AppConfig? configuration, DnsStatus? status, Exception? error)
    {
        if (error is not null)
        {
            statusMenuItem.Text = "Status: error";
            notifyIcon.Icon = trayIconProvider.GetIcon(TrayIconState.Error);
            notifyIcon.Text = TrimTrayText($"DnsSwitcher: error - {error.Message}");
            return;
        }

        if (configuration is null || status is null)
        {
            notifyIcon.Icon = trayIconProvider.GetIcon(TrayIconState.Default);
            notifyIcon.Text = "DnsSwitcher";
            return;
        }

        notifyIcon.Icon = trayIconProvider.GetIcon(ResolveTrayIconState(status));
        notifyIcon.Text = TrimTrayText(BuildStatusText(configuration, status));
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
        enableDnsMenuItem.Enabled = false;
        disableDnsMenuItem.Enabled = false;
        switchNextMenuItem.Enabled = false;
        profilesMenuItem.Enabled = false;
    }

    private static string BuildStatusText(AppConfig configuration, DnsStatus status)
    {
        var currentProfile = configuration.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, status.MatchedProfileId, StringComparison.OrdinalIgnoreCase));

        var label = currentProfile?.Name ?? status.Mode switch
        {
            DnsMode.Dhcp => "DHCP",
            DnsMode.Manual => "Manual DNS",
            DnsMode.Mixed => "Mixed DNS",
            _ => "Unknown",
        };

        var adapterName = status.AdapterName ?? "no adapter selected";
        return $"Status: {label} | {adapterName}";
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

    private static string TrimTrayText(string value)
    {
        if (value.Length <= MaxTrayTextLength)
        {
            return value;
        }

        return value[..(MaxTrayTextLength - 3)] + "...";
    }

    private void ShowSuccess(string message)
    {
        notifyIcon.BalloonTipTitle = "DnsSwitcher";
        notifyIcon.BalloonTipText = message;
        notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        notifyIcon.ShowBalloonTip(2000);
    }

    private static void ShowError(string title, string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
