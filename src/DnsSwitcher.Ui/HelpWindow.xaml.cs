using System.Windows;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Ui;

public partial class HelpWindow : Window
{
    public HelpWindow(AppLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        InitializeComponent();
        WindowThemeService.Attach(this);

        Title = localizer.GetUpdateText("HelpWindowTitle");
        HeaderTextBlock.Text = localizer.GetUpdateText("HelpWindowHeader");
        IntroTextBlock.Text = localizer.GetUpdateText("HelpWindowIntro");
        CloseButton.Content = localizer.GetUpdateText("CloseButton");
        SectionsItemsControl.ItemsSource = BuildSections(localizer);
    }

    private static IReadOnlyList<HelpSection> BuildSections(AppLocalizer localizer) =>
    [
        Section(localizer, "HelpProfilesTitle", "HelpProfilesBody"),
        Section(localizer, "HelpAdapterTitle", "HelpAdapterBody"),
        Section(localizer, "HelpChecksTitle", "HelpChecksBody"),
        Section(localizer, "HelpHealthTitle", "HelpHealthBody"),
        Section(localizer, "HelpSplitDnsTitle", "HelpSplitDnsBody"),
        Section(localizer, "HelpAgentTitle", "HelpAgentBody"),
        Section(localizer, "HelpTrayTitle", "HelpTrayBody"),
        Section(localizer, "HelpImportExportTitle", "HelpImportExportBody"),
        Section(localizer, "HelpSettingsTitle", "HelpSettingsBody"),
        Section(localizer, "HelpUpdatesTitle", "HelpUpdatesBody"),
        Section(localizer, "HelpFilesTitle", "HelpFilesBody"),
    ];

    private static HelpSection Section(AppLocalizer localizer, string titleKey, string bodyKey) =>
        new(localizer.GetUpdateText(titleKey), localizer.GetUpdateText(bodyKey));

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private sealed record HelpSection(string Title, string Body);
}
