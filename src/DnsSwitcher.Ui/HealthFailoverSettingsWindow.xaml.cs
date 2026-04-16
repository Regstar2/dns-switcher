using System.IO;
using System.Windows;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Ui;

public partial class HealthFailoverSettingsWindow : Window
{
    private static readonly char[] ValueSeparators = ['\r', '\n', ',', ';'];
    private readonly IReadOnlyList<DnsProfile> profiles;
    private readonly List<string> failoverChain;

    public HealthFailoverSettingsWindow(
        DnsHealthSettings settings,
        DnsHealthState state,
        IReadOnlyList<DnsProfile> profiles)
    {
        InitializeComponent();
        WindowThemeService.Attach(this);
        this.profiles = profiles;
        failoverChain = settings.FailoverChain.ToList();

        var profileOptions = BuildProfileOptions(profiles).ToArray();
        FallbackProfileComboBox.ItemsSource = new[] { new ProfileOption(null, "<none>") }.Concat(profileOptions).ToArray();
        ChainProfileComboBox.ItemsSource = profileOptions;
        CheckModeComboBox.ItemsSource = BuildCheckModeOptions();
        ActionComboBox.ItemsSource = BuildActionOptions();

        EnabledCheckBox.IsChecked = settings.Enabled;
        IntervalTextBox.Text = settings.MonitorIntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        FailureThresholdTextBox.Text = settings.FailureThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture);
        RecoveryThresholdTextBox.Text = settings.RecoveryThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture);
        CooldownTextBox.Text = settings.CooldownSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        CheckModeComboBox.SelectedValue = settings.CheckMode;
        ActionComboBox.SelectedValue = settings.ActionOnFailure;
        FallbackProfileComboBox.SelectedValue = settings.FallbackProfileId;
        TestDomainsTextBox.Text = string.Join(Environment.NewLine, settings.TestDomains);
        ExpectedAddressesTextBox.Text = FormatExpectedAddresses(settings.ExpectedAddresses);
        StateTextBox.Text = BuildStateText(state);
        RefreshChainList();
    }

    public bool RunCheckRequested { get; private set; }

    public DnsHealthSettings EditedSettings => new()
    {
        Enabled = EnabledCheckBox.IsChecked == true,
        MonitorIntervalSeconds = ParsePositiveInt(IntervalTextBox.Text, "Monitor interval"),
        FailureThreshold = ParsePositiveInt(FailureThresholdTextBox.Text, "Failure threshold"),
        RecoveryThreshold = ParsePositiveInt(RecoveryThresholdTextBox.Text, "Recovery threshold"),
        CooldownSeconds = ParseNonNegativeInt(CooldownTextBox.Text, "Cooldown"),
        CheckMode = CheckModeComboBox.SelectedValue is DnsHealthCheckMode mode ? mode : DnsHealthCheckMode.ResolveOnly,
        ActionOnFailure = ActionComboBox.SelectedValue is DnsHealthFailureAction action ? action : DnsHealthFailureAction.NotifyOnly,
        FallbackProfileId = FallbackProfileComboBox.SelectedValue as string,
        FailoverChain = failoverChain.ToList(),
        TestDomains = SplitValues(TestDomainsTextBox.Text),
        ExpectedAddresses = ParseExpectedAddresses(ExpectedAddressesTextBox.Text),
    };

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
        if (FailoverChainListBox.SelectedItem is not ChainItem item)
        {
            return;
        }

        failoverChain.RemoveAt(item.Index);
        RefreshChainList();
    }

    private void OnMoveChainUpClicked(object sender, RoutedEventArgs e)
    {
        MoveSelectedChainItem(-1);
    }

    private void OnMoveChainDownClicked(object sender, RoutedEventArgs e)
    {
        MoveSelectedChainItem(1);
    }

    private void OnRunCheckClicked(object sender, RoutedEventArgs e)
    {
        if (!TryValidate(out var error))
        {
            MessageBox.Show(error, "DNS Health Failover", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RunCheckRequested = true;
        DialogResult = true;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (!TryValidate(out var error))
        {
            MessageBox.Show(error, "DNS Health Failover", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private bool TryValidate(out string error)
    {
        try
        {
            _ = EditedSettings;
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private void MoveSelectedChainItem(int direction)
    {
        if (FailoverChainListBox.SelectedItem is not ChainItem item)
        {
            return;
        }

        var targetIndex = item.Index + direction;

        if (targetIndex < 0 || targetIndex >= failoverChain.Count)
        {
            return;
        }

        (failoverChain[item.Index], failoverChain[targetIndex]) = (failoverChain[targetIndex], failoverChain[item.Index]);
        RefreshChainList(targetIndex);
    }

    private void RefreshChainList(int selectedIndex = -1)
    {
        FailoverChainListBox.ItemsSource = failoverChain
            .Select((profileId, index) => new ChainItem(index, profileId, ResolveProfileDisplayName(profileId)))
            .ToArray();

        if (selectedIndex >= 0 && selectedIndex < FailoverChainListBox.Items.Count)
        {
            FailoverChainListBox.SelectedIndex = selectedIndex;
        }
    }

    private string ResolveProfileDisplayName(string profileId)
    {
        var profile = profiles.FirstOrDefault(profile => string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
        return profile is null ? $"{profileId} (missing)" : $"{profile.Name} ({profile.Id})";
    }

    private static IReadOnlyList<ProfileOption> BuildProfileOptions(IReadOnlyList<DnsProfile> profiles)
    {
        return profiles
            .Where(profile => profile.Mode == ProfileMode.Static)
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new ProfileOption(profile.Id, $"{profile.Name} ({profile.Id})"))
            .ToArray();
    }

    private static IReadOnlyList<CheckModeOption> BuildCheckModeOptions()
    {
        return
        [
            new CheckModeOption(DnsHealthCheckMode.ResolveOnly, "Resolve only"),
            new CheckModeOption(DnsHealthCheckMode.ResolveWithExpectedIp, "Resolve + expected IP"),
        ];
    }

    private static IReadOnlyList<ActionOption> BuildActionOptions()
    {
        return
        [
            new ActionOption(DnsHealthFailureAction.NotifyOnly, "Notify only"),
            new ActionOption(DnsHealthFailureAction.SwitchToNextProfile, "Switch to next profile"),
            new ActionOption(DnsHealthFailureAction.SwitchToFallbackProfile, "Switch to fallback profile"),
        ];
    }

    private static string BuildStateText(DnsHealthState state)
    {
        return
            $"Status: {state.Status}{Environment.NewLine}" +
            $"Active profile: {state.ActiveProfileId ?? "<none>"}{Environment.NewLine}" +
            $"Last failover profile: {state.LastFailoverProfileId ?? "<none>"}{Environment.NewLine}" +
            $"Consecutive failures: {state.ConsecutiveFailures}{Environment.NewLine}" +
            $"Consecutive successes: {state.ConsecutiveSuccesses}{Environment.NewLine}" +
            $"Last checked UTC: {state.LastCheckedUtc?.ToString("O") ?? "<never>"}{Environment.NewLine}" +
            $"Last successful UTC: {state.LastSuccessfulCheckUtc?.ToString("O") ?? "<never>"}{Environment.NewLine}" +
            $"Last failure UTC: {state.LastFailureUtc?.ToString("O") ?? "<never>"}{Environment.NewLine}" +
            $"Cooldown until UTC: {state.CooldownUntilUtc?.ToString("O") ?? "<none>"}{Environment.NewLine}" +
            $"Last failure reason: {state.LastFailureReason ?? "<none>"}{Environment.NewLine}" +
            $"Last action: {state.LastAction ?? "<none>"}";
    }

    private static string FormatExpectedAddresses(IReadOnlyDictionary<string, List<string>> expectedAddresses)
    {
        return string.Join(
            Environment.NewLine,
            expectedAddresses
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}={string.Join(", ", pair.Value)}"));
    }

    private static Dictionary<string, List<string>> ParseExpectedAddresses(string rawText)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in rawText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);

            if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
            {
                throw new InvalidDataException($"Expected IP line must use 'domain=ip1,ip2': {line}");
            }

            var domain = line[..separatorIndex].Trim().TrimEnd('.');
            var addresses = SplitValues(line[(separatorIndex + 1)..]);

            if (addresses.Count == 0)
            {
                throw new InvalidDataException($"Expected IP line has no addresses: {line}");
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

    private static int ParsePositiveInt(string value, string fieldName)
    {
        if (!int.TryParse(value, out var result) || result <= 0)
        {
            throw new InvalidDataException($"{fieldName} must be a positive number.");
        }

        return result;
    }

    private static int ParseNonNegativeInt(string value, string fieldName)
    {
        if (!int.TryParse(value, out var result) || result < 0)
        {
            throw new InvalidDataException($"{fieldName} must be zero or a positive number.");
        }

        return result;
    }

    private sealed record ProfileOption(string? ProfileId, string DisplayName);

    private sealed record CheckModeOption(DnsHealthCheckMode Mode, string DisplayName);

    private sealed record ActionOption(DnsHealthFailureAction Action, string DisplayName);

    private sealed record ChainItem(int Index, string ProfileId, string DisplayName)
    {
        public override string ToString() => $"{Index + 1}. {DisplayName}";
    }
}
