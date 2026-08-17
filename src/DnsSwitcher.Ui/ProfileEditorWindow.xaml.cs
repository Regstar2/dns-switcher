using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        NameTextBox.TextChanged += OnInputChanged;
        IdTextBox.TextChanged += OnInputChanged;
        Ipv4TextBox.TextChanged += OnInputChanged;
        Ipv6TextBox.TextChanged += OnInputChanged;
        UpdateModeState();
        ValidateInputs();
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

        System.Windows.Automation.AutomationProperties.SetName(NameTextBox, NameLabelTextBlock.Text);
        System.Windows.Automation.AutomationProperties.SetName(IdTextBox, IdLabelTextBlock.Text);
        System.Windows.Automation.AutomationProperties.SetName(Ipv4TextBox, Ipv4LabelTextBlock.Text);
        System.Windows.Automation.AutomationProperties.SetName(Ipv6TextBox, Ipv6LabelTextBlock.Text);
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
        ValidateInputs();
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e)
    {
        ValidateInputs();
    }

    private void UpdateModeState()
    {
        var isStaticMode = SelectedMode == ProfileMode.Static;
        Ipv4TextBox.IsEnabled = isStaticMode;
        Ipv6TextBox.IsEnabled = isStaticMode;
    }

    private void ValidateInputs()
    {
        var nameValid = !string.IsNullOrWhiteSpace(NameTextBox.Text);
        var idValid = !string.IsNullOrWhiteSpace(IdTextBox.Text);
        var ipv4Valid = SelectedMode == ProfileMode.Dhcp
            || ValidateAddressList(Ipv4TextBox.Text, AddressFamily.InterNetwork);
        var ipv6Valid = SelectedMode == ProfileMode.Dhcp
            || ValidateAddressList(Ipv6TextBox.Text, AddressFamily.InterNetworkV6);

        SetValidationState(NameTextBox, nameValid, NameLabelTextBlock.Text);
        SetValidationState(IdTextBox, idValid, IdLabelTextBlock.Text);
        SetValidationState(Ipv4TextBox, ipv4Valid, Ipv4LabelTextBlock.Text);
        SetValidationState(Ipv6TextBox, ipv6Valid, Ipv6LabelTextBlock.Text);
        SaveButton.IsEnabled = nameValid && idValid && ipv4Valid && ipv6Valid;
    }

    private void SetValidationState(TextBox textBox, bool isValid, string label)
    {
        textBox.BorderBrush = (Brush)FindResource(isValid ? "BorderBrush" : "DangerBrush");
        textBox.ToolTip = isValid ? null : label;
    }

    private static bool ValidateAddressList(string? rawText, AddressFamily expectedFamily)
    {
        var values = SplitRawValues(rawText);
        if (values.Count != values.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return false;
        }

        return values.All(value =>
            IPAddress.TryParse(value, out var address)
            && address.AddressFamily == expectedFamily);
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        ValidateInputs();
        if (!SaveButton.IsEnabled)
        {
            return;
        }

        DialogResult = true;
    }

    private static List<string> SplitRawValues(string? rawText)
    {
        return string.IsNullOrWhiteSpace(rawText)
            ? []
            : rawText
                .Split(ValueSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
    }

    private static List<string> SplitValues(string? rawText)
    {
        return SplitRawValues(rawText)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
