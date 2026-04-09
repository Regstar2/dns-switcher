using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using DnsSwitcher.Infrastructure.Windows.Agent;
using DnsSwitcher.Ui.UiModels;

namespace DnsSwitcher.Ui;

public partial class MainWindow : Window
{
    private const double CompactWidthThreshold = 760;
    private const double HideSelectedProfileHeightThreshold = 500;
    private const double HideCurrentStatusHeightThreshold = 380;
    private static readonly TimeSpan PeriodicRefreshInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ConfigRefreshDebounceInterval = TimeSpan.FromMilliseconds(500);

    private static readonly Brush InfoStatusBrush = new SolidColorBrush(Color.FromRgb(239, 246, 255));
    private static readonly Brush ErrorStatusBrush = new SolidColorBrush(Color.FromRgb(254, 226, 226));
    private static readonly Brush ErrorTextBrush = new SolidColorBrush(Color.FromRgb(153, 27, 27));
    private static readonly Brush NormalTextBrush = new SolidColorBrush(Color.FromRgb(17, 24, 39));

    private readonly DispatcherTimer periodicRefreshTimer;
    private readonly DispatcherTimer configRefreshDebounceTimer;
    private bool suppressAdapterSelectionChanged;
    private bool isBusy;
    private bool isRefreshingUi;
    private bool pendingExternalRefresh;
    private DateTime lastProfilesWriteUtc = DateTime.MinValue;
    private FileSystemWatcher? profilesFileWatcher;

    public MainWindow()
    {
        InitializeComponent();
        periodicRefreshTimer = new DispatcherTimer { Interval = PeriodicRefreshInterval };
        periodicRefreshTimer.Tick += OnPeriodicRefreshTick;
        configRefreshDebounceTimer = new DispatcherTimer { Interval = ConfigRefreshDebounceInterval };
        configRefreshDebounceTimer.Tick += OnConfigRefreshDebounceTick;
        Loaded += OnLoaded;
        Closed += OnClosed;
        SizeChanged += OnWindowSizeChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        try
        {
            await App.Host.ProfileService.EnsureInitializedAsync().ConfigureAwait(true);
            InitializeExternalRefresh();
            await RefreshUiAsync("UI loaded.").ConfigureAwait(true);
            UpdateResponsiveLayout();
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        periodicRefreshTimer.Stop();
        configRefreshDebounceTimer.Stop();

        if (profilesFileWatcher is null)
        {
            return;
        }

        profilesFileWatcher.EnableRaisingEvents = false;
        profilesFileWatcher.Changed -= OnProfilesFileChanged;
        profilesFileWatcher.Created -= OnProfilesFileChanged;
        profilesFileWatcher.Deleted -= OnProfilesFileChanged;
        profilesFileWatcher.Renamed -= OnProfilesFileRenamed;
        profilesFileWatcher.Dispose();
        profilesFileWatcher = null;
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        await RefreshUiAsync("Configuration and status reloaded from disk and system state.").ConfigureAwait(true);
    }

    private async void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not ProfileListItem profileItem)
        {
            SetOperationStatus("Select a DNS profile first.", isError: true);
            return;
        }

        await RunOperationAsync(
            async () =>
            {
                await App.Host.AgentDnsSwitchService
                    .ApplyProfileAsync(profileItem.Id, GetSelectedAdapterValue())
                    .ConfigureAwait(true);
            },
            $"Applied profile '{profileItem.Name}'.").ConfigureAwait(true);
    }

    private async void OnTestCurrentDnsClicked(object sender, RoutedEventArgs e)
    {
        await RunDnsTestAsync().ConfigureAwait(true);
    }

    private async void OnResetClicked(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(
            async () =>
            {
                await App.Host.AgentDnsSwitchService
                    .ResetToDhcpAsync(GetSelectedAdapterValue())
                    .ConfigureAwait(true);
            },
            "DNS settings were reset to DHCP.").ConfigureAwait(true);
    }

    private async void OnAdapterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressAdapterSelectionChanged || !IsLoaded)
        {
            return;
        }

        await RefreshUiAsync("Adapter selection changed.").ConfigureAwait(true);
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

    private void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectedProfilePanel(ProfilesListBox.SelectedItem as ProfileListItem);
        UpdateActionButtons();
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

        var selectedProfileId = (ProfilesListBox.SelectedItem as ProfileListItem)?.Id;
        var selectedAdapterValue = GetSelectedAdapterValue();
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

            RebuildAdapterOptions(adapters, defaultAdapter, resolvedAdapterSelection);
            RebuildProfiles(configuration, dnsStatus, activeProfile, selectedProfileId);
            UpdateStatusPanel(configuration, activeProfile, dnsStatus, agentServiceStatus, agentAvailable);
            UpdateActionButtons();
            pendingExternalRefresh = false;
            lastProfilesWriteUtc = GetProfilesLastWriteUtc();

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
                    ? "Automatic (no default adapter)"
                    : $"Automatic ({defaultAdapter.Name})",
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

        ProfilesListBox.ItemsSource = profileItems;

        var selectedItem = profileItems.FirstOrDefault(item =>
            string.Equals(item.Id, selectedProfileId, StringComparison.OrdinalIgnoreCase))
            ?? profileItems.FirstOrDefault(item =>
                string.Equals(item.Id, status.MatchedProfileId, StringComparison.OrdinalIgnoreCase))
            ?? profileItems.FirstOrDefault(item =>
                string.Equals(item.Id, configuration.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
            ?? profileItems.FirstOrDefault();

        ProfilesListBox.SelectedItem = selectedItem;
        UpdateSelectedProfilePanel(selectedItem);
    }

    private void UpdateStatusPanel(
        AppConfig configuration,
        DnsProfile? activeProfile,
        DnsStatus status,
        AgentServiceStatus agentServiceStatus,
        bool agentAvailable)
    {
        CurrentProfileValueTextBlock.Text = GetCurrentProfileText(configuration, status);
        ConfigActiveProfileValueTextBlock.Text = activeProfile is null
            ? "<none>"
            : $"{activeProfile.Name} ({activeProfile.Id})";
        SelectedAdapterValueTextBlock.Text = status.AdapterName ?? "<none>";
        DnsModeValueTextBlock.Text = status.Mode.ToString();
        AgentServiceStatusValueTextBlock.Text = agentServiceStatus.ToString();
        AgentAvailableValueTextBlock.Text = agentAvailable ? "Yes" : "No";
        Ipv4ValueTextBlock.Text = FormatServers(status.Ipv4.NameServers);
        Ipv6ValueTextBlock.Text = FormatServers(status.Ipv6.NameServers);
    }

    private void UpdateSelectedProfilePanel(ProfileListItem? item)
    {
        if (item is null)
        {
            SelectedProfileNameTextBlock.Text = "<none>";
            SelectedProfileSummaryTextBlock.Text = "<none>";
            SelectedProfileTagsTextBlock.Text = "<none>";
            return;
        }

        SelectedProfileNameTextBlock.Text = $"{item.Name} ({item.Id})";
        SelectedProfileSummaryTextBlock.Text = item.SummaryText;
        SelectedProfileTagsTextBlock.Text = item.Profile.Tags.Count == 0
            ? "<none>"
            : string.Join(", ", item.Profile.Tags);
    }

    private void UpdateActionButtons()
    {
        var hasProfileSelection = ProfilesListBox.SelectedItem is ProfileListItem;
        var hasAdapterOptions = AdapterComboBox.Items.Count > 0;

        ApplyButton.IsEnabled = !isBusy && hasProfileSelection;
        ResetButton.IsEnabled = !isBusy && hasAdapterOptions;
        ReloadButton.IsEnabled = !isBusy;
        TestDnsButton.IsEnabled = !isBusy;
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
            return;
        }

        AdapterGroupBox.Visibility = Visibility.Visible;
        CurrentStatusGroupBox.Visibility = hideCurrentStatus ? Visibility.Collapsed : Visibility.Visible;
        SelectedProfileGroupBox.Visibility = hideSelectedProfile ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetBusyState(bool value, bool showBusyMessage = true)
    {
        isBusy = value;

        if (value && showBusyMessage)
        {
            SetOperationStatus("Working...", isError: false);
        }

        UpdateActionButtons();
    }

    private void SetOperationStatus(string message, bool isError)
    {
        OperationStatusBorder.Background = isError ? ErrorStatusBrush : InfoStatusBrush;
        OperationStatusTextBlock.Foreground = isError ? ErrorTextBrush : NormalTextBrush;
        OperationStatusTextBlock.Text = message;
    }

    private void HandleException(Exception exception, bool showDialog = true)
    {
        var message = exception switch
        {
            AppConfigValidationException validationException => BuildValidationMessage(validationException),
            InvalidDataException => exception.Message,
            DnsProfileNotFoundException => exception.Message,
            NetworkAdapterNotFoundException => exception.Message,
            NetworkAdapterDisabledException => exception.Message,
            DnsAgentUnavailableException => exception.Message,
            DnsOperationRequiresAdminException => exception.Message,
            DnsOperationFailedException => exception.Message,
            _ => $"Unexpected error: {exception.Message}",
        };

        SetOperationStatus(message, isError: true);

        if (showDialog)
        {
            MessageBox.Show(
                message,
                "DnsSwitcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string BuildValidationMessage(AppConfigValidationException exception)
    {
        return "profiles.json is invalid:" + Environment.NewLine + string.Join(
            Environment.NewLine,
            exception.Errors.Select(error => $"- {error.Path}: {error.Message} ({error.Code})"));
    }

    private static string BuildAdapterDisplayName(NetworkAdapter adapter)
    {
        var labels = new List<string>();

        if (adapter.IsActive)
        {
            labels.Add("active");
        }

        if (adapter.HasDefaultGateway)
        {
            labels.Add("gateway");
        }

        if (adapter.IsPhysical)
        {
            labels.Add("physical");
        }

        if (adapter.IsLoopback)
        {
            labels.Add("loopback");
        }

        labels.Add(adapter.SupportedStacks.ToString());

        return labels.Count == 0
            ? adapter.Name
            : $"{adapter.Name} [{string.Join(", ", labels)}]";
    }

    private static ProfileListItem CreateProfileListItem(
        AppConfig configuration,
        DnsStatus status,
        DnsProfile? activeProfile,
        DnsProfile profile)
    {
        var flags = new List<string>();

        if (string.Equals(profile.Id, status.MatchedProfileId, StringComparison.OrdinalIgnoreCase))
        {
            flags.Add("current");
        }

        if (string.Equals(profile.Id, configuration.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
        {
            flags.Add("configured");
        }

        if (activeProfile is not null
            && string.Equals(profile.Id, activeProfile.Id, StringComparison.OrdinalIgnoreCase)
            && !flags.Contains("configured", StringComparer.OrdinalIgnoreCase))
        {
            flags.Add("configured");
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
            StatusText = flags.Count == 0 ? "Available profile" : string.Join(" | ", flags),
            SummaryText = string.Join(Environment.NewLine, summaryParts),
        };
    }

    private static string GetCurrentProfileText(AppConfig configuration, DnsStatus status)
    {
        var matchedProfile = configuration.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, status.MatchedProfileId, StringComparison.OrdinalIgnoreCase));

        if (matchedProfile is not null)
        {
            return $"{matchedProfile.Name} ({matchedProfile.Id})";
        }

        return status.Mode switch
        {
            DnsMode.Dhcp => "Automatic / DHCP",
            DnsMode.Manual => "Custom manual DNS",
            DnsMode.Mixed => "Custom mixed DNS",
            _ => "<unknown>",
        };
    }

    private string? GetSelectedAdapterValue()
    {
        return (AdapterComboBox.SelectedItem as AdapterOption)?.SelectionValue;
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
        if (!IsLoaded || isBusy)
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
            SetOperationStatus(BuildDnsTestSummary(result), isError: result.Status == DnsTestStatus.Failed);
        }
        catch (Exception exception)
        {
            HandleException(exception);
            SetBusyState(false, showBusyMessage: false);
        }
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

    private static string FormatServers(IReadOnlyList<string> servers)
    {
        return servers.Count == 0 ? "<none>" : string.Join(", ", servers);
    }

    private DateTime GetProfilesLastWriteUtc()
    {
        var profilesFilePath = App.Host.Paths.ProfilesFilePath;
        return File.Exists(profilesFilePath)
            ? File.GetLastWriteTimeUtc(profilesFilePath)
            : DateTime.MinValue;
    }

    private static string BuildDnsTestSummary(DnsTestResult result)
    {
        var parts = new List<string>
        {
            $"DNS test {result.Status}",
            $"domains: {result.Domains.Count}",
            $"servers: {result.DnsServers.Count}",
            $"avg latency: {FormatLatency(result.AverageLatency)}",
        };

        if (result.DomainResults.Count > 0)
        {
            parts.Add(string.Join(
                "; ",
                result.DomainResults.Select(domainResult =>
                    $"{domainResult.Domain}: {domainResult.Status} ({domainResult.SuccessfulAttempts}/{domainResult.TotalAttempts})")));
        }

        return string.Join(" | ", parts);
    }

    private static string FormatLatency(TimeSpan? latency)
    {
        return latency is null
            ? "n/a"
            : $"{Math.Round(latency.Value.TotalMilliseconds, MidpointRounding.AwayFromZero):0} ms";
    }
}
