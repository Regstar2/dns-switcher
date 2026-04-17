using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Agent;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Desktop;
using DnsSwitcher.Infrastructure.Windows.Presentation;
using DnsSwitcher.Infrastructure.Windows.Startup;
using DnsSwitcher.Ui.UiModels;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MediaBrush = System.Windows.Media.Brush;

namespace DnsSwitcher.Ui;

public partial class MainWindow : Window
{
    private const double CompactWidthThreshold = 760;
    private const double HideSelectedProfileHeightThreshold = 500;
    private const double HideCurrentStatusHeightThreshold = 380;
    private const string TrayAutostartValueName = "DnsSwitcherTray";
    private const string LegacyUiAutostartValueName = "DnsSwitcherUi";
    private static readonly TimeSpan PeriodicRefreshInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ConfigRefreshDebounceInterval = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherTimer periodicRefreshTimer;
    private readonly DispatcherTimer configRefreshDebounceTimer;
    private readonly ILogger<MainWindow> logger;
    private readonly JsonUiSettingsStore uiSettingsStore;
    private readonly JsonAppPreferencesStore appPreferencesStore;
    private readonly JsonDnsProfileExchangeService profileExchangeService;
    private readonly WindowsAutostartManager autostartManager;
    private readonly WindowsAutostartManager legacyUiAutostartManager;
    private readonly DesktopClientLauncher desktopClientLauncher;
    private bool suppressAdapterSelectionChanged;
    private bool suppressProfileSelectionChanged;
    private bool isBusy;
    private bool isRefreshingUi;
    private bool pendingExternalRefresh;
    private bool isInitialized;
    private bool allowExplicitClose;
    private DateTime lastProfilesWriteUtc = DateTime.MinValue;
    private FileSystemWatcher? profilesFileWatcher;
    private AppLocalizer localizer = new(AppLanguage.System);
    private AppPreferences appPreferences = AppPreferences.Default;
    private UiSettings uiSettings = UiSettings.Default;

    public MainWindow()
    {
        InitializeComponent();
        WindowThemeService.Attach(this);
        logger = App.Host.LoggerFactory.CreateLogger<MainWindow>();
        uiSettingsStore = new JsonUiSettingsStore(App.Host.Paths, App.Host.LoggerFactory.CreateLogger<JsonUiSettingsStore>());
        appPreferencesStore = new JsonAppPreferencesStore(App.Host.Paths, App.Host.LoggerFactory.CreateLogger<JsonAppPreferencesStore>());
        profileExchangeService = new JsonDnsProfileExchangeService();
        autostartManager = new WindowsAutostartManager(TrayAutostartValueName);
        legacyUiAutostartManager = new WindowsAutostartManager(LegacyUiAutostartValueName);
        desktopClientLauncher = new DesktopClientLauncher(App.Host.LoggerFactory.CreateLogger<DesktopClientLauncher>());
        periodicRefreshTimer = new DispatcherTimer { Interval = PeriodicRefreshInterval };
        periodicRefreshTimer.Tick += OnPeriodicRefreshTick;
        configRefreshDebounceTimer = new DispatcherTimer { Interval = ConfigRefreshDebounceInterval };
        configRefreshDebounceTimer.Tick += OnConfigRefreshDebounceTick;
        Loaded += OnLoaded;
        Closed += OnClosed;
        Closing += OnClosing;
        SizeChanged += OnWindowSizeChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        try
        {
            logger.LogInformation("Loading DnsSwitcher UI main window.");
            await App.Host.ProfileService.EnsureInitializedAsync().ConfigureAwait(true);
            appPreferences = await LoadAppPreferencesOrDefaultAsync().ConfigureAwait(true);
            localizer = new AppLocalizer(appPreferences.Language);
            uiSettings = await LoadUiSettingsOrDefaultAsync().ConfigureAwait(true);
            MigrateLegacyUiAutostartIfNeeded();
            ApplyLocalization();
            InitializeExternalRefresh();
            await RefreshUiAsync(localizer["UiLoadedStatus"]).ConfigureAwait(true);
            UpdateResponsiveLayout();
            isInitialized = true;
            await ShowAgentManagerOnFirstLaunchAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!allowExplicitClose && isInitialized && uiSettings.MinimizeToTray)
        {
            if (!TryContinueInTray())
            {
                e.Cancel = true;
            }
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        periodicRefreshTimer.Stop();
        configRefreshDebounceTimer.Stop();

        if (profilesFileWatcher is not null)
        {
            profilesFileWatcher.EnableRaisingEvents = false;
            profilesFileWatcher.Changed -= OnProfilesFileChanged;
            profilesFileWatcher.Created -= OnProfilesFileChanged;
            profilesFileWatcher.Deleted -= OnProfilesFileChanged;
            profilesFileWatcher.Renamed -= OnProfilesFileRenamed;
            profilesFileWatcher.Dispose();
            profilesFileWatcher = null;
        }

    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        logger.LogInformation("UI requested reload.");
        await RefreshUiAsync(localizer["ReloadedStatus"]).ConfigureAwait(true);
    }

    private async void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not ProfileListItem profileItem)
        {
            SetOperationStatus(localizer["SelectProfileFirstError"], isError: true);
            return;
        }

        logger.LogInformation("UI requested apply profile {ProfileId}.", profileItem.Id);

        await RunOperationAsync(
            async () =>
            {
                await App.Host.AgentDnsSwitchService
                    .ApplyProfileAsync(profileItem.Id, GetSelectedAdapterValue())
                    .ConfigureAwait(true);
            },
            localizer.Format("AppliedProfileFormat", profileItem.Name)).ConfigureAwait(true);
    }

    private async void OnTestCurrentDnsClicked(object sender, RoutedEventArgs e)
    {
        logger.LogInformation("UI requested DNS test.");
        await RunDnsTestAsync().ConfigureAwait(true);
    }

    private async void OnTestSitesClicked(object sender, RoutedEventArgs e)
    {
        logger.LogInformation("UI requested site test.");
        await RunSiteTestAsync().ConfigureAwait(true);
    }

    private async void OnBenchmarkClicked(object sender, RoutedEventArgs e)
    {
        logger.LogInformation("UI requested DNS benchmark.");
        await RunBenchmarkAsync().ConfigureAwait(true);
    }

    private async void OnHealthCheckClicked(object sender, RoutedEventArgs e)
    {
        logger.LogInformation("UI requested DNS health check.");
        await RunHealthCheckAsync().ConfigureAwait(true);
    }

    private async void OnHealthEnableClicked(object sender, RoutedEventArgs e)
    {
        await SetHealthMonitorEnabledAsync(enabled: true).ConfigureAwait(true);
    }

    private async void OnHealthDisableClicked(object sender, RoutedEventArgs e)
    {
        await SetHealthMonitorEnabledAsync(enabled: false).ConfigureAwait(true);
    }

    private void OnOpenChecksClicked(object sender, RoutedEventArgs e)
    {
        ChecksContextMenu.PlacementTarget = ChecksButton;
        ChecksContextMenu.IsOpen = true;
    }

    private void OnOpenMoreToolsClicked(object sender, RoutedEventArgs e)
    {
        MoreToolsContextMenu.PlacementTarget = MoreToolsButton;
        MoreToolsContextMenu.IsOpen = true;
    }

    private async void OnResetClicked(object sender, RoutedEventArgs e)
    {
        logger.LogInformation("UI requested DHCP reset.");
        await RunOperationAsync(
            async () =>
            {
                await App.Host.AgentDnsSwitchService
                    .ResetToDhcpAsync(GetSelectedAdapterValue())
                    .ConfigureAwait(true);
            },
            localizer["ResetSuccess"]).ConfigureAwait(true);
    }

    private async void OnAdapterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressAdapterSelectionChanged || !isInitialized)
        {
            return;
        }

        await PersistSelectionStateAsync().ConfigureAwait(true);
        await RefreshUiAsync(localizer["AdapterChangedStatus"]).ConfigureAwait(true);
    }

    private async void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressProfileSelectionChanged)
        {
            return;
        }

        UpdateSelectedProfilePanel(ProfilesListBox.SelectedItem as ProfileListItem);
        UpdateActionButtons();

        if (!isInitialized)
        {
            return;
        }

        await PersistSelectionStateAsync().ConfigureAwait(true);
    }

    private async void OnCreateProfileClicked(object sender, RoutedEventArgs e)
    {
        await OpenProfileEditorAsync(profile: null, previousProfileId: null).ConfigureAwait(true);
    }

    private async void OnEditProfileClicked(object sender, RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not ProfileListItem profileItem)
        {
            SetOperationStatus(localizer["SelectProfileToEditError"], isError: true);
            return;
        }

        await OpenProfileEditorAsync(profileItem.Profile, profileItem.Id).ConfigureAwait(true);
    }

    private async void OnDeleteProfileClicked(object sender, RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not ProfileListItem profileItem)
        {
            SetOperationStatus(localizer["SelectProfileToDeleteError"], isError: true);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            localizer.Format("DeleteProfileConfirmFormat", profileItem.Name),
            localizer["DeleteProfileTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await RunOperationAsync(
            async () =>
            {
                await App.Host.ProfileService.DeleteProfileAsync(profileItem.Id).ConfigureAwait(true);
                await PrepareSelectedProfileAsync(null).ConfigureAwait(true);
            },
            localizer["ProfileDeletedStatus"]).ConfigureAwait(true);
    }

    private async void OnImportProfilesClicked(object sender, RoutedEventArgs e)
    {
        if (isBusy)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = localizer["ImportProfilesDialogTitle"],
            Filter = localizer["JsonFilesFilter"],
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await RunOperationAsync(
            async () =>
            {
                var importedProfiles = await profileExchangeService.ImportProfilesAsync(dialog.FileName).ConfigureAwait(true);
                var importedCount = await App.Host.ProfileService.ImportProfilesAsync(importedProfiles).ConfigureAwait(true);
                var selectedProfileId = importedProfiles.LastOrDefault()?.Id;

                if (!string.IsNullOrWhiteSpace(selectedProfileId))
                {
                    await PrepareSelectedProfileAsync(selectedProfileId).ConfigureAwait(true);
                }

                SetOperationStatus(localizer.Format("ProfilesImportedFormat", importedCount), isError: false);
            },
            successMessage: string.Empty).ConfigureAwait(true);
    }

    private async void OnExportProfileClicked(object sender, RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not ProfileListItem profileItem)
        {
            SetOperationStatus(localizer["SelectProfileToExportError"], isError: true);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = localizer["ExportProfileDialogTitle"],
            Filter = localizer["JsonFilesFilter"],
            FileName = $"{profileItem.Id}.json",
            AddExtension = true,
            DefaultExt = ".json",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await RunOperationAsync(
            async () =>
            {
                await profileExchangeService.ExportProfileAsync(dialog.FileName, profileItem.Profile).ConfigureAwait(true);
            },
            localizer["ProfileExportedStatus"]).ConfigureAwait(true);
    }

    private async void OnSplitDnsStatusClicked(object sender, RoutedEventArgs e)
    {
        await ShowSplitDnsStatusAsync().ConfigureAwait(true);
    }

    private async void OnSplitDnsApplyClicked(object sender, RoutedEventArgs e)
    {
        await RunSplitDnsApplyAsync().ConfigureAwait(true);
    }

    private async void OnSplitDnsResetClicked(object sender, RoutedEventArgs e)
    {
        await RunSplitDnsResetAsync().ConfigureAwait(true);
    }

    private void OnOpenConfigFolderClicked(object sender, RoutedEventArgs e)
    {
        OpenFolder(App.Host.Paths.ConfigDirectory, localizer["OpenedConfigFolder"]);
    }

    private void OnOpenLogsFolderClicked(object sender, RoutedEventArgs e)
    {
        OpenFolder(App.Host.Paths.LogDirectory, localizer["OpenedLogsFolder"]);
    }

    private async void OnOpenSettingsClicked(object sender, RoutedEventArgs e)
    {
        if (isBusy)
        {
            return;
        }

        try
        {
            var settingsWindow = new SettingsWindow(
                localizer,
                appPreferences.Language,
                appPreferences.Theme,
                IsTrayAutostartEnabled(),
                uiSettings.MinimizeToTray,
                App.IsDarkThemeActive)
            {
                Owner = this,
            };
            settingsWindow.AgentManagerRequested += async (_, _) =>
            {
                await OpenAgentManagerAsync(settingsWindow).ConfigureAwait(true);
            };
            settingsWindow.HealthSettingsRequested += async (_, _) =>
            {
                await OpenHealthSettingsAsync(settingsWindow).ConfigureAwait(true);
            };
            settingsWindow.SplitDnsSettingsRequested += async (_, _) =>
            {
                await OpenSplitDnsRulesAsync(settingsWindow).ConfigureAwait(true);
            };

            if (settingsWindow.ShowDialog() != true)
            {
                return;
            }

            await ApplySettingsAsync(settingsWindow).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
    }

    private async Task OpenAgentManagerAsync(Window owner)
    {
        try
        {
            var window = new AgentManagerWindow(App.Host, localizer)
            {
                Owner = owner,
            };
            window.ShowDialog();
            await RefreshUiAsync(showBusyMessage: false, showErrorDialog: false, preserveOperationStatus: true, disableControls: false).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
    }

    private async Task OpenHealthSettingsAsync(Window owner)
    {
        if (isBusy)
        {
            return;
        }

        try
        {
            var settings = await App.Host.DnsHealthFailoverService.GetSettingsAsync().ConfigureAwait(true);
            var state = await App.Host.DnsHealthFailoverService.GetStateAsync().ConfigureAwait(true);
            var configuration = await App.Host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
            var window = new HealthFailoverSettingsWindow(localizer, settings, state, configuration.Profiles)
            {
                Owner = owner,
            };

            if (window.ShowDialog() != true)
            {
                return;
            }

            await RunOperationAsync(
                async () =>
                {
                    await App.Host.DnsHealthFailoverService.SaveSettingsAsync(window.EditedSettings).ConfigureAwait(true);

                    if (window.RunCheckRequested)
                    {
                        var result = await App.Host.DnsHealthFailoverService.EvaluateAsync(GetSelectedAdapterValue()).ConfigureAwait(true);
                        SetOperationStatus($"Health: {result.Status}. {result.Details}", result.Status == DnsHealthStatus.Failed);
                    }
                },
                window.RunCheckRequested ? string.Empty : localizer["HealthSettingsSavedStatus"]).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
    }

    private async Task OpenSplitDnsRulesAsync(Window owner)
    {
        if (isBusy)
        {
            return;
        }

        try
        {
            var window = new SplitDnsRulesWindow(App.Host, localizer)
            {
                Owner = owner,
            };
            window.ShowDialog();
            await RefreshUiAsync(showBusyMessage: false, showErrorDialog: false, preserveOperationStatus: true, disableControls: false).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout();
    }

    private async void OnPeriodicRefreshTick(object? sender, EventArgs e)
    {
        await TryRefreshExternalChangesAsync(requireConfigChange: false).ConfigureAwait(true);
    }

    private async void OnConfigRefreshDebounceTick(object? sender, EventArgs e)
    {
        configRefreshDebounceTimer.Stop();
        await TryRefreshExternalChangesAsync(requireConfigChange: true).ConfigureAwait(true);
    }

    private async Task RefreshUiAsync(
        string? successMessage = null,
        bool showBusyMessage = true,
        bool showErrorDialog = true,
        bool preserveOperationStatus = false,
        bool disableControls = true)
    {
        if (isBusy || isRefreshingUi)
        {
            return;
        }

        var selectedProfileId = GetSelectedProfileId() ?? uiSettings.LastSelectedProfileId;
        var selectedAdapterValue = GetSelectedAdapterValue() ?? uiSettings.LastAdapterId;
        isRefreshingUi = true;

        if (disableControls)
        {
            SetBusyState(true, showBusyMessage);
        }

        try
        {
            var configuration = await App.Host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
            var activeProfile = await App.Host.ProfileService.GetActiveProfileAsync().ConfigureAwait(true);
            var adapters = await App.Host.NetworkAdapterService.GetAdaptersAsync().ConfigureAwait(true);
            var defaultAdapter = await App.Host.NetworkAdapterService.GetDefaultAdapterAsync().ConfigureAwait(true);

            var resolvedAdapterSelection = ResolveAdapterSelectionValue(selectedAdapterValue, adapters);
            var dnsStatus = await App.Host.DnsManager.GetStatusAsync(resolvedAdapterSelection).ConfigureAwait(true);
            var agentServiceStatus = await App.Host.AgentServiceManager.GetStatusAsync().ConfigureAwait(true);
            var agentAvailable = await App.Host.AgentDnsSwitchService.IsAgentAvailableAsync().ConfigureAwait(true);
            var healthSettings = await App.Host.DnsHealthFailoverService.GetSettingsAsync().ConfigureAwait(true);
            var healthState = await App.Host.DnsHealthFailoverService.GetStateAsync().ConfigureAwait(true);
            var splitDnsConfiguration = await App.Host.SplitDnsRuleService.GetConfigurationAsync().ConfigureAwait(true);

            RebuildAdapterOptions(adapters, defaultAdapter, resolvedAdapterSelection);
            RebuildProfiles(configuration, dnsStatus, activeProfile, selectedProfileId);
            UpdateStatusPanel(
                configuration,
                activeProfile,
                dnsStatus,
                agentServiceStatus,
                agentAvailable,
                healthSettings,
                healthState,
                splitDnsConfiguration);
            UpdateActionButtons();
            pendingExternalRefresh = false;
            lastProfilesWriteUtc = GetProfilesLastWriteUtc();
            await PersistSelectionStateAsync().ConfigureAwait(true);

            if (!preserveOperationStatus && !string.IsNullOrWhiteSpace(successMessage))
            {
                SetOperationStatus(successMessage, isError: false);
            }
        }
        catch (Exception exception)
        {
            HandleException(exception, showErrorDialog);
        }
        finally
        {
            if (disableControls)
            {
                SetBusyState(false);
            }

            isRefreshingUi = false;
        }
    }

    private async Task RunOperationAsync(Func<Task> action, string successMessage)
    {
        if (isBusy)
        {
            return;
        }

        SetBusyState(true);

        try
        {
            await action().ConfigureAwait(true);
            SetBusyState(false);
            await RefreshUiAsync(successMessage).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            HandleException(exception);
            SetBusyState(false);
        }
    }

    private async Task OpenProfileEditorAsync(DnsProfile? profile, string? previousProfileId)
    {
        if (isBusy)
        {
            return;
        }

        try
        {
            var editorWindow = new ProfileEditorWindow(localizer, profile)
            {
                Owner = this,
            };

            if (editorWindow.ShowDialog() != true)
            {
                return;
            }

            var editedProfile = editorWindow.EditedProfile;

            await RunOperationAsync(
                async () =>
                {
                    await App.Host.ProfileService
                        .SaveProfileAsync(editedProfile, previousProfileId)
                        .ConfigureAwait(true);
                    await PrepareSelectedProfileAsync(editedProfile.Id).ConfigureAwait(true);
                },
                profile is null ? localizer["ProfileCreatedStatus"] : localizer["ProfileUpdatedStatus"]).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
    }

    private void RebuildAdapterOptions(
        IReadOnlyList<NetworkAdapter> adapters,
        NetworkAdapter? defaultAdapter,
        string? selectedAdapterValue)
    {
        var options = new List<AdapterOption>
        {
            new()
            {
                DisplayName = defaultAdapter is null
                    ? localizer["AutomaticNoDefaultAdapter"]
                    : localizer.Format("AutomaticAdapterFormat", defaultAdapter.Name),
                SelectionValue = null,
                Adapter = defaultAdapter,
            },
        };

        options.AddRange(adapters
            .OrderBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase)
            .Select(adapter => new AdapterOption
            {
                DisplayName = BuildAdapterDisplayName(adapter),
                SelectionValue = adapter.Id,
                Adapter = adapter,
            }));

        suppressAdapterSelectionChanged = true;
        AdapterComboBox.ItemsSource = options;
        AdapterComboBox.SelectedItem = options.FirstOrDefault(option =>
            string.Equals(option.SelectionValue, selectedAdapterValue, StringComparison.OrdinalIgnoreCase))
            ?? options[0];
        suppressAdapterSelectionChanged = false;
    }

    private void RebuildProfiles(
        AppConfig configuration,
        DnsStatus status,
        DnsProfile? activeProfile,
        string? selectedProfileId)
    {
        var profileItems = configuration.Profiles
            .Select(profile => CreateProfileListItem(configuration, status, activeProfile, profile))
            .OrderBy(item => item.Profile.Mode == ProfileMode.Dhcp ? 1 : 0)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selectedItem = profileItems.FirstOrDefault(item =>
            string.Equals(item.Id, selectedProfileId, StringComparison.OrdinalIgnoreCase))
            ?? profileItems.FirstOrDefault(item =>
                string.Equals(item.Id, status.MatchedProfileId, StringComparison.OrdinalIgnoreCase))
            ?? profileItems.FirstOrDefault(item =>
                string.Equals(item.Id, configuration.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
            ?? profileItems.FirstOrDefault();

        suppressProfileSelectionChanged = true;
        ProfilesListBox.ItemsSource = profileItems;
        ProfilesListBox.SelectedItem = selectedItem;
        suppressProfileSelectionChanged = false;
        UpdateSelectedProfilePanel(selectedItem);
    }

    private void UpdateStatusPanel(
        AppConfig configuration,
        DnsProfile? activeProfile,
        DnsStatus status,
        AgentServiceStatus agentServiceStatus,
        bool agentAvailable,
        DnsHealthSettings healthSettings,
        DnsHealthState healthState,
        SplitDnsConfiguration splitDnsConfiguration)
    {
        CurrentProfileValueTextBlock.Text = GetCurrentProfileText(configuration, status);
        ConfigActiveProfileValueTextBlock.Text = activeProfile is null
            ? localizer["NoneValue"]
            : $"{activeProfile.Name} ({activeProfile.Id})";
        SelectedAdapterValueTextBlock.Text = status.AdapterName ?? localizer["NoneValue"];
        DnsModeValueTextBlock.Text = status.Mode.ToString();
        AgentServiceStatusValueTextBlock.Text = agentServiceStatus.ToString();
        AgentAvailableValueTextBlock.Text = agentAvailable ? localizer["YesValue"] : localizer["NoValue"];
        HealthMonitorValueTextBlock.Text = $"{(healthSettings.Enabled ? localizer["EnabledValue"] : localizer["DisabledValue"])} ({healthState.Status})";
        SplitDnsValueTextBlock.Text = $"{(splitDnsConfiguration.Enabled ? localizer["EnabledValue"] : localizer["DisabledValue"])} ({splitDnsConfiguration.Rules.Count} rule(s))";
        Ipv4ValueTextBlock.Text = FormatServers(status.Ipv4.NameServers);
        Ipv6ValueTextBlock.Text = FormatServers(status.Ipv6.NameServers);
    }

    private void UpdateSelectedProfilePanel(ProfileListItem? item)
    {
        if (item is null)
        {
            SelectedProfileNameTextBlock.Text = localizer["NoneValue"];
            SelectedProfileSummaryTextBlock.Text = localizer["NoneValue"];
            SelectedProfileTagsTextBlock.Text = localizer["NoneValue"];
            return;
        }

        SelectedProfileNameTextBlock.Text = $"{item.Name} ({item.Id})";
        SelectedProfileSummaryTextBlock.Text = item.SummaryText;
        SelectedProfileTagsTextBlock.Text = item.Profile.Tags.Count == 0
            ? localizer["NoneValue"]
            : string.Join(", ", item.Profile.Tags);
    }

    private void UpdateActionButtons()
    {
        var hasProfileSelection = ProfilesListBox.SelectedItem is ProfileListItem;
        var hasAdapterOptions = AdapterComboBox.Items.Count > 0;

        CreateProfileButton.IsEnabled = !isBusy;
        EditProfileButton.IsEnabled = !isBusy && hasProfileSelection;
        DeleteProfileButton.IsEnabled = !isBusy && hasProfileSelection;
        ApplyButton.IsEnabled = !isBusy && hasProfileSelection;
        ResetButton.IsEnabled = !isBusy && hasAdapterOptions;
        ReloadButton.IsEnabled = !isBusy;
        SettingsButton.IsEnabled = !isBusy;
        OpenConfigButton.IsEnabled = !isBusy;
        OpenLogsButton.IsEnabled = !isBusy;
        ChecksButton.IsEnabled = !isBusy;
        TestDnsMenuItem.IsEnabled = !isBusy;
        TestSitesMenuItem.IsEnabled = !isBusy;
        MoreToolsButton.IsEnabled = !isBusy;
        BenchmarkMenuItem.IsEnabled = !isBusy;
        HealthCheckMenuItem.IsEnabled = !isBusy;
        HealthEnableMenuItem.IsEnabled = !isBusy;
        HealthDisableMenuItem.IsEnabled = !isBusy;
        ImportProfilesMenuItem.IsEnabled = !isBusy;
        ExportProfileMenuItem.IsEnabled = !isBusy && hasProfileSelection;
        SplitDnsStatusMenuItem.IsEnabled = !isBusy;
        SplitDnsApplyMenuItem.IsEnabled = !isBusy;
        SplitDnsResetMenuItem.IsEnabled = !isBusy;
        AdapterComboBox.IsEnabled = !isBusy;
        ProfilesListBox.IsEnabled = !isBusy;
    }

    private void UpdateResponsiveLayout()
    {
        var isWidthCompact = ActualWidth < CompactWidthThreshold;
        var hideSelectedProfile = ActualHeight < HideSelectedProfileHeightThreshold;
        var hideCurrentStatus = ActualHeight < HideCurrentStatusHeightThreshold;

        DetailsPanel.Visibility = isWidthCompact ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetColumnSpan(ProfilesGroupBox, isWidthCompact ? 2 : 1);
        ProfilesGroupBox.Margin = isWidthCompact ? new Thickness(0) : new Thickness(0, 0, 12, 0);

        if (isWidthCompact)
        {
            BottomActionsPanel.Visibility = Visibility.Collapsed;
            Grid.SetColumnSpan(OperationStatusBorder, 2);
            OperationStatusBorder.Margin = new Thickness(0);
            return;
        }

        BottomActionsPanel.Visibility = Visibility.Visible;
        Grid.SetColumnSpan(OperationStatusBorder, 1);
        OperationStatusBorder.Margin = new Thickness(0, 0, 6, 0);
        AdapterGroupBox.Visibility = Visibility.Visible;
        CurrentStatusGroupBox.Visibility = hideCurrentStatus ? Visibility.Collapsed : Visibility.Visible;
        SelectedProfileGroupBox.Visibility = hideSelectedProfile ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetBusyState(bool value, bool showBusyMessage = true)
    {
        isBusy = value;

        if (value && showBusyMessage)
        {
            SetOperationStatus(localizer["WorkingStatus"], isError: false);
        }

        UpdateActionButtons();
    }

    private void SetOperationStatus(string message, bool isError)
    {
        OperationStatusBorder.Background = isError ? GetBrushResource("ErrorStatusBrush") : GetBrushResource("InfoStatusBrush");
        OperationStatusTextBlock.Foreground = isError ? GetBrushResource("ErrorTextBrush") : GetBrushResource("PrimaryTextBrush");
        OperationStatusTextBlock.Text = message;
    }

    private void HandleException(Exception exception, bool showDialog = true)
    {
        var message = FriendlyExceptionFormatter.ToUserMessage(exception);
        logger.LogError(exception, "UI operation failed.");

        SetOperationStatus(message, isError: true);

        if (showDialog)
        {
            System.Windows.MessageBox.Show(
                message,
                localizer["AppTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private string BuildAdapterDisplayName(NetworkAdapter adapter)
    {
        var labels = new List<string>();

        if (adapter.IsActive)
        {
            labels.Add(localizer["ActiveLabel"]);
        }

        if (adapter.HasDefaultGateway)
        {
            labels.Add(localizer["GatewayLabel"]);
        }

        if (adapter.IsPhysical)
        {
            labels.Add(localizer["PhysicalLabel"]);
        }

        if (adapter.IsLoopback)
        {
            labels.Add(localizer["LoopbackLabel"]);
        }

        labels.Add(adapter.SupportedStacks.ToString());

        return labels.Count == 0
            ? adapter.Name
            : $"{adapter.Name} [{string.Join(", ", labels)}]";
    }

    private ProfileListItem CreateProfileListItem(
        AppConfig configuration,
        DnsStatus status,
        DnsProfile? activeProfile,
        DnsProfile profile)
    {
        var flags = new List<string>();

        if (string.Equals(profile.Id, status.MatchedProfileId, StringComparison.OrdinalIgnoreCase))
        {
            flags.Add(localizer["CurrentFlag"]);
        }

        if (string.Equals(profile.Id, configuration.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
        {
            flags.Add(localizer["ConfiguredFlag"]);
        }

        if (activeProfile is not null
            && string.Equals(profile.Id, activeProfile.Id, StringComparison.OrdinalIgnoreCase)
            && !flags.Contains(localizer["ConfiguredFlag"], StringComparer.OrdinalIgnoreCase))
        {
            flags.Add(localizer["ConfiguredFlag"]);
        }

        var summaryParts = new List<string>
        {
            $"ID: {profile.Id}",
            $"IPv4: {FormatServers(profile.Ipv4)}",
        };

        if (profile.Ipv6.Count > 0)
        {
            summaryParts.Add($"IPv6: {FormatServers(profile.Ipv6)}");
        }

        return new ProfileListItem
        {
            Profile = profile,
            StatusText = flags.Count == 0 ? localizer["AvailableProfile"] : string.Join(" | ", flags),
            SummaryText = string.Join(Environment.NewLine, summaryParts),
        };
    }

    private string GetCurrentProfileText(AppConfig configuration, DnsStatus status)
    {
        var matchedProfile = configuration.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, status.MatchedProfileId, StringComparison.OrdinalIgnoreCase));

        if (matchedProfile is not null)
        {
            return $"{matchedProfile.Name} ({matchedProfile.Id})";
        }

        return status.Mode switch
        {
            DnsMode.Dhcp => localizer["AutomaticDhcpStatus"],
            DnsMode.Manual => localizer["CustomManualDnsStatus"],
            DnsMode.Mixed => localizer["CustomMixedDnsStatus"],
            _ => localizer["UnknownValue"],
        };
    }

    private string? GetSelectedAdapterValue()
    {
        return (AdapterComboBox.SelectedItem as AdapterOption)?.SelectionValue;
    }

    private string? GetSelectedProfileId()
    {
        return (ProfilesListBox.SelectedItem as ProfileListItem)?.Id;
    }

    private async Task PersistSelectionStateAsync()
    {
        var updatedSettings = uiSettings with
        {
            LastAdapterId = GetSelectedAdapterValue(),
            LastSelectedProfileId = GetSelectedProfileId(),
        };

        if (updatedSettings == uiSettings)
        {
            return;
        }

        await PersistUiSettingsAsync(updatedSettings).ConfigureAwait(true);
    }

    private async Task PrepareSelectedProfileAsync(string? profileId)
    {
        suppressProfileSelectionChanged = true;
        ProfilesListBox.SelectedItem = null;
        suppressProfileSelectionChanged = false;
        await PersistUiSettingsAsync(uiSettings with { LastSelectedProfileId = profileId }).ConfigureAwait(true);
    }

    private async Task PersistUiSettingsAsync(UiSettings updatedSettings, string? successMessage = null)
    {
        if (updatedSettings == uiSettings)
        {
            return;
        }

        await uiSettingsStore.SaveAsync(updatedSettings).ConfigureAwait(true);
        uiSettings = updatedSettings;

        if (!string.IsNullOrWhiteSpace(successMessage))
        {
            SetOperationStatus(successMessage, isError: false);
        }
    }

    private async Task<UiSettings> LoadUiSettingsOrDefaultAsync()
    {
        try
        {
            return await uiSettingsStore.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "UI settings could not be loaded. Default UI settings will be used.");
            return UiSettings.Default;
        }
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

    private async Task ShowAgentManagerOnFirstLaunchAsync()
    {
        if (uiSettings.AgentManagerShownOnFirstLaunch)
        {
            return;
        }

        await PersistUiSettingsAsync(uiSettings with { AgentManagerShownOnFirstLaunch = true }).ConfigureAwait(true);
        await OpenAgentManagerAsync(this).ConfigureAwait(true);
    }

    private void ApplyLocalization()
    {
        Title = localizer["AppTitle"];
        ProfilesGroupBox.Header = localizer["ProfilesHeader"];
        AdapterGroupBox.Header = localizer["AdapterAndAppHeader"];
        CurrentStatusGroupBox.Header = localizer["CurrentStatusHeader"];
        SelectedProfileGroupBox.Header = localizer["SelectedProfileHeader"];

        ApplyButton.Content = localizer["ApplyProfileButton"];
        ResetButton.Content = localizer["RestoreAutoDnsButton"];
        ChecksButton.Content = localizer["ChecksButton"];
        TestDnsMenuItem.Header = localizer["CheckDnsButton"];
        TestSitesMenuItem.Header = localizer["CheckSitesButton"];
        MoreToolsButton.Content = localizer["ImportExportButton"];
        BenchmarkMenuItem.Header = localizer["BenchmarkButton"];
        HealthCheckMenuItem.Header = localizer["HealthCheckButton"];
        HealthEnableMenuItem.Header = localizer["HealthEnableButton"];
        HealthDisableMenuItem.Header = localizer["HealthDisableButton"];
        ImportProfilesMenuItem.Header = localizer["ImportProfilesButton"];
        ExportProfileMenuItem.Header = localizer["ExportProfileButton"];
        SplitDnsStatusMenuItem.Header = localizer["SplitDnsStatusButton"];
        SplitDnsApplyMenuItem.Header = localizer["SplitDnsApplyButton"];
        SplitDnsResetMenuItem.Header = localizer["SplitDnsResetButton"];
        CreateProfileButton.Content = localizer["CreateProfileButton"];
        EditProfileButton.Content = localizer["EditProfileButton"];
        DeleteProfileButton.Content = localizer["DeleteProfileButton"];
        ReloadButton.Content = localizer["ReloadButton"];
        SettingsButton.Content = localizer["SettingsButton"];
        OpenConfigButton.Content = localizer["OpenConfigButton"];
        OpenLogsButton.Content = localizer["OpenLogsButton"];

        CurrentProfileLabelTextBlock.Text = localizer["CurrentProfileLabel"];
        ConfigActiveProfileLabelTextBlock.Text = localizer["ConfigActiveProfileLabel"];
        SelectedAdapterLabelTextBlock.Text = localizer["SelectedAdapterLabel"];
        DnsModeLabelTextBlock.Text = localizer["DnsModeLabel"];
        AgentServiceLabelTextBlock.Text = localizer["AgentServiceLabel"];
        AgentAvailableLabelTextBlock.Text = localizer["AgentAvailableLabel"];
        HealthMonitorLabelTextBlock.Text = localizer["HealthMonitorLabel"];
        SplitDnsLabelTextBlock.Text = localizer["SplitDnsLabel"];
        Ipv4LabelTextBlock.Text = localizer["Ipv4Label"];
        Ipv6LabelTextBlock.Text = localizer["Ipv6Label"];
        ProfileLabelTextBlock.Text = localizer["ProfileLabel"];
        SummaryLabelTextBlock.Text = localizer["SummaryLabel"];
        TagsLabelTextBlock.Text = localizer["TagsLabel"];

        if (string.IsNullOrWhiteSpace(OperationStatusTextBlock.Text) || OperationStatusTextBlock.Text == "Ready.")
        {
            SetOperationStatus(localizer["ReadyStatus"], isError: false);
        }
    }

    private void InitializeExternalRefresh()
    {
        if (profilesFileWatcher is not null)
        {
            return;
        }

        var profilesFilePath = App.Host.Paths.ProfilesFilePath;
        var configDirectory = Path.GetDirectoryName(profilesFilePath);
        var profilesFileName = Path.GetFileName(profilesFilePath);

        if (string.IsNullOrWhiteSpace(configDirectory) || string.IsNullOrWhiteSpace(profilesFileName))
        {
            return;
        }

        profilesFileWatcher = new FileSystemWatcher(configDirectory, profilesFileName)
        {
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.CreationTime
                | NotifyFilters.Size,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
        };

        profilesFileWatcher.Changed += OnProfilesFileChanged;
        profilesFileWatcher.Created += OnProfilesFileChanged;
        profilesFileWatcher.Deleted += OnProfilesFileChanged;
        profilesFileWatcher.Renamed += OnProfilesFileRenamed;

        lastProfilesWriteUtc = GetProfilesLastWriteUtc();
        periodicRefreshTimer.Start();
    }

    private void OnProfilesFileChanged(object sender, FileSystemEventArgs e)
    {
        ScheduleExternalRefresh();
    }

    private void OnProfilesFileRenamed(object sender, RenamedEventArgs e)
    {
        ScheduleExternalRefresh();
    }

    private void ScheduleExternalRefresh()
    {
        pendingExternalRefresh = true;

        Dispatcher.InvokeAsync(() =>
        {
            configRefreshDebounceTimer.Stop();
            configRefreshDebounceTimer.Start();
        });
    }

    private async Task TryRefreshExternalChangesAsync(bool requireConfigChange)
    {
        if (!isInitialized || isBusy)
        {
            return;
        }

        var profilesWriteUtc = GetProfilesLastWriteUtc();
        var profilesChanged = pendingExternalRefresh || profilesWriteUtc > lastProfilesWriteUtc;

        if (requireConfigChange && !profilesChanged)
        {
            return;
        }

        await RefreshUiAsync(
            showBusyMessage: false,
            showErrorDialog: false,
            preserveOperationStatus: true,
            disableControls: false).ConfigureAwait(true);
    }

    private async Task RunDnsTestAsync()
    {
        if (isBusy)
        {
            return;
        }

        SetBusyState(true);

        try
        {
            var result = await App.Host.DnsTester.TestCurrentDnsAsync(GetSelectedAdapterValue()).ConfigureAwait(true);
            SetBusyState(false, showBusyMessage: false);
            SetOperationStatus(DiagnosticTextFormatter.BuildDnsStatusSummary(result), isError: result.Status == DnsTestStatus.Failed);
        }
        catch (Exception exception)
        {
            HandleException(exception);
            SetBusyState(false, showBusyMessage: false);
        }
    }

    private async Task RunSiteTestAsync()
    {
        if (isBusy)
        {
            return;
        }

        SetBusyState(true);

        try
        {
            var result = await App.Host.ConnectivityTester.TestCurrentSitesAsync(GetSelectedAdapterValue()).ConfigureAwait(true);
            SetBusyState(false, showBusyMessage: false);
            SetOperationStatus(DiagnosticTextFormatter.BuildSiteStatusSummary(result), isError: result.Status is ConnectivityTestStatus.Blocked or ConnectivityTestStatus.Failed);
            TextResultWindow.ShowDialog(
                $"{localizer["AppTitle"]} {localizer["SiteTestTitle"]}",
                DiagnosticTextFormatter.BuildSiteDetails(result),
                this);
        }
        catch (Exception exception)
        {
            HandleException(exception);
            SetBusyState(false, showBusyMessage: false);
        }
    }

    private async Task RunBenchmarkAsync()
    {
        if (isBusy)
        {
            return;
        }

        SetBusyState(true);

        try
        {
            var result = await App.Host.DnsBenchmarkService.BenchmarkProfilesAsync(GetSelectedAdapterValue()).ConfigureAwait(true);
            SetBusyState(false, showBusyMessage: false);
            await RefreshUiAsync(
                showBusyMessage: false,
                showErrorDialog: false,
                preserveOperationStatus: true,
                disableControls: false).ConfigureAwait(true);
            SetOperationStatus(
                DiagnosticTextFormatter.BuildBenchmarkStatusSummary(result),
                isError: result.OverallStatus == DnsTestStatus.Failed || !result.RestoreSucceeded);
            TextResultWindow.ShowDialog(
                $"{localizer["AppTitle"]} {localizer["BenchmarkTitle"]}",
                DiagnosticTextFormatter.BuildBenchmarkDetails(result),
                this);
        }
        catch (Exception exception)
        {
            HandleException(exception);
            SetBusyState(false, showBusyMessage: false);
        }
    }

    private async Task RunHealthCheckAsync()
    {
        if (isBusy)
        {
            return;
        }

        SetBusyState(true);

        try
        {
            var result = await App.Host.DnsHealthFailoverService.EvaluateAsync(GetSelectedAdapterValue()).ConfigureAwait(true);
            SetBusyState(false, showBusyMessage: false);
            await RefreshUiAsync(
                showBusyMessage: false,
                showErrorDialog: false,
                preserveOperationStatus: true,
                disableControls: false).ConfigureAwait(true);
            SetOperationStatus(
                $"Health: {result.Status}. {result.Details}",
                isError: result.Status == DnsHealthStatus.Failed);
            TextResultWindow.ShowDialog(
                $"{localizer["AppTitle"]} {localizer["HealthCheckTitle"]}",
                BuildHealthDetails(result),
                this);
        }
        catch (Exception exception)
        {
            HandleException(exception);
            SetBusyState(false, showBusyMessage: false);
        }
    }

    private async Task SetHealthMonitorEnabledAsync(bool enabled)
    {
        await RunOperationAsync(
            async () =>
            {
                var settings = await App.Host.DnsHealthFailoverService.GetSettingsAsync().ConfigureAwait(true);
                await App.Host.DnsHealthFailoverService.SaveSettingsAsync(settings with { Enabled = enabled }).ConfigureAwait(true);
            },
            enabled ? localizer["HealthEnabledStatus"] : localizer["HealthDisabledStatus"]).ConfigureAwait(true);
    }

    private async Task ShowSplitDnsStatusAsync()
    {
        if (isBusy)
        {
            return;
        }

        try
        {
            var configuration = await App.Host.SplitDnsRuleService.GetConfigurationAsync().ConfigureAwait(true);
            TextResultWindow.ShowDialog(
                $"{localizer["AppTitle"]} {localizer["SplitDnsTitle"]}",
                BuildSplitDnsDetails(configuration),
                this);
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
    }

    private async Task RunSplitDnsApplyAsync()
    {
        await RunOperationAsync(
            async () =>
            {
                var configuration = await App.Host.SplitDnsRuleService.GetConfigurationAsync().ConfigureAwait(true);
                await App.Host.AgentSplitDnsService.ApplyAsync(configuration).ConfigureAwait(true);
            },
            localizer["SplitDnsAppliedStatus"]).ConfigureAwait(true);
    }

    private async Task RunSplitDnsResetAsync()
    {
        await RunOperationAsync(
            async () =>
            {
                await App.Host.AgentSplitDnsService.ResetAsync().ConfigureAwait(true);
            },
            localizer["SplitDnsResetStatus"]).ConfigureAwait(true);
    }

    private string BuildHealthDetails(DnsHealthEvaluationResult result)
    {
        return
            $"{localizer["HealthStateStatusLine"]} {result.Status}{Environment.NewLine}" +
            $"{localizer["HealthResultSwitchedProfileLine"]} {(result.SwitchedProfile ? localizer["YesValue"] : localizer["NoValue"])}{Environment.NewLine}" +
            $"{localizer["HealthStateActiveProfileLine"]} {result.ActiveProfileId ?? localizer["NoneValue"]}{Environment.NewLine}" +
            $"{localizer["HealthResultTargetProfileLine"]} {result.TargetProfileId ?? localizer["NoneValue"]}{Environment.NewLine}" +
            $"{localizer["HealthStateLastActionLine"]} {result.State.LastAction ?? localizer["NoneValue"]}{Environment.NewLine}" +
            $"{localizer["HealthStateFailureReasonLine"]} {result.State.LastFailureReason ?? localizer["NoneValue"]}{Environment.NewLine}" +
            $"{localizer["HealthStateLastCheckedLine"]} {result.State.LastCheckedUtc?.ToString("O") ?? localizer["NeverValue"]}{Environment.NewLine}" +
            $"{localizer["HealthStateCooldownLine"]} {result.State.CooldownUntilUtc?.ToString("O") ?? localizer["NoneValue"]}{Environment.NewLine}" +
            $"{Environment.NewLine}{result.Details}";
    }

    private string BuildSplitDnsDetails(SplitDnsConfiguration configuration)
    {
        var lines = new List<string>
        {
            $"{localizer["SplitDnsEnabledLine"]} {(configuration.Enabled ? localizer["YesValue"] : localizer["NoValue"])}",
            $"{localizer["SplitDnsModeLine"]} {configuration.Mode}",
            $"{localizer["SplitDnsDefaultBehaviorLine"]} {configuration.DefaultBehavior}",
            $"{localizer["SplitDnsRulesLine"]} {configuration.Rules.Count}",
            string.Empty,
        };

        foreach (var rule in configuration.Rules
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Namespace, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add(
                $"{rule.Id}: {rule.Namespace} -> {rule.ProfileId} | " +
                $"enabled={rule.Enabled} priority={rule.Priority}" +
                $"{(string.IsNullOrWhiteSpace(rule.Comment) ? string.Empty : $" | {rule.Comment}")}");
        }

        if (configuration.Rules.Count == 0)
        {
            lines.Add(localizer["SplitDnsNoRulesConfigured"]);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string? ResolveAdapterSelectionValue(string? selectionValue, IReadOnlyList<NetworkAdapter> adapters)
    {
        if (string.IsNullOrWhiteSpace(selectionValue))
        {
            return null;
        }

        return adapters.Any(adapter => string.Equals(adapter.Id, selectionValue, StringComparison.OrdinalIgnoreCase))
            ? selectionValue
            : null;
    }

    private string FormatServers(IReadOnlyList<string> servers)
    {
        return servers.Count == 0 ? localizer["NoneValue"] : string.Join(", ", servers);
    }

    private DateTime GetProfilesLastWriteUtc()
    {
        var profilesFilePath = App.Host.Paths.ProfilesFilePath;
        return File.Exists(profilesFilePath)
            ? File.GetLastWriteTimeUtc(profilesFilePath)
            : DateTime.MinValue;
    }

    private string GetTrayExecutablePath()
    {
        return DesktopClientLayout.TryGetTrayExecutablePath(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(localizer["TrayExecutableNotFound"]);
    }

    private bool IsTrayAutostartEnabled()
    {
        var trayExecutablePath = DesktopClientLayout.TryGetTrayExecutablePath(AppContext.BaseDirectory);
        return trayExecutablePath is not null
            ? autostartManager.IsEnabled(trayExecutablePath)
            : !string.IsNullOrWhiteSpace(autostartManager.GetCommandLine());
    }

    private void MigrateLegacyUiAutostartIfNeeded()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(legacyUiAutostartManager.GetCommandLine()))
            {
                return;
            }

            legacyUiAutostartManager.Disable();
            logger.LogInformation("Removed legacy UI autostart entry in favor of tray autostart.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to remove legacy UI autostart entry.");
        }
    }

    private void OpenFolder(string folderPath, string successMessage)
    {
        try
        {
            Directory.CreateDirectory(folderPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true,
                Verb = "open",
            });

            logger.LogInformation("Opened folder {FolderPath}.", folderPath);
            SetOperationStatus(successMessage, isError: false);
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
    }

    private bool TryContinueInTray()
    {
        try
        {
            if (!desktopClientLauncher.EnsureTrayRunning(AppContext.BaseDirectory))
            {
                SetOperationStatus(localizer["TrayExecutableNotFound"], isError: true);
                return false;
            }

            allowExplicitClose = true;
            logger.LogInformation("UI is closing and leaving the tray client running.");
            return true;
        }
        catch (Exception exception)
        {
            HandleException(exception);
            return false;
        }
    }

    private async Task ApplySettingsAsync(SettingsWindow settingsWindow)
    {
        appPreferences = appPreferences with
        {
            Language = settingsWindow.SelectedLanguage,
            Theme = settingsWindow.SelectedTheme,
        };

        await appPreferencesStore.SaveAsync(appPreferences).ConfigureAwait(true);
        localizer = new AppLocalizer(appPreferences.Language);
        ApplyLocalization();
        App.SetThemePreference(appPreferences.Theme);

        if (settingsWindow.StartWithWindowsEnabled)
        {
            autostartManager.Enable(GetTrayExecutablePath());
            legacyUiAutostartManager.Disable();
        }
        else
        {
            autostartManager.Disable();
            legacyUiAutostartManager.Disable();
        }

        var updatedSettings = uiSettings with
        {
            MinimizeToTray = settingsWindow.ContinueInTrayEnabled,
        };

        if (updatedSettings != uiSettings)
        {
            await PersistUiSettingsAsync(updatedSettings).ConfigureAwait(true);
        }

        await RefreshUiAsync(
            preserveOperationStatus: true,
            showBusyMessage: false,
            disableControls: false).ConfigureAwait(true);
        SetOperationStatus(localizer["SettingsSaved"], isError: false);
    }

    private MediaBrush GetBrushResource(string key)
    {
        return (MediaBrush)FindResource(key);
    }

}
