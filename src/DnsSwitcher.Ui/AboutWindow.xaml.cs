using System.Windows;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Ui;

public partial class AboutWindow : Window
{
    public AboutWindow(AppLocalizer localizer, ApplicationMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(metadata);

        InitializeComponent();
        WindowThemeService.Attach(this);

        Title = localizer.GetUpdateText("AboutWindowTitle");
        ProductNameTextBlock.Text = metadata.ProductName;
        VersionTextBlock.Text = localizer.FormatUpdateText("AboutVersionFormat", metadata.DisplayVersion);
        SummaryTextBlock.Text = localizer.GetUpdateText("AboutDetailedSummary");
        CapabilitiesHeaderTextBlock.Text = localizer.GetUpdateText("AboutCapabilitiesHeader");
        CapabilitiesTextBlock.Text = localizer.GetUpdateText("AboutCapabilitiesBody");
        ArchitectureHeaderTextBlock.Text = localizer.GetUpdateText("AboutArchitectureHeader");
        ArchitectureTextBlock.Text = localizer.GetUpdateText("AboutArchitectureBody");
        LicenseHeaderTextBlock.Text = localizer.GetUpdateText("AboutLicenseHeader");
        LicenseTextBlock.Text = localizer.GetUpdateText("AboutLicenseBody");
        GitHubButton.Content = localizer.GetUpdateText("OpenGitHubButton");
        CloseButton.Content = localizer.GetUpdateText("CloseButton");
    }

    public event EventHandler? OpenRepositoryRequested;

    private void OnOpenGitHubClicked(object sender, RoutedEventArgs e)
    {
        OpenRepositoryRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
