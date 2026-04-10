using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Desktop;
using DnsSwitcher.Infrastructure.Windows.Presentation;
using DnsSwitcher.Infrastructure.Windows.Tray;
using Microsoft.Extensions.Logging;
using ToolStripProfessionalRenderer = System.Windows.Forms.ToolStripProfessionalRenderer;

namespace DnsSwitcher.Tray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private const int RefreshIntervalMilliseconds = 15000;

    private readonly WindowsDnsSwitcherHost host;
    private readonly ILogger<TrayApplicationContext> logger;
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip contextMenu;
    private readonly ToolStripMenuItem openUiMenuItem;
    private readonly ToolStripMenuItem statusMenuItem;
    private readonly ToolStripMenuItem adapterMenuItem;
    private readonly ToolStripMenuItem enableDnsMenuItem;
    private readonly ToolStripMenuItem disableDnsMenuItem;
    private readonly ToolStripMenuItem switchNextMenuItem;
    private readonly ToolStripMenuItem testsMenuItem;
    private readonly ToolStripMenuItem testDnsMenuItem;
    private readonly ToolStripMenuItem testSitesMenuItem;
    private readonly ToolStripMenuItem benchmarkMenuItem;
    private readonly ToolStripMenuItem profilesMenuItem;
    private readonly ToolStripMenuItem settingsMenuItem;
    private readonly ToolStripMenuItem themeMenuItem;
    private readonly ToolStripMenuItem systemThemeMenuItem;
    private readonly ToolStripMenuItem lightThemeMenuItem;
    private readonly ToolStripMenuItem darkThemeMenuItem;
    private readonly ToolStripMenuItem notificationsMenuItem;
    private readonly ToolStripMenuItem showAdapterNameMenuItem;
    private readonly ToolStripMenuItem exitMenuItem;
    private readonly System.Windows.Forms.Timer refreshTimer;
    private readonly TrayIconProvider trayIconProvider = new();
    private readonly DnsProfileSelectionService profileSelectionService = new();
    private readonly DesktopClientLauncher desktopClientLauncher;
    private readonly JsonAppPreferencesStore appPreferencesStore;
    private readonly JsonTraySettingsStore traySettingsStore;

    private string? preferredProfileId;
    private AppConfig? lastConfiguration;
    private DnsStatus? lastStatus;
    private Exception? lastRefreshError;
    private AppPreferences appPreferences = AppPreferences.Default;
    private AppLocalizer localizer = new(AppLanguage.System);
    private TraySettings traySettings = TraySettings.Default;
    private bool isRefreshing;
    private bool isActionInProgress;

    public TrayApplicationContext(WindowsDnsSwitcherHost host)
    {
        this.host = host;
        logger = host.LoggerFactory.CreateLogger<TrayApplicationContext>();
        desktopClientLauncher = new DesktopClientLauncher(host.LoggerFactory.CreateLogger<DesktopClientLauncher>());
        appPreferencesStore = new JsonAppPreferencesStore(host.Paths, host.LoggerFactory.CreateLogger<JsonAppPreferencesStore>());
        traySettingsStore = new JsonTraySettingsStore(host.Paths, host.LoggerFactory.CreateLogger<JsonTraySettingsStore>());
        appPreferences = LoadAppPreferencesOrDefault();
        localizer = new AppLocalizer(appPreferences.Language);
        traySettings = LoadTraySettingsOrDefault();

        openUiMenuItem = new ToolStripMenuItem(localizer["TrayOpenUi"]);
        statusMenuItem = new ToolStripMenuItem(localizer["TrayStatusLoading"])
        {
            Enabled = false,
        };
        adapterMenuItem = new ToolStripMenuItem(localizer["TrayAdapterLoading"])
        {
            Enabled = false,
            Visible = traySettings.ShowAdapterName,
        };

        enableDnsMenuItem = new ToolStripMenuItem(localizer["TrayEnableDns"]);
        disableDnsMenuItem = new ToolStripMenuItem(localizer["TrayDisableDns"]);
        switchNextMenuItem = new ToolStripMenuItem(localizer["TraySwitchNext"]);
        testsMenuItem = new ToolStripMenuItem(localizer["TrayTests"]);
        testDnsMenuItem = new ToolStripMenuItem(localizer["TrayTestDns"]);
        testSitesMenuItem = new ToolStripMenuItem(localizer["TrayTestSites"]);
        benchmarkMenuItem = new ToolStripMenuItem(localizer["TrayBenchmarkProfiles"]);
        profilesMenuItem = new ToolStripMenuItem(localizer["TrayShowProfiles"]);
        settingsMenuItem = new ToolStripMenuItem(localizer["TraySettings"]);
        themeMenuItem = new ToolStripMenuItem(localizer["SettingsThemeHeader"]);
        systemThemeMenuItem = new ToolStripMenuItem(localizer["ThemeSystemValue"]);
        lightThemeMenuItem = new ToolStripMenuItem(localizer["ThemeLightValue"]);
        darkThemeMenuItem = new ToolStripMenuItem(localizer["ThemeDarkValue"]);
        notificationsMenuItem = new ToolStripMenuItem(localizer["TrayShowNotifications"]);
        showAdapterNameMenuItem = new ToolStripMenuItem(localizer["TrayShowAdapterName"]);
        exitMenuItem = new ToolStripMenuItem(localizer["TrayExit"]);

        openUiMenuItem.Click += (_, _) => OpenUi();
        enableDnsMenuItem.Click += async (_, _) => await ExecuteActionAsync(EnableDnsAsync).ConfigureAwait(true);
        disableDnsMenuItem.Click += async (_, _) => await ExecuteActionAsync(DisableDnsAsync).ConfigureAwait(true);
        switchNextMenuItem.Click += async (_, _) => await ExecuteActionAsync(SwitchNextAsync).ConfigureAwait(true);
        testDnsMenuItem.Click += async (_, _) => await ExecuteActionAsync(TestDnsAsync).ConfigureAwait(true);
        testSitesMenuItem.Click += async (_, _) => await ExecuteActionAsync(TestSitesAsync).ConfigureAwait(true);
        benchmarkMenuItem.Click += async (_, _) => await ExecuteActionAsync(BenchmarkAsync).ConfigureAwait(true);
        systemThemeMenuItem.Click += async (_, _) => await UpdateThemePreferenceAsync(AppTheme.System).ConfigureAwait(true);
        lightThemeMenuItem.Click += async (_, _) => await UpdateThemePreferenceAsync(AppTheme.Light).ConfigureAwait(true);
        darkThemeMenuItem.Click += async (_, _) => await UpdateThemePreferenceAsync(AppTheme.Dark).ConfigureAwait(true);
        notificationsMenuItem.Click += async (_, _) => await ToggleNotificationsAsync().ConfigureAwait(true);
        showAdapterNameMenuItem.Click += async (_, _) => await ToggleAdapterVisibilityAsync().ConfigureAwait(true);
        exitMenuItem.Click += (_, _) => ExitThread();

        themeMenuItem.DropDownItems.Add(systemThemeMenuItem);
        themeMenuItem.DropDownItems.Add(lightThemeMenuItem);
        themeMenuItem.DropDownItems.Add(darkThemeMenuItem);
        settingsMenuItem.DropDownItems.Add(themeMenuItem);
        settingsMenuItem.DropDownItems.Add(notificationsMenuItem);
        settingsMenuItem.DropDownItems.Add(showAdapterNameMenuItem);
        testsMenuItem.DropDownItems.Add(testDnsMenuItem);
        testsMenuItem.DropDownItems.Add(testSitesMenuItem);
        testsMenuItem.DropDownItems.Add(benchmarkMenuItem);

        contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(openUiMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
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
        ApplyTheme(appPreferences.Theme);

        notifyIcon = new NotifyIcon
        {
            Icon = trayIconProvider.GetIcon(TrayIconState.Default),
            Text = localizer["DnsSwitcherTrayTitle"],
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
            appPreferences = await LoadAppPreferencesOrDefaultAsync().ConfigureAwait(true);
            localizer = new AppLocalizer(appPreferences.Language);
            ApplyTheme(appPreferences.Theme);
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
            logger.LogError(exception, "Tray state refresh failed.");
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
            ShowError(localizer["DnsSwitcherTrayTitle"], exception);
        }
        catch (Exception exception)
        {
            ShowError(localizer["DnsSwitcherTrayTitle"], exception);
        }
        finally
        {
            isActionInProgress = false;
            await RefreshStateCoreAsync().ConfigureAwait(true);
        }
    }

    private async Task EnableDnsAsync()
    {
        logger.LogInformation("Tray requested DNS enable.");
        var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
        var status = await host.DnsManager.GetStatusAsync().ConfigureAwait(true);

        SyncPreferredProfile(configuration, status);

        var profile = profileSelectionService.GetProfileToEnable(configuration, status, preferredProfileId)
            ?? throw new DnsOperationFailedException(localizer["TrayNoStaticProfiles"]);

        await host.AgentDnsSwitchService.ApplyProfileAsync(profile.Id).ConfigureAwait(true);

        preferredProfileId = profile.Id;
        ShowSuccess(localizer.Format("TrayDnsEnabledFormat", profile.Name));
    }

    private async Task DisableDnsAsync()
    {
        logger.LogInformation("Tray requested DHCP reset.");
        var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
        var status = await host.DnsManager.GetStatusAsync().ConfigureAwait(true);

        SyncPreferredProfile(configuration, status);

        await host.AgentDnsSwitchService.ResetToDhcpAsync().ConfigureAwait(true);
        ShowSuccess(localizer["TrayDnsDisabled"]);
    }

    private async Task SwitchNextAsync()
    {
        logger.LogInformation("Tray requested next DNS profile.");
        var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
        var status = await host.DnsManager.GetStatusAsync().ConfigureAwait(true);

        SyncPreferredProfile(configuration, status);

        var profile = profileSelectionService.GetNextProfile(configuration, status, preferredProfileId)
            ?? throw new DnsOperationFailedException(localizer["TrayNoStaticProfiles"]);

        await host.AgentDnsSwitchService.ApplyProfileAsync(profile.Id).ConfigureAwait(true);

        preferredProfileId = profile.Id;
        ShowSuccess(localizer.Format("TrayDnsSwitchedFormat", profile.Name));
    }

    private async Task ApplyProfileAsync(string profileId)
    {
        logger.LogInformation("Tray requested apply profile {ProfileId}.", profileId);
        await host.AgentDnsSwitchService.ApplyProfileAsync(profileId).ConfigureAwait(true);
        preferredProfileId = profileId;

        var profile = await host.ProfileService.GetRequiredProfileAsync(profileId).ConfigureAwait(true);
        ShowSuccess(localizer.Format("TrayDnsSwitchedFormat", profile.Name));
    }

    private async Task TestDnsAsync()
    {
        logger.LogInformation("Tray requested DNS test.");
        var result = await host.DnsTester.TestCurrentDnsAsync().ConfigureAwait(true);
        var summary = DiagnosticTextFormatter.BuildDnsBalloonSummary(result);

        if (!traySettings.NotificationsEnabled)
        {
            logger.LogInformation("Tray DNS test finished without balloon notification: {Summary}", summary);
            ShowInformation($"{localizer["DnsSwitcherTrayTitle"]} {localizer["DnsTestTitle"]}", DiagnosticTextFormatter.BuildDnsDetails(result));
            return;
        }

        notifyIcon.BalloonTipTitle = localizer["DnsSwitcherTrayTitle"];
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
        logger.LogInformation("Tray toggled notifications setting.");
        await UpdateTraySettingsAsync(traySettings with
        {
            NotificationsEnabled = !traySettings.NotificationsEnabled,
        }).ConfigureAwait(true);
    }

    private async Task ToggleAdapterVisibilityAsync()
    {
        logger.LogInformation("Tray toggled adapter visibility setting.");
        await UpdateTraySettingsAsync(traySettings with
        {
            ShowAdapterName = !traySettings.ShowAdapterName,
        }).ConfigureAwait(true);
    }

    private async Task ShowStatusDialogAsync()
    {
        try
        {
            appPreferences = await LoadAppPreferencesOrDefaultAsync().ConfigureAwait(true);
            localizer = new AppLocalizer(appPreferences.Language);
            var configuration = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
            var status = await host.DnsManager.GetStatusAsync().ConfigureAwait(true);

            SyncPreferredProfile(configuration, status);

            var lines = new List<string>
            {
                $"{localizer["TrayStatusLabel"]}: {TrayTextFormatter.BuildStatusLabel(configuration, status, localizer)}",
                $"{localizer["TrayAdapterLabel"]}: {status.AdapterName ?? localizer["NoneValue"]}",
                $"{localizer["TrayModeLabel"]}: {status.Mode}",
                $"{localizer["TrayMatchedProfileLabel"]}: {status.MatchedProfileId ?? localizer["NoneValue"]}",
                $"{localizer["TraySelectedProfileLabel"]}: {preferredProfileId ?? localizer["NoneValue"]}",
                $"{localizer["TrayIpv4Label"]}: {FormatServers(status.Ipv4.NameServers)}",
                $"{localizer["TrayIpv6Label"]}: {FormatServers(status.Ipv6.NameServers)}",
            };

            ShowInformation(
                localizer["DnsSwitcherTrayTitle"],
                string.Join(Environment.NewLine, lines));
        }
        catch (Exception exception)
        {
            ShowError(localizer["DnsSwitcherTrayTitle"], exception);
        }
    }

    private async Task TestSitesAsync()
    {
        logger.LogInformation("Tray requested site test.");
        var result = await host.ConnectivityTester.TestCurrentSitesAsync().ConfigureAwait(true);
        var summary = DiagnosticTextFormatter.BuildSiteBalloonSummary(result);

        if (!traySettings.NotificationsEnabled)
        {
            ShowInformation($"{localizer["DnsSwitcherTrayTitle"]} {localizer["SiteTestTitle"]}", DiagnosticTextFormatter.BuildSiteDetails(result));
            return;
        }

        notifyIcon.BalloonTipTitle = localizer["DnsSwitcherTrayTitle"];
        notifyIcon.BalloonTipText = summary;
        notifyIcon.BalloonTipIcon = result.Status is ConnectivityTestStatus.Blocked or ConnectivityTestStatus.Failed
            ? ToolTipIcon.Warning
            : result.Status == ConnectivityTestStatus.Slow
                ? ToolTipIcon.Warning
                : ToolTipIcon.Info;
        notifyIcon.ShowBalloonTip(2500);
    }

    private async Task BenchmarkAsync()
    {
        logger.LogInformation("Tray requested DNS benchmark.");
        var result = await host.DnsBenchmarkService.BenchmarkProfilesAsync().ConfigureAwait(true);
        var summary = DiagnosticTextFormatter.BuildBenchmarkBalloonSummary(result);

        if (!traySettings.NotificationsEnabled || result.WasInterrupted || !result.RestoreSucceeded)
        {
            ShowInformation($"{localizer["DnsSwitcherTrayTitle"]} {localizer["BenchmarkTitle"]}", DiagnosticTextFormatter.BuildBenchmarkDetails(result));
            return;
        }

        notifyIcon.BalloonTipTitle = localizer["DnsSwitcherTrayTitle"];
        notifyIcon.BalloonTipText = summary;
        notifyIcon.BalloonTipIcon = result.OverallStatus == DnsTestStatus.Failed
            ? ToolTipIcon.Error
            : result.OverallStatus == DnsTestStatus.Slow
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
            profilesMenuItem.DropDownItems.Add(new ToolStripMenuItem(localizer["TrayNoStaticProfiles"])
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
            var profileMenuItem = new ToolStripMenuItem(TrayTextFormatter.BuildProfileMenuText(profile, isCurrent, isPreferred, localizer))
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

        statusMenuItem.Text = TrayTextFormatter.BuildStatusMenuText(configuration, status, localizer);
        adapterMenuItem.Text = TrayTextFormatter.BuildAdapterMenuText(status, traySettings, localizer) ?? localizer["TrayHiddenAdapter"];
        adapterMenuItem.Visible = traySettings.ShowAdapterName;
        enableDnsMenuItem.Text = TrayTextFormatter.BuildEnableMenuText(enableProfile, localizer);
        switchNextMenuItem.Text = TrayTextFormatter.BuildSwitchNextMenuText(nextProfile, localizer);
        openUiMenuItem.Text = localizer["TrayOpenUi"];
        testsMenuItem.Text = localizer["TrayTests"];
        testDnsMenuItem.Text = localizer["TrayTestDns"];
        testSitesMenuItem.Text = localizer["TrayTestSites"];
        benchmarkMenuItem.Text = localizer["TrayBenchmarkProfiles"];
        profilesMenuItem.Text = localizer["TrayShowProfiles"];
        settingsMenuItem.Text = localizer["TraySettings"];
        themeMenuItem.Text = localizer["SettingsThemeHeader"];
        systemThemeMenuItem.Text = localizer["ThemeSystemValue"];
        lightThemeMenuItem.Text = localizer["ThemeLightValue"];
        darkThemeMenuItem.Text = localizer["ThemeDarkValue"];
        notificationsMenuItem.Text = localizer["TrayShowNotifications"];
        showAdapterNameMenuItem.Text = localizer["TrayShowAdapterName"];
        exitMenuItem.Text = localizer["TrayExit"];

        enableDnsMenuItem.Enabled = !isActionInProgress && enableProfile is not null;
        disableDnsMenuItem.Enabled = !isActionInProgress && status.Mode != DnsMode.Dhcp;
        switchNextMenuItem.Enabled = !isActionInProgress && nextProfile is not null;
        testsMenuItem.Enabled = !isActionInProgress;
        testDnsMenuItem.Enabled = !isActionInProgress;
        testSitesMenuItem.Enabled = !isActionInProgress;
        benchmarkMenuItem.Enabled = !isActionInProgress;
        profilesMenuItem.Enabled = !isActionInProgress && profileSelectionService.GetSwitchableProfiles(configuration).Count > 0;
        settingsMenuItem.Enabled = !isActionInProgress;
        themeMenuItem.Enabled = !isActionInProgress;
        systemThemeMenuItem.Checked = appPreferences.Theme == AppTheme.System;
        lightThemeMenuItem.Checked = appPreferences.Theme == AppTheme.Light;
        darkThemeMenuItem.Checked = appPreferences.Theme == AppTheme.Dark;
        notificationsMenuItem.Checked = traySettings.NotificationsEnabled;
        showAdapterNameMenuItem.Checked = traySettings.ShowAdapterName;
    }

    private void UpdateNotifyIcon(AppConfig? configuration, DnsStatus? status, Exception? error)
    {
        if (error is not null)
        {
            statusMenuItem.Text = localizer["TrayErrorStatus"];
            adapterMenuItem.Visible = false;
            notifyIcon.Icon = trayIconProvider.GetIcon(TrayIconState.Error);
            notifyIcon.Text = TrayTextFormatter.BuildErrorNotifyIconText(error.Message, localizer);
            return;
        }

        if (configuration is null || status is null)
        {
            notifyIcon.Icon = trayIconProvider.GetIcon(TrayIconState.Default);
            notifyIcon.Text = localizer["DnsSwitcherTrayTitle"];
            return;
        }

        notifyIcon.Icon = trayIconProvider.GetIcon(ResolveTrayIconState(status));
        notifyIcon.Text = TrayTextFormatter.BuildNotifyIconText(configuration, status, traySettings, localizer);
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
        statusMenuItem.Text = localizer["TrayBusyStatus"];
        adapterMenuItem.Visible = traySettings.ShowAdapterName;
        openUiMenuItem.Enabled = false;
        enableDnsMenuItem.Enabled = false;
        disableDnsMenuItem.Enabled = false;
        switchNextMenuItem.Enabled = false;
        testsMenuItem.Enabled = false;
        testDnsMenuItem.Enabled = false;
        testSitesMenuItem.Enabled = false;
        benchmarkMenuItem.Enabled = false;
        profilesMenuItem.Enabled = false;
        settingsMenuItem.Enabled = false;
    }

    private void ApplyPresentationState()
    {
        if (lastRefreshError is not null)
        {
            SetBusyMenuState();
            profilesMenuItem.DropDownItems.Clear();
            profilesMenuItem.DropDownItems.Add(new ToolStripMenuItem(localizer["TrayProfilesUnavailable"])
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
            notifyIcon.Text = localizer["DnsSwitcherTrayTitle"];
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

    private string FormatServers(IReadOnlyList<string> servers)
    {
        return servers.Count == 0 ? localizer["NoneValue"] : string.Join(", ", servers);
    }

    private async Task<AppPreferences> LoadAppPreferencesOrDefaultAsync()
    {
        try
        {
            return await appPreferencesStore.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "App preferences could not be loaded. Default preferences will be used.");
            return AppPreferences.Default;
        }
    }

    private AppPreferences LoadAppPreferencesOrDefault()
    {
        try
        {
            return appPreferencesStore.LoadAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "App preferences could not be loaded. Default preferences will be used.");
            return AppPreferences.Default;
        }
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

        notifyIcon.BalloonTipTitle = localizer["DnsSwitcherTrayTitle"];
        notifyIcon.BalloonTipText = message;
        notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        notifyIcon.ShowBalloonTip(2000);
    }

    private void OpenUi()
    {
        if (!desktopClientLauncher.EnsureUiRunning(AppContext.BaseDirectory))
        {
            ShowError(localizer["DnsSwitcherTrayTitle"], new InvalidOperationException(localizer["UiExecutableNotFound"]));
        }
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
            ShowError(localizer["DnsSwitcherTrayTitle"], exception);
        }
    }

    private async Task UpdateThemePreferenceAsync(AppTheme theme)
    {
        if (appPreferences.Theme == theme)
        {
            return;
        }

        try
        {
            appPreferences = appPreferences with
            {
                Theme = theme,
            };

            await appPreferencesStore.SaveAsync(appPreferences).ConfigureAwait(true);
            ApplyTheme(appPreferences.Theme);
            ApplyPresentationState();
        }
        catch (Exception exception)
        {
            ShowError(localizer["DnsSwitcherTrayTitle"], exception);
        }
    }

    private void ShowError(string title, Exception exception)
    {
        logger.LogError(exception, "Tray operation failed.");
        TrayDialogs.ShowError(
            title,
            FriendlyExceptionFormatter.ToUserMessage(exception),
            appPreferences.Theme);
    }

    private void ShowInformation(string title, string message)
    {
        TrayDialogs.ShowInformation(title, message, appPreferences.Theme);
    }

    private void ApplyTheme(AppTheme themePreference)
    {
        var palette = ThemeModeResolver.IsDarkTheme(themePreference)
            ? TrayThemePalette.Dark
            : TrayThemePalette.Light;

        var renderer = new ToolStripProfessionalRenderer(new TrayColorTable(palette));
        contextMenu.Renderer = renderer;
        contextMenu.BackColor = palette.BackgroundRaised;
        contextMenu.ForeColor = palette.Foreground;

        ApplyThemeToItems(contextMenu.Items, palette, renderer);
    }

    private static void ApplyThemeToItems(ToolStripItemCollection items, TrayThemePalette palette, ToolStripProfessionalRenderer renderer)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = palette.BackgroundRaised;
            item.ForeColor = item.Enabled ? palette.Foreground : palette.ForegroundMuted;

            if (item is ToolStripSeparator separator)
            {
                separator.BackColor = palette.BackgroundRaised;
                separator.ForeColor = palette.Border;
                continue;
            }

            if (item is ToolStripDropDownItem dropDownItem)
            {
                dropDownItem.DropDown.BackColor = palette.BackgroundRaised;
                dropDownItem.DropDown.ForeColor = palette.Foreground;
                dropDownItem.DropDown.Renderer = renderer;
                ApplyThemeToItems(dropDownItem.DropDownItems, palette, renderer);
            }
        }
    }
}
