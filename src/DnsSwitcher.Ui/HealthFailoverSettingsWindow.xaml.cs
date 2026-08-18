using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Shapes;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Ui;

public partial class HealthFailoverSettingsWindow : Window
{
    private static readonly char[] ValueSeparators = ['\r', '\n', ',', ';'];
    private readonly AppLocalizer localizer;
    private readonly IReadOnlyList<DnsProfile> profiles;
    private readonly List<string> failoverChain;
    private readonly List<string> testDomains;

    public HealthFailoverSettingsWindow(
        AppLocalizer localizer,
        DnsHealthSettings settings,
        DnsHealthState state,
        IReadOnlyList<DnsProfile> profiles)
    {
        InitializeComponent();
        WindowThemeService.Attach(this);
        this.localizer = localizer;
        this.profiles = profiles;
        failoverChain = settings.FailoverChain.ToList();
        testDomains = settings.TestDomains
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Select(domain => domain.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var profileOptions = BuildProfileOptions(profiles).ToArray();
        FallbackProfileComboBox.ItemsSource = new[] { new ProfileOption(null, localizer["NoneValue"]) }.Concat(profileOptions).ToArray();
        ChainProfileComboBox.ItemsSource = profileOptions;
        CheckModeComboBox.ItemsSource = BuildCheckModeOptions();
        ActionComboBox.ItemsSource = BuildActionOptions();
        ApplyLocalization();

        EnabledCheckBox.IsChecked = settings.Enabled;
        IntervalTextBox.Text = settings.MonitorIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        FailureThresholdTextBox.Text = settings.FailureThreshold.ToString(CultureInfo.InvariantCulture);
        RecoveryThresholdTextBox.Text = settings.RecoveryThreshold.ToString(CultureInfo.InvariantCulture);
        CooldownTextBox.Text = settings.CooldownSeconds.ToString(CultureInfo.InvariantCulture);
        CheckModeComboBox.SelectedValue = settings.CheckMode;
        ActionComboBox.SelectedValue = settings.ActionOnFailure;
        FallbackProfileComboBox.SelectedValue = settings.FallbackProfileId;
        ExpectedAddressesTextBox.Text = FormatExpectedAddresses(settings.ExpectedAddresses);

        RefreshDomainsList();
        RefreshChainList();
        UpdateConditionalSections();
        UpdateMonitorStateText();
        UpdateHealthStateView(state);
    }

    public bool RunCheckRequested { get; private set; }

    public string MoveUpActionText => localizer["MoveUpButton"];

    public string MoveDownActionText => localizer["MoveDownButton"];

    public string RemoveActionText => localizer["RemoveButton"];

    public DnsHealthSettings EditedSettings => new()
    {
        Enabled = EnabledCheckBox.IsChecked == true,
        MonitorIntervalSeconds = ParsePositiveInt(IntervalTextBox.Text, IntervalLabelTextBlock.Text),
        FailureThreshold = ParsePositiveInt(FailureThresholdTextBox.Text, FailureThresholdLabelTextBlock.Text),
        RecoveryThreshold = ParsePositiveInt(RecoveryThresholdTextBox.Text, RecoveryThresholdLabelTextBlock.Text),
        CooldownSeconds = ParseNonNegativeInt(CooldownTextBox.Text, CooldownLabelTextBlock.Text),
        CheckMode = CheckModeComboBox.SelectedValue is DnsHealthCheckMode mode ? mode : DnsHealthCheckMode.ResolveOnly,
        ActionOnFailure = ActionComboBox.SelectedValue is DnsHealthFailureAction action ? action : DnsHealthFailureAction.NotifyOnly,
        FallbackProfileId = FallbackProfileComboBox.SelectedValue as string,
        FailoverChain = failoverChain.ToList(),
        TestDomains = testDomains.ToList(),
        ExpectedAddresses = ParseExpectedAddresses(ExpectedAddressesTextBox.Text),
    };

    private void OnEnabledMonitorChanged(object sender, RoutedEventArgs e)
    {
        UpdateMonitorStateText();
    }

    private void OnCheckModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateConditionalSections();
    }

    private void OnActionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateConditionalSections();
    }

    private void OnAddDomainsClicked(object sender, RoutedEventArgs e)
    {
        AddDomainsFromInput();
    }

    private void OnDomainInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        AddDomainsFromInput();
        e.Handled = true;
    }

    private void OnRemoveDomainClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string domain)
        {
            return;
        }

        testDomains.RemoveAll(value => string.Equals(value, domain, StringComparison.OrdinalIgnoreCase));
        RefreshDomainsList();
    }

    private void OnAddChainProfileClicked(object sender, RoutedEventArgs e)
    {
        if (ChainProfileComboBox.SelectedValue is not string profileId || string.IsNullOrWhiteSpace(profileId))
        {
            return;
        }

        if (!failoverChain.Contains(profileId, StringComparer.OrdinalIgnoreCase))
        {
            failoverChain.Add(profileId);
            RefreshChainList();
        }
    }

    private void OnRemoveChainProfileClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not ChainItem item)
        {
            return;
        }

        if (item.Index < 0 || item.Index >= failoverChain.Count)
        {
            return;
        }

        failoverChain.RemoveAt(item.Index);
        RefreshChainList();
    }

    private void OnMoveChainUpClicked(object sender, RoutedEventArgs e)
    {
        MoveChainItemFromButton(sender, -1);
    }

    private void OnMoveChainDownClicked(object sender, RoutedEventArgs e)
    {
        MoveChainItemFromButton(sender, 1);
    }

    private void OnRunCheckClicked(object sender, RoutedEventArgs e)
    {
        if (!TryValidate(out _))
        {
            return;
        }

        RunCheckRequested = true;
        DialogResult = true;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (!TryValidate(out _))
        {
            return;
        }

        DialogResult = true;
    }

    private void AddDomainsFromInput()
    {
        var values = SplitValues(TestDomainInputTextBox.Text);
        var changed = false;

        foreach (var value in values)
        {
            var domain = value.Trim();

            if (string.IsNullOrWhiteSpace(domain) || testDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            testDomains.Add(domain);
            changed = true;
        }

        TestDomainInputTextBox.Clear();

        if (changed)
        {
            RefreshDomainsList();
        }
    }

    private void UpdateConditionalSections()
    {
        var checkMode = CheckModeComboBox.SelectedValue is DnsHealthCheckMode mode
            ? mode
            : DnsHealthCheckMode.ResolveOnly;
        var action = ActionComboBox.SelectedValue is DnsHealthFailureAction selectedAction
            ? selectedAction
            : DnsHealthFailureAction.NotifyOnly;

        ExpectedIpSectionBorder.Visibility = checkMode == DnsHealthCheckMode.ResolveWithExpectedIp
            ? Visibility.Visible
            : Visibility.Collapsed;
        FailoverChainCard.Visibility = action == DnsHealthFailureAction.SwitchToNextProfile
            ? Visibility.Visible
            : Visibility.Collapsed;
        FallbackProfileCard.Visibility = action == DnsHealthFailureAction.SwitchToFallbackProfile
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateMonitorStateText()
    {
        MonitorStateTextBlock.Text = EnabledCheckBox.IsChecked == true
            ? localizer["EnabledValue"]
            : localizer["DisabledValue"];
    }

    private void RefreshDomainsList()
    {
        TestDomainsListBox.ItemsSource = testDomains.ToArray();
    }

    private void MoveChainItemFromButton(object sender, int direction)
    {
        if (sender is not Button button || button.Tag is not ChainItem item)
        {
            return;
        }

        MoveChainItem(item.Index, direction);
    }

    private void MoveChainItem(int index, int direction)
    {
        var targetIndex = index + direction;

        if (index < 0 || index >= failoverChain.Count || targetIndex < 0 || targetIndex >= failoverChain.Count)
        {
            return;
        }

        (failoverChain[index], failoverChain[targetIndex]) = (failoverChain[targetIndex], failoverChain[index]);
        RefreshChainList();
    }

    private void RefreshChainList()
    {
        FailoverChainListBox.ItemsSource = failoverChain
            .Select((profileId, index) => BuildChainItem(profileId, index, failoverChain.Count))
            .ToArray();
    }

    private ChainItem BuildChainItem(string profileId, int index, int count)
    {
        var profile = profiles.FirstOrDefault(candidate => string.Equals(candidate.Id, profileId, StringComparison.OrdinalIgnoreCase));

        return profile is null
            ? new ChainItem(
                index,
                profileId,
                profileId,
                localizer["MissingProfileValue"],
                index > 0,
                index < count - 1)
            : new ChainItem(
                index,
                profileId,
                profile.Name,
                profile.Id,
                index > 0,
                index < count - 1);
    }

    private string ResolveProfileDisplayName(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return localizer["NoneValue"];
        }

        var profile = profiles.FirstOrDefault(candidate => string.Equals(candidate.Id, profileId, StringComparison.OrdinalIgnoreCase));
        return profile is null ? $"{profileId} ({localizer["MissingProfileValue"]})" : profile.Name;
    }

    private static IReadOnlyList<ProfileOption> BuildProfileOptions(IReadOnlyList<DnsProfile> profiles)
    {
        return profiles
            .Where(profile => profile.Mode == ProfileMode.Static)
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new ProfileOption(profile.Id, $"{profile.Name} ({profile.Id})"))
            .ToArray();
    }

    private IReadOnlyList<CheckModeOption> BuildCheckModeOptions()
    {
        return
        [
            new CheckModeOption(DnsHealthCheckMode.ResolveOnly, localizer["HealthCheckModeResolveOnly"]),
            new CheckModeOption(DnsHealthCheckMode.ResolveWithExpectedIp, localizer["HealthCheckModeExpectedIp"]),
        ];
    }

    private IReadOnlyList<ActionOption> BuildActionOptions()
    {
        return
        [
            new ActionOption(DnsHealthFailureAction.NotifyOnly, localizer["HealthActionNotifyOnly"]),
            new ActionOption(DnsHealthFailureAction.SwitchToNextProfile, localizer["HealthActionSwitchNext"]),
            new ActionOption(DnsHealthFailureAction.SwitchToFallbackProfile, localizer["HealthActionSwitchFallback"]),
        ];
    }

    private void UpdateHealthStateView(DnsHealthState state)
    {
        HealthStatusTextBlock.Text = state.Status switch
        {
            DnsHealthStatus.Healthy => localizer["HealthStatusHealthy"],
            DnsHealthStatus.Degraded => localizer["HealthStatusDegraded"],
            DnsHealthStatus.Failed => localizer["HealthStatusFailed"],
            DnsHealthStatus.Cooldown => localizer["HealthStatusCooldown"],
            _ => localizer["HealthStatusDisabled"],
        };

        var (backgroundResource, foregroundResource) = state.Status switch
        {
            DnsHealthStatus.Healthy => ("SuccessStatusBrush", "SuccessTextBrush"),
            DnsHealthStatus.Degraded => ("WarningStatusBrush", "WarningTextBrush"),
            DnsHealthStatus.Cooldown => ("WarningStatusBrush", "WarningTextBrush"),
            DnsHealthStatus.Failed => ("ErrorStatusBrush", "ErrorTextBrush"),
            _ => ("SurfaceMutedBrush", "SecondaryTextBrush"),
        };

        HealthStatusBadgeBorder.SetResourceReference(Border.BackgroundProperty, backgroundResource);
        HealthStatusTextBlock.SetResourceReference(TextElement.ForegroundProperty, foregroundResource);
        HealthStatusDot.SetResourceReference(Shape.FillProperty, foregroundResource);

        ActiveProfileValueTextBlock.Text = ResolveProfileDisplayName(state.ActiveProfileId);
        FailuresMetricValueTextBlock.Text = state.ConsecutiveFailures.ToString(CultureInfo.CurrentCulture);
        SuccessesMetricValueTextBlock.Text = state.ConsecutiveSuccesses.ToString(CultureInfo.CurrentCulture);
        LastCheckedMetricValueTextBlock.Text = FormatUtc(state.LastCheckedUtc, localizer["NeverValue"]);
        LastFailoverMetricValueTextBlock.Text = ResolveProfileDisplayName(state.LastFailoverProfileId);
        LastSuccessfulValueTextBlock.Text = FormatUtc(state.LastSuccessfulCheckUtc, localizer["NeverValue"]);
        LastFailureValueTextBlock.Text = FormatUtc(state.LastFailureUtc, localizer["NeverValue"]);
        CooldownUntilValueTextBlock.Text = FormatUtc(state.CooldownUntilUtc, localizer["NoneValue"]);
        FailureReasonValueTextBlock.Text = state.LastFailureReason ?? localizer["NoneValue"];
        LastActionValueTextBlock.Text = state.LastAction ?? localizer["NoneValue"];
    }

    private static string FormatUtc(DateTimeOffset? value, string emptyValue)
    {
        return value.HasValue
            ? value.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
            : emptyValue;
    }

    private bool TryValidate(out string error)
    {
        ResetValidationState();
        var errors = new List<string>();
        TextBox? firstInvalidField = null;

        ValidatePositiveField(IntervalTextBox, IntervalLabelTextBlock.Text, errors, ref firstInvalidField);
        ValidatePositiveField(FailureThresholdTextBox, FailureThresholdLabelTextBlock.Text, errors, ref firstInvalidField);
        ValidatePositiveField(RecoveryThresholdTextBox, RecoveryThresholdLabelTextBlock.Text, errors, ref firstInvalidField);
        ValidateNonNegativeField(CooldownTextBox, CooldownLabelTextBlock.Text, errors, ref firstInvalidField);

        if (CheckModeComboBox.SelectedValue is DnsHealthCheckMode.ResolveWithExpectedIp ||
            !string.IsNullOrWhiteSpace(ExpectedAddressesTextBox.Text))
        {
            try
            {
                _ = ParseExpectedAddresses(ExpectedAddressesTextBox.Text);
            }
            catch (InvalidDataException exception)
            {
                errors.Add(exception.Message);
                MarkFieldInvalid(ExpectedAddressesTextBox);
                firstInvalidField ??= ExpectedAddressesTextBox;
            }
        }

        error = string.Join(Environment.NewLine, errors);

        if (errors.Count == 0)
        {
            return true;
        }

        ValidationMessageTextBlock.Text = error;
        ValidationMessageBorder.Visibility = Visibility.Visible;

        if (ReferenceEquals(firstInvalidField, ExpectedAddressesTextBox))
        {
            ExpectedIpSectionBorder.Visibility = Visibility.Visible;
            ExpectedIpsExpander.IsExpanded = true;
        }

        firstInvalidField?.Focus();
        firstInvalidField?.SelectAll();
        return false;
    }

    private void ValidatePositiveField(
        TextBox textBox,
        string fieldLabel,
        ICollection<string> errors,
        ref TextBox? firstInvalidField)
    {
        if (int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
        {
            return;
        }

        errors.Add(localizer.Format("HealthValidationPositiveFormat", NormalizeFieldLabel(fieldLabel)));
        MarkFieldInvalid(textBox);
        firstInvalidField ??= textBox;
    }

    private void ValidateNonNegativeField(
        TextBox textBox,
        string fieldLabel,
        ICollection<string> errors,
        ref TextBox? firstInvalidField)
    {
        if (int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0)
        {
            return;
        }

        errors.Add(localizer.Format("HealthValidationNonNegativeFormat", NormalizeFieldLabel(fieldLabel)));
        MarkFieldInvalid(textBox);
        firstInvalidField ??= textBox;
    }

    private void ResetValidationState()
    {
        ValidationMessageBorder.Visibility = Visibility.Collapsed;
        ValidationMessageTextBlock.Text = string.Empty;

        foreach (var textBox in new[]
                 {
                     IntervalTextBox,
                     FailureThresholdTextBox,
                     RecoveryThresholdTextBox,
                     CooldownTextBox,
                     ExpectedAddressesTextBox,
                 })
        {
            textBox.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        }
    }

    private static void MarkFieldInvalid(TextBox textBox)
    {
        textBox.SetResourceReference(Control.BorderBrushProperty, "ErrorTextBrush");
    }

    private static string NormalizeFieldLabel(string value)
    {
        return value.Trim().TrimEnd(':');
    }

    private static string FormatExpectedAddresses(IReadOnlyDictionary<string, List<string>> expectedAddresses)
    {
        return string.Join(
            Environment.NewLine,
            expectedAddresses
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}={string.Join(", ", pair.Value)}"));
    }

    private Dictionary<string, List<string>> ParseExpectedAddresses(string rawText)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in rawText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);

            if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
            {
                throw new InvalidDataException(localizer.Format("HealthExpectedIpLineFormatError", line));
            }

            var domain = line[..separatorIndex].Trim().TrimEnd('.');
            var addresses = SplitValues(line[(separatorIndex + 1)..]);

            if (addresses.Count == 0)
            {
                throw new InvalidDataException(localizer.Format("HealthExpectedIpNoAddressesError", line));
            }

            result[domain] = addresses;
        }

        return result;
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

    private int ParsePositiveInt(string value, string fieldName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || result <= 0)
        {
            throw new InvalidDataException(localizer.Format("HealthValidationPositiveFormat", NormalizeFieldLabel(fieldName)));
        }

        return result;
    }

    private int ParseNonNegativeInt(string value, string fieldName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || result < 0)
        {
            throw new InvalidDataException(localizer.Format("HealthValidationNonNegativeFormat", NormalizeFieldLabel(fieldName)));
        }

        return result;
    }

    private sealed record ProfileOption(string? ProfileId, string DisplayName);

    private sealed record CheckModeOption(DnsHealthCheckMode Mode, string DisplayName);

    private sealed record ActionOption(DnsHealthFailureAction Action, string DisplayName);

    private sealed record ChainItem(
        int Index,
        string ProfileId,
        string DisplayName,
        string Details,
        bool CanMoveUp,
        bool CanMoveDown)
    {
        public int Position => Index + 1;
    }

    private void ApplyLocalization()
    {
        Title = localizer["HealthFailoverWindowTitle"];
        PageHeaderTextBlock.Text = localizer["HealthPageTitle"];
        PageSubtitleTextBlock.Text = localizer["HealthPageSubtitle"];
        MonitorTitleTextBlock.Text = localizer["HealthMonitorGroupHeader"];
        MonitorDescriptionTextBlock.Text = localizer["HealthMonitorDescription"];
        CheckSettingsHeaderTextBlock.Text = localizer["HealthCheckSettingsHeader"];
        IntervalLabelTextBlock.Text = localizer["HealthIntervalLabel"];
        FailureThresholdLabelTextBlock.Text = localizer["HealthFailureThresholdLabel"];
        RecoveryThresholdLabelTextBlock.Text = localizer["HealthRecoveryThresholdLabel"];
        CooldownLabelTextBlock.Text = localizer["HealthCooldownLabel"];
        CheckModeLabelTextBlock.Text = localizer["HealthCheckModeLabel"];
        ActionOnFailureLabelTextBlock.Text = localizer["HealthActionOnFailureLabel"];
        ThresholdHintTextBlock.Text = localizer["HealthCheckSettingsHint"];
        TestDomainsHeaderTextBlock.Text = localizer["HealthTestDomainsHeader"];
        TestDomainsHintTextBlock.Text = localizer["HealthTestDomainsHint"];
        DomainsInputHintTextBlock.Text = localizer["HealthDomainsInputHint"];
        AddDomainButton.Content = localizer["AddButton"];
        ExpectedIpsHeaderTextBlock.Text = localizer["HealthExpectedIpSectionHeader"];
        ExpectedIpsHintTextBlock.Text = localizer["HealthExpectedIpsHint"];
        FailoverChainHeaderTextBlock.Text = localizer["HealthFailoverChainHeader"];
        FailoverChainHintTextBlock.Text = localizer["HealthFailoverChainHint"];
        AddChainButton.Content = localizer["AddButton"];
        FallbackHeaderTextBlock.Text = localizer["HealthFallbackHeader"];
        FallbackDescriptionTextBlock.Text = localizer["HealthFallbackDescription"];
        FallbackProfileLabelTextBlock.Text = localizer["HealthFallbackProfileLabel"];
        CurrentHealthStateHeaderTextBlock.Text = localizer["HealthCurrentStateHeader"];
        ActiveProfileLabelTextBlock.Text = localizer["HealthStateActiveProfileLine"];
        FailuresMetricLabelTextBlock.Text = localizer["HealthStateFailuresLine"];
        SuccessesMetricLabelTextBlock.Text = localizer["HealthStateSuccessesLine"];
        LastCheckedMetricLabelTextBlock.Text = localizer["HealthStateLastCheckedLine"];
        LastFailoverMetricLabelTextBlock.Text = localizer["HealthStateLastFailoverLine"];
        StateDetailsHeaderTextBlock.Text = localizer["HealthStateDetailsHeader"];
        LastSuccessfulLabelTextBlock.Text = localizer["HealthStateLastSuccessfulLine"];
        LastFailureLabelTextBlock.Text = localizer["HealthStateLastFailureLine"];
        CooldownUntilLabelTextBlock.Text = localizer["HealthStateCooldownLine"];
        FailureReasonLabelTextBlock.Text = localizer["HealthStateFailureReasonLine"];
        LastActionLabelTextBlock.Text = localizer["HealthStateLastActionLine"];
        RunCheckButton.Content = localizer["RunCheckButton"];
        CancelButton.Content = localizer["CancelButton"];
        SaveButton.Content = localizer["SaveButton"];

        TestDomainInputTextBox.ToolTip = localizer["HealthDomainsInputHint"];
        ExpectedAddressesTextBox.ToolTip = localizer["HealthExpectedIpsHint"];
        AutomationProperties.SetName(EnabledCheckBox, localizer["HealthEnableMonitorCheckbox"]);
        AutomationProperties.SetHelpText(EnabledCheckBox, localizer["HealthMonitorDescription"]);
        AutomationProperties.SetName(TestDomainInputTextBox, localizer["HealthTestDomainsHeader"]);
        AutomationProperties.SetHelpText(TestDomainInputTextBox, localizer["HealthDomainsInputHint"]);
        AutomationProperties.SetName(ExpectedAddressesTextBox, localizer["HealthExpectedIpSectionHeader"]);
        AutomationProperties.SetHelpText(ExpectedAddressesTextBox, localizer["HealthExpectedIpsHint"]);
    }
}
