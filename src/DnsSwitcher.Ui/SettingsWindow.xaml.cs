using System.Windows;
using System.Windows.Automation;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Presentation;
using DnsSwitcher.Ui.UiModels;

namespace DnsSwitcher.Ui;

public partial class SettingsWindow : Window
{
    private readonly AppLocalizer localizer;

    public event EventHandler? AgentManagerRequested;

    public event EventHandler? HealthSettingsRequested;

    public event EventHandler? SplitDnsSettingsRequested;

    public SettingsWindow(
        AppLocalizer localizer,
        AppLanguage selectedLanguage,
        AppTheme selectedTheme,
        bool startWithWindowsEnabled,
        bool minimizeToTrayEnabled,
        TraySettings traySettings,
        bool isDarkTheme)
    {
        InitializeComponent();
        WindowThemeService.Attach(this);
        this.localizer = localizer;

        LanguageComboBox.ItemsSource = BuildLanguageOptions(localizer);
        LanguageComboBox.SelectedValue = selectedLanguage;
        ThemeComboBox.ItemsSource = BuildThemeOptions(localizer);
        ThemeComboBox.SelectedValue = selectedTheme;
        StartWithWindowsCheckBox.IsChecked = startWithWindowsEnabled;
        MinimizeToTrayCheckBox.IsChecked = minimizeToTrayEnabled;
        ShowDnsActionsCheckBox.IsChecked = traySettings.ShowDnsActions;
        ShowDiagnosticsCheckBox.IsChecked = traySettings.ShowDiagnostics;
        ShowSplitDnsCheckBox.IsChecked = traySettings.ShowSplitDns;
        ShowAgentCheckBox.IsChecked = traySettings.ShowAgent;
        ShowProfilesCheckBox.IsChecked = traySettings.ShowProfiles;
        ShowAdapterNameCheckBox.IsChecked = traySettings.ShowAdapterName;
        NotificationsEnabledCheckBox.IsChecked = traySettings.NotificationsEnabled;

        ApplyLocalization(isDarkTheme);
    }

    public AppLanguage SelectedLanguage =>
        LanguageComboBox.SelectedValue is AppLanguage language
            ? language
            : AppLanguage.System;

    public AppTheme SelectedTheme =>
        ThemeComboBox.SelectedValue is AppTheme theme
            ? theme
            : AppTheme.System;

    public bool StartWithWindowsEnabled => StartWithWindowsCheckBox.IsChecked == true;

    public bool ContinueInTrayEnabled => MinimizeToTrayCheckBox.IsChecked == true;

    public TraySettings EditedTraySettings => new()
    {
        NotificationsEnabled = NotificationsEnabledCheckBox.IsChecked == true,
        ShowAdapterName = ShowAdapterNameCheckBox.IsChecked == true,
        ShowDnsActions = ShowDnsActionsCheckBox.IsChecked == true,
        ShowDiagnostics = ShowDiagnosticsCheckBox.IsChecked == true,
        ShowSplitDns = ShowSplitDnsCheckBox.IsChecked == true,
        ShowAgent = ShowAgentCheckBox.IsChecked == true,
        ShowProfiles = ShowProfilesCheckBox.IsChecked == true,
    };

    private void ApplyLocalization(bool isDarkTheme)
    {
        Title = localizer["SettingsWindowTitle"];
        SettingsHeaderTextBlock.Text = localizer["SettingsWindowTitle"];
        SettingsSubtitleTextBlock.Text = localizer["SettingsSubtitle"];
        GeneralHeaderTextBlock.Text = localizer["SettingsGeneralHeader"];
        BehaviorHeaderTextBlock.Text = localizer["SettingsBehaviorHeader"];
        SystemTrayHeaderTextBlock.Text = localizer["SettingsSystemTrayHeader"];
        AdvancedHeaderTextBlock.Text = localizer["SettingsAdvancedHeader"];

        LanguageLabelTextBlock.Text = localizer["SettingsLanguageHeader"];
        LanguageDescriptionTextBlock.Text = localizer["SettingsLanguageDescription"];
        ThemeLabelTextBlock.Text = localizer["SettingsThemeHeader"];

        StartWithWindowsTitleTextBlock.Text = localizer["StartWithWindowsCheckBox"];
        StartWithWindowsDescriptionTextBlock.Text = localizer["SettingsStartWithWindowsDescription"];
        CloseToTrayTitleTextBlock.Text = localizer["CloseToTrayCheckBox"];
        CloseToTrayDescriptionTextBlock.Text = localizer["SettingsCloseToTrayDescription"];
        SetAccessibility(StartWithWindowsCheckBox, StartWithWindowsTitleTextBlock.Text, StartWithWindowsDescriptionTextBlock.Text);
        SetAccessibility(MinimizeToTrayCheckBox, CloseToTrayTitleTextBlock.Text, CloseToTrayDescriptionTextBlock.Text);

        ApplyTraySettingLocalization(
            ShowDnsActionsCheckBox,
            TrayDnsActionsTitleTextBlock,
            TrayDnsActionsDescriptionTextBlock,
            "SettingsTrayDnsActionsTitle",
            "SettingsTrayDnsActionsDescription");
        ApplyTraySettingLocalization(
            ShowDiagnosticsCheckBox,
            TrayDiagnosticsTitleTextBlock,
            TrayDiagnosticsDescriptionTextBlock,
            "SettingsTrayDiagnosticsTitle",
            "SettingsTrayDiagnosticsDescription");
        ApplyTraySettingLocalization(
            ShowProfilesCheckBox,
            TrayProfilesTitleTextBlock,
            TrayProfilesDescriptionTextBlock,
            "SettingsTrayProfilesTitle",
            "SettingsTrayProfilesDescription");
        ApplyTraySettingLocalization(
            ShowSplitDnsCheckBox,
            TraySplitDnsTitleTextBlock,
            TraySplitDnsDescriptionTextBlock,
            "SettingsTraySplitDnsTitle",
            "SettingsTraySplitDnsDescription");
        ApplyTraySettingLocalization(
            ShowAgentCheckBox,
            TrayAgentTitleTextBlock,
            TrayAgentDescriptionTextBlock,
            "SettingsTrayAgentTitle",
            "SettingsTrayAgentDescription");
        ApplyTraySettingLocalization(
            ShowAdapterNameCheckBox,
            TrayAdapterNameTitleTextBlock,
            TrayAdapterNameDescriptionTextBlock,
            "SettingsTrayAdapterNameTitle",
            "SettingsTrayAdapterNameDescription");
        ApplyTraySettingLocalization(
            NotificationsEnabledCheckBox,
            TrayNotificationsTitleTextBlock,
            TrayNotificationsDescriptionTextBlock,
            "SettingsTrayNotificationsTitle",
            "SettingsTrayNotificationsDescription");

        AgentManagerTitleTextBlock.Text = localizer["AgentManagerSettingsButton"];
        AgentManagerDescriptionTextBlock.Text = localizer["SettingsAgentDescription"];
        HealthSettingsTitleTextBlock.Text = localizer["HealthSettingsButton"];
        HealthSettingsDescriptionTextBlock.Text = localizer["SettingsHealthDescription"];
        SplitDnsSettingsTitleTextBlock.Text = localizer["SplitDnsButton"];
        SplitDnsSettingsDescriptionTextBlock.Text = localizer["SettingsSplitDnsDescription"];

        UpdateThemePreview(isDarkTheme);
        SaveButton.Content = localizer["SaveButton"];
        CancelButton.Content = localizer["CancelButton"];
    }

    private void ApplyTraySettingLocalization(
        System.Windows.Controls.CheckBox checkBox,
        System.Windows.Controls.TextBlock title,
        System.Windows.Controls.TextBlock description,
        string titleKey,
        string descriptionKey)
    {
        title.Text = localizer[titleKey];
        description.Text = localizer[descriptionKey];
        SetAccessibility(checkBox, title.Text, description.Text);
    }

    private static void SetAccessibility(System.Windows.Controls.CheckBox checkBox, string name, string helpText)
    {
        AutomationProperties.SetName(checkBox, name);
        AutomationProperties.SetHelpText(checkBox, helpText);
    }

    private static IReadOnlyList<LanguageOption> BuildLanguageOptions(AppLocalizer localizer)
    {
        return
        [
            new LanguageOption { Language = AppLanguage.System, DisplayName = localizer["LanguageSystem"] },
            new LanguageOption { Language = AppLanguage.English, DisplayName = localizer["LanguageEnglish"] },
            new LanguageOption { Language = AppLanguage.Russian, DisplayName = localizer["LanguageRussian"] },
        ];
    }

    private static IReadOnlyList<ThemeOption> BuildThemeOptions(AppLocalizer localizer)
    {
        return
        [
            new ThemeOption { Theme = AppTheme.System, DisplayName = localizer["ThemeSystemValue"] },
            new ThemeOption { Theme = AppTheme.Light, DisplayName = localizer["ThemeLightValue"] },
            new ThemeOption { Theme = AppTheme.Dark, DisplayName = localizer["ThemeDarkValue"] },
        ];
    }

    private void OnThemeSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateThemePreview(SelectedTheme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => WindowsThemeDetector.IsDarkModeEnabled(),
        });
    }

    private void UpdateThemePreview(bool isDarkTheme)
    {
        var currentThemeText = localizer.Format(
            "ThemeCurrentFormat",
            isDarkTheme ? localizer["ThemeDarkValue"] : localizer["ThemeLightValue"]);

        ThemeCurrentTextBlock.Text = SelectedTheme == AppTheme.System
            ? $"{localizer["ThemeFollowsSystemText"]} {currentThemeText}"
            : currentThemeText;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnAgentManagerClicked(object sender, RoutedEventArgs e)
    {
        AgentManagerRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnHealthSettingsClicked(object sender, RoutedEventArgs e)
    {
        HealthSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSplitDnsSettingsClicked(object sender, RoutedEventArgs e)
    {
        SplitDnsSettingsRequested?.Invoke(this, EventArgs.Empty);
    }
}
