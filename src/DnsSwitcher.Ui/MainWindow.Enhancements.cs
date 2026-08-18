using System.Windows;
using System.Windows.Controls;
using DnsSwitcher.Infrastructure.Windows.Configuration;
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
