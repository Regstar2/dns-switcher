using System.Windows;
using System.Windows.Controls;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DnsSwitcher.Ui;

public partial class MainWindow
{
    private bool mainWindowEnhancementsInitialized;
    private MenuItem? exportAllProfilesMenuItem;
    private JsonTraySettingsStore? traySettingsStore;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (mainWindowEnhancementsInitialized)
        {
            return;
        }

        mainWindowEnhancementsInitialized = true;
        traySettingsStore = new JsonTraySettingsStore(
            App.Host.Paths,
            App.Host.LoggerFactory.CreateLogger<JsonTraySettingsStore>());
        LoadMainWindowEnhancementResources();
        ApplyMainWindowButtonStyles();
        ConfigureMainWindowMenus();

        ReloadButton.Click -= OnRefreshClicked;
        ReloadButton.Click += OnSmoothRefreshClicked;
        SettingsButton.Click -= OnOpenSettingsClicked;
        SettingsButton.Click += OnOpenSettingsWithTraySettingsClicked;
    }

    private void LoadMainWindowEnhancementResources()
    {
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/Themes/MainWindowEnhancements.xaml", UriKind.Relative),
        });
    }

    private void ApplyMainWindowButtonStyles()
    {
        var softStyle = (Style)FindResource("MainWindowSoftButtonStyle");
        var primaryStyle = (Style)FindResource("MainWindowPrimaryButtonStyle");
        var dangerStyle = (Style)FindResource("MainWindowDangerButtonStyle");
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
            button.Style = softStyle;
        }

        ApplyButton.Style = primaryStyle;
        DeleteProfileButton.Style = dangerStyle;
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

    private async void OnOpenSettingsWithTraySettingsClicked(object sender, RoutedEventArgs e)
    {
        if (isBusy || traySettingsStore is null)
        {
            return;
        }

        try
        {
            var traySettings = await LoadTraySettingsForSettingsWindowAsync().ConfigureAwait(true);
            var settingsWindow = new SettingsWindow(
                localizer,
                appPreferences.Language,
                appPreferences.Theme,
                IsTrayAutostartEnabled(),
                uiSettings.MinimizeToTray,
                traySettings,
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

            await traySettingsStore.SaveAsync(settingsWindow.EditedTraySettings).ConfigureAwait(true);
            await ApplySettingsAsync(settingsWindow).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
    }

    private async Task<TraySettings> LoadTraySettingsForSettingsWindowAsync()
    {
        if (traySettingsStore is null)
        {
            return TraySettings.Default;
        }

        try
        {
            return await traySettingsStore.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Tray settings could not be loaded in Desktop Settings. Default tray settings will be shown.");
            return TraySettings.Default;
        }
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

        SetRefreshInteractionEnabled(enabled: false);

        try
        {
            await RefreshUiAsync(
                localizer["ReloadedStatus"],
                showBusyMessage: false,
                showErrorDialog: true,
                preserveOperationStatus: false,
                disableControls: false).ConfigureAwait(true);
        }
        finally
        {
            SetRefreshInteractionEnabled(enabled: true);
        }
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
