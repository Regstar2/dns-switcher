using System.Windows;
using System.Windows.Controls;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Ui.UiModels;
using Microsoft.Win32;

namespace DnsSwitcher.Ui;

public partial class MainWindow
{
    private bool mainWindowEnhancementsInitialized;
    private MenuItem? exportAllProfilesMenuItem;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (mainWindowEnhancementsInitialized)
        {
            return;
        }

        mainWindowEnhancementsInitialized = true;
        LoadMainWindowEnhancementResources();
        ApplyMainWindowButtonStyles();
        ConfigureMainWindowMenus();

        ReloadButton.Click -= OnRefreshClicked;
        ReloadButton.Click += OnSmoothRefreshClicked;
    }

    private void LoadMainWindowEnhancementResources()
    {
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/DnsSwitcher.Ui;component/Themes/MainWindowEnhancements.xaml", UriKind.Relative),
        });
    }

    private void ApplyMainWindowButtonStyles()
    {
        var style = (Style)FindResource("MainWindowSoftButtonStyle");
        var buttons = new[]
        {
            ReloadButton,
            CreateProfileButton,
            EditProfileButton,
            ResetButton,
            ChecksButton,
            MoreToolsButton,
            SettingsButton,
            AdditionalButton,
        };

        foreach (var button in buttons)
        {
            button.Style = style;
        }
    }

    private void ConfigureMainWindowMenus()
    {
        var exportIndex = MoreToolsContextMenu.Items.IndexOf(ExportProfileMenuItem);
        if (exportIndex >= 0)
        {
            MoreToolsContextMenu.Items.Insert(exportIndex, new Separator());
        }

        exportAllProfilesMenuItem = new MenuItem();
        exportAllProfilesMenuItem.Click += OnExportAllProfilesClicked;
        MoreToolsContextMenu.Items.Add(exportAllProfilesMenuItem);
        MoreToolsContextMenu.Opened += OnMoreToolsContextMenuOpened;

        ApplyContextMenuStyle(ChecksContextMenu);
        ApplyContextMenuStyle(MoreToolsContextMenu);
        ApplyContextMenuStyle(AdditionalContextMenu);
        UpdateEnhancementLocalization();
    }

    private void ApplyContextMenuStyle(ContextMenu contextMenu)
    {
        var contextMenuStyle = (Style)FindResource("MainWindowContextMenuStyle");
        var menuItemStyle = (Style)FindResource("MainWindowMenuItemStyle");
        var separatorStyle = (Style)FindResource("MainWindowMenuSeparatorStyle");

        contextMenu.Style = contextMenuStyle;

        foreach (var item in contextMenu.Items)
        {
            switch (item)
            {
                case MenuItem menuItem:
                    menuItem.Style = menuItemStyle;
                    break;
                case Separator separator:
                    separator.Style = separatorStyle;
                    break;
            }
        }
    }

    private void OnMoreToolsContextMenuOpened(object sender, RoutedEventArgs e)
    {
        UpdateEnhancementLocalization();
    }

    private void UpdateEnhancementLocalization()
    {
        if (exportAllProfilesMenuItem is null)
        {
            return;
        }

        exportAllProfilesMenuItem.Header = GetEnhancementText(
            english: "Export all profiles",
            russian: "Экспорт всех профилей");
    }

    private async void OnExportAllProfilesClicked(object sender, RoutedEventArgs e)
    {
        if (isBusy || isRefreshingUi)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = GetEnhancementText(
                english: "Export all DNS profiles",
                russian: "Экспорт всех DNS-профилей"),
            Filter = localizer["JsonFilesFilter"],
            FileName = "dns-profiles.json",
            AddExtension = true,
            DefaultExt = ".json",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        isBusy = true;
        SetRefreshInteractionEnabled(enabled: false);

        try
        {
            var configuration = await App.Host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
            await profileExchangeService
                .ExportProfilesAsync(dialog.FileName, configuration.Profiles)
                .ConfigureAwait(true);
            SetOperationStatus(
                GetEnhancementText(
                    english: "All DNS profiles were exported.",
                    russian: "Все DNS-профили экспортированы."),
                isError: false);
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
        finally
        {
            isBusy = false;
            SetRefreshInteractionEnabled(enabled: true);
            UpdateActionButtons();
        }
    }

    private async void OnSmoothRefreshClicked(object sender, RoutedEventArgs e)
    {
        if (isBusy || isRefreshingUi)
        {
            return;
        }

        logger.LogInformation("UI requested reload without replacing unchanged item sources.");

        var selectedProfileId = GetSelectedProfileId() ?? uiSettings.LastSelectedProfileId;
        var selectedAdapterValue = GetSelectedAdapterValue() ?? uiSettings.LastAdapterId;
        isRefreshingUi = true;
        SetRefreshInteractionEnabled(enabled: false);

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

            if (AdapterOptionsNeedRefresh(adapters, defaultAdapter))
            {
                RebuildAdapterOptions(adapters, defaultAdapter, resolvedAdapterSelection);
            }

            if (ProfilesNeedRefresh(configuration, dnsStatus, activeProfile))
            {
                RebuildProfiles(configuration, dnsStatus, activeProfile, selectedProfileId);
            }
            else
            {
                UpdateSelectedProfilePanel(ProfilesListBox.SelectedItem as ProfileListItem);
            }

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
            SetOperationStatus(localizer["ReloadedStatus"], isError: false);
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
        finally
        {
            SetRefreshInteractionEnabled(enabled: true);
            isRefreshingUi = false;
        }
    }

    private bool AdapterOptionsNeedRefresh(
        IReadOnlyList<NetworkAdapter> adapters,
        NetworkAdapter? defaultAdapter)
    {
        var currentOptions = AdapterComboBox.Items.OfType<AdapterOption>().ToArray();
        if (currentOptions.Length != adapters.Count + 1)
        {
            return true;
        }

        var expectedAutomaticDisplayName = defaultAdapter is null
            ? localizer["AutomaticNoDefaultAdapter"]
            : localizer.Format("AutomaticAdapterFormat", defaultAdapter.Name);

        if (currentOptions[0].SelectionValue is not null
            || !string.Equals(currentOptions[0].DisplayName, expectedAutomaticDisplayName, StringComparison.Ordinal))
        {
            return true;
        }

        var orderedAdapters = adapters
            .OrderBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (var index = 0; index < orderedAdapters.Length; index++)
        {
            var current = currentOptions[index + 1];
            var expected = orderedAdapters[index];

            if (!string.Equals(current.SelectionValue, expected.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(current.DisplayName, BuildAdapterDisplayName(expected), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool ProfilesNeedRefresh(
        AppConfig configuration,
        DnsStatus status,
        DnsProfile? activeProfile)
    {
        var expectedItems = configuration.Profiles
            .Select(profile => CreateProfileListItem(configuration, status, activeProfile, profile))
            .OrderBy(item => item.Profile.Mode == ProfileMode.Dhcp ? 1 : 0)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var currentItems = ProfilesListBox.Items.OfType<ProfileListItem>().ToArray();

        if (currentItems.Length != expectedItems.Length)
        {
            return true;
        }

        for (var index = 0; index < expectedItems.Length; index++)
        {
            var current = currentItems[index];
            var expected = expectedItems[index];

            if (!string.Equals(current.StatusText, expected.StatusText, StringComparison.Ordinal)
                || !string.Equals(current.SummaryText, expected.SummaryText, StringComparison.Ordinal)
                || !ProfilesEquivalent(current.Profile, expected.Profile))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ProfilesEquivalent(DnsProfile left, DnsProfile right)
    {
        return string.Equals(left.Id, right.Id, StringComparison.Ordinal)
            && string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            && string.Equals(left.Description, right.Description, StringComparison.Ordinal)
            && left.Mode == right.Mode
            && left.Ipv4.SequenceEqual(right.Ipv4, StringComparer.Ordinal)
            && left.Ipv6.SequenceEqual(right.Ipv6, StringComparer.Ordinal)
            && left.Tags.SequenceEqual(right.Tags, StringComparer.Ordinal)
            && left.TestDomains.SequenceEqual(right.TestDomains, StringComparer.Ordinal)
            && left.TestUrls.SequenceEqual(right.TestUrls, StringComparer.Ordinal);
    }

    private void SetRefreshInteractionEnabled(bool enabled)
    {
        AdapterGroupBox.IsHitTestVisible = enabled;
        MainContentGrid.IsHitTestVisible = enabled;
        BottomBarGrid.IsHitTestVisible = enabled;
    }

    private string GetEnhancementText(string english, string russian)
    {
        return localizer.EffectiveLanguage == AppLanguage.Russian ? russian : english;
    }
}
