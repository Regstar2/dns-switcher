using System.Windows;
using System.Windows.Controls;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Presentation;
using DnsSwitcher.Ui.UiModels;

namespace DnsSwitcher.Ui;

public partial class ProfileEditorWindow : Window
{
    private static readonly char[] ValueSeparators = ['\r', '\n', ',', ';'];
    private readonly AppLocalizer localizer;

    public ProfileEditorWindow(AppLocalizer localizer, DnsProfile? profile = null)
    {
        InitializeComponent();
        WindowThemeService.Attach(this);
        this.localizer = localizer;
        ModeComboBox.ItemsSource = BuildModeOptions(localizer);
        ModeComboBox.SelectedValue = profile?.Mode ?? ProfileMode.Static;

        if (profile is not null)
        {
            IdTextBox.Text = profile.Id;
            NameTextBox.Text = profile.Name;
            DescriptionTextBox.Text = profile.Description ?? string.Empty;
            Ipv4TextBox.Text = string.Join(Environment.NewLine, profile.Ipv4);
            Ipv6TextBox.Text = string.Join(Environment.NewLine, profile.Ipv6);
            TagsTextBox.Text = string.Join(Environment.NewLine, profile.Tags);
            TestDomainsTextBox.Text = string.Join(Environment.NewLine, profile.TestDomains);
            TestUrlsTextBox.Text = string.Join(Environment.NewLine, profile.TestUrls);
        }

        ApplyLocalization(profile is not null);
        UpdateModeState();
    }

    public DnsProfile EditedProfile => new()
    {
        Id = IdTextBox.Text.Trim(),
        Name = NameTextBox.Text.Trim(),
        Description = NullIfWhiteSpace(DescriptionTextBox.Text),
        Mode = SelectedMode,
        Ipv4 = SelectedMode == ProfileMode.Dhcp ? [] : SplitValues(Ipv4TextBox.Text),
        Ipv6 = SelectedMode == ProfileMode.Dhcp ? [] : SplitValues(Ipv6TextBox.Text),
        Tags = SplitValues(TagsTextBox.Text),
        TestDomains = SplitValues(TestDomainsTextBox.Text),
        TestUrls = SplitValues(TestUrlsTextBox.Text),
    };

    private ProfileMode SelectedMode =>
        ModeComboBox.SelectedValue is ProfileMode mode
            ? mode
            : ProfileMode.Static;

    private void ApplyLocalization(bool isEditMode)
    {
        Title = localizer[isEditMode ? "ProfileEditorTitleEdit" : "ProfileEditorTitleNew"];
        IdLabelTextBlock.Text = localizer["ProfileEditorIdLabel"];
        NameLabelTextBlock.Text = localizer["ProfileEditorNameLabel"];
        DescriptionLabelTextBlock.Text = localizer["ProfileEditorDescriptionLabel"];
        ModeLabelTextBlock.Text = localizer["ProfileEditorModeLabel"];
        Ipv4LabelTextBlock.Text = localizer["ProfileEditorIpv4Label"];
        Ipv6LabelTextBlock.Text = localizer["ProfileEditorIpv6Label"];
        TagsLabelTextBlock.Text = localizer["ProfileEditorTagsLabel"];
        TestDomainsLabelTextBlock.Text = localizer["ProfileEditorTestDomainsLabel"];
        TestUrlsLabelTextBlock.Text = localizer["ProfileEditorTestUrlsLabel"];
        HintTextBlock.Text = localizer["ProfileEditorHintLineSeparated"];
        SaveButton.Content = localizer["SaveButton"];
        CancelButton.Content = localizer["CancelButton"];
    }

    private static IReadOnlyList<ProfileModeOption> BuildModeOptions(AppLocalizer localizer)
    {
        return
        [
            new ProfileModeOption { Mode = ProfileMode.Static, DisplayName = localizer["ProfileModeStatic"] },
            new ProfileModeOption { Mode = ProfileMode.Dhcp, DisplayName = localizer["ProfileModeDhcp"] },
        ];
    }

    private void OnModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateModeState();
    }

    private void UpdateModeState()
    {
        var isStaticMode = SelectedMode == ProfileMode.Static;
        Ipv4TextBox.IsEnabled = isStaticMode;
        Ipv6TextBox.IsEnabled = isStaticMode;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private static List<string> SplitValues(string? rawText)
    {
        return string.IsNullOrWhiteSpace(rawText)
            ? []
            : rawText
                .Split(ValueSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
