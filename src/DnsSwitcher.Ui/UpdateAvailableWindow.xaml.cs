using System.Windows;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Ui;

public partial class UpdateAvailableWindow : Window
{
    private readonly AppLocalizer localizer;

    public UpdateAvailableWindow(AppLocalizer localizer, UpdateInfo update)
    {
        InitializeComponent();
        WindowThemeService.Attach(this);
        this.localizer = localizer;
        Update = update;

        Title = localizer.GetUpdateText("UpdateDialogTitle");
        TitleTextBlock.Text = localizer.GetUpdateText("UpdateDialogTitle");
        VersionTextBlock.Text = localizer.FormatUpdateText("UpdateAvailableFormat", update.Version);
        DetailsTextBlock.Text = localizer.GetUpdateText("SettingsAboutDescription");
        StatusTextBlock.Text = string.Empty;
        ReleaseNotesButton.Content = localizer.GetUpdateText("UpdateReleaseNotesButton");
        LaterButton.Content = localizer.GetUpdateText("UpdateLaterButton");
        InstallButton.Content = localizer.GetUpdateText("UpdateInstallButton");
    }

    public UpdateInfo Update { get; }

    public event EventHandler? InstallRequested;

    public event EventHandler? ReleaseNotesRequested;

    public void SetBusy(bool busy, string status)
    {
        InstallButton.IsEnabled = !busy;
        ReleaseNotesButton.IsEnabled = !busy;
        LaterButton.IsEnabled = !busy;
        ProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        StatusTextBlock.Text = status;
    }

    public void SetError(string message)
    {
        SetBusy(false, message);
    }

    private void OnInstallClicked(object sender, RoutedEventArgs e)
    {
        InstallRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnReleaseNotesClicked(object sender, RoutedEventArgs e)
    {
        ReleaseNotesRequested?.Invoke(this, EventArgs.Empty);
    }
}
