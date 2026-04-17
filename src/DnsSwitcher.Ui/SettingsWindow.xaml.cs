using System.Windows;
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

    private void ApplyLocalization(bool isDarkTheme)
    {
        Title = localizer["SettingsWindowTitle"];
        LanguageGroupBox.Header = localizer["SettingsLanguageHeader"];
        BehaviorGroupBox.Header = localizer["SettingsBehaviorHeader"];
        ThemeGroupBox.Header = localizer["SettingsThemeHeader"];
        LanguageLabelTextBlock.Text = localizer["LanguageLabel"];
        ThemeLabelTextBlock.Text = localizer["ThemeLabel"];
        StartWithWindowsCheckBox.Content = localizer["StartWithWindowsCheckBox"];
        MinimizeToTrayCheckBox.Content = localizer["CloseToTrayCheckBox"];
        AgentManagerButton.Content = localizer["AgentManagerSettingsButton"];
        HealthSettingsButton.Content = localizer["HealthSettingsButton"];
        SplitDnsSettingsButton.Content = localizer["SplitDnsButton"];
        UpdateThemePreview(isDarkTheme);
        SaveButton.Content = localizer["SaveButton"];
        CancelButton.Content = localizer["CancelButton"];
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
        ThemeModeTextBlock.Text = SelectedTheme == AppTheme.System
            ? localizer["ThemeFollowsSystemText"]
            : string.Empty;
        ThemeCurrentTextBlock.Text = localizer.Format(
            "ThemeCurrentFormat",
            isDarkTheme ? localizer["ThemeDarkValue"] : localizer["ThemeLightValue"]);
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
