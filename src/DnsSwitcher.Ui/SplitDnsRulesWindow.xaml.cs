using System.IO;
using System.Windows;
using System.Windows.Controls;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Ui;

public partial class SplitDnsRulesWindow : Window
{
    private readonly WindowsDnsSwitcherHost host;
    private SplitDnsConfiguration configuration = SplitDnsConfiguration.Default;
    private IReadOnlyList<DnsProfile> staticProfiles = [];
    private bool suppressEnabledChanged;

    public SplitDnsRulesWindow(WindowsDnsSwitcherHost host)
    {
        InitializeComponent();
        WindowThemeService.Attach(this);
        this.host = host;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await LoadAsync().ConfigureAwait(true);
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        await LoadAsync().ConfigureAwait(true);
    }

    private void OnNewRuleClicked(object sender, RoutedEventArgs e)
    {
        RulesListBox.SelectedItem = null;
        RuleIdTextBox.Text = string.Empty;
        NamespaceTextBox.Text = string.Empty;
        TargetProfileComboBox.SelectedIndex = staticProfiles.Count > 0 ? 0 : -1;
        PriorityTextBox.Text = "0";
        RuleEnabledCheckBox.IsChecked = true;
        CommentTextBox.Text = string.Empty;
        NamespaceTextBox.Focus();
    }

    private async void OnSaveRuleClicked(object sender, RoutedEventArgs e)
    {
        await SaveRuleAsync().ConfigureAwait(true);
    }

    private async void OnDeleteRuleClicked(object sender, RoutedEventArgs e)
    {
        if (RulesListBox.SelectedItem is not RuleItem item)
        {
            SetStatus("Select a Split DNS rule to delete.");
            return;
        }

        var result = MessageBox.Show(
            $"Delete Split DNS rule '{item.Rule.Id}'?",
            "Split DNS",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await host.SplitDnsRuleService.RemoveRuleAsync(item.Rule.Id).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
            SetStatus($"Rule '{item.Rule.Id}' deleted.");
        }).ConfigureAwait(true);
    }

    private async void OnToggleRuleClicked(object sender, RoutedEventArgs e)
    {
        if (RulesListBox.SelectedItem is not RuleItem item)
        {
            SetStatus("Select a Split DNS rule to toggle.");
            return;
        }

        await RunAsync(async () =>
        {
            await host.SplitDnsRuleService.SetRuleEnabledAsync(item.Rule.Id, !item.Rule.Enabled).ConfigureAwait(true);
            await LoadAsync(item.Rule.Id).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    private async void OnSplitDnsEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (suppressEnabledChanged)
        {
            return;
        }

        await RunAsync(async () =>
        {
            configuration = configuration with { Enabled = SplitDnsEnabledCheckBox.IsChecked == true };
            await host.SplitDnsRuleService.SaveConfigurationAsync(configuration).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    private void OnRuleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RulesListBox.SelectedItem is not RuleItem item)
        {
            return;
        }

        RuleIdTextBox.Text = item.Rule.Id;
        NamespaceTextBox.Text = item.Rule.Namespace;
        TargetProfileComboBox.SelectedValue = item.Rule.ProfileId;
        PriorityTextBox.Text = item.Rule.Priority.ToString(System.Globalization.CultureInfo.InvariantCulture);
        RuleEnabledCheckBox.IsChecked = item.Rule.Enabled;
        CommentTextBox.Text = item.Rule.Comment ?? string.Empty;
    }

    private async void OnTestRuleClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TestDomainTextBox.Text))
        {
            SetStatus("Enter a domain to test.");
            return;
        }

        await RunAsync(async () =>
        {
            var match = await host.SplitDnsRuleService.TestMatchAsync(TestDomainTextBox.Text).ConfigureAwait(true);
            SetStatus(
                $"Domain: {match.Domain}{Environment.NewLine}" +
                $"Matched: {match.Matched}{Environment.NewLine}" +
                $"Rule: {match.Rule?.Id ?? "<none>"}{Environment.NewLine}" +
                $"Details: {match.Details}");
        }).ConfigureAwait(true);
    }

    private async void OnApplyRulesClicked(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            await SaveCurrentConfigurationAsync().ConfigureAwait(true);
            await host.AgentSplitDnsService.ApplyAsync(configuration).ConfigureAwait(true);
            SetStatus("Split DNS NRPT rules applied.");
        }).ConfigureAwait(true);
    }

    private async void OnResetRulesClicked(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Remove all DnsSwitcher-owned Windows NRPT rules?",
            "Split DNS",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await host.AgentSplitDnsService.ResetAsync().ConfigureAwait(true);
            SetStatus("DnsSwitcher-owned NRPT rules removed.");
        }).ConfigureAwait(true);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private async Task LoadAsync(string? selectedRuleId = null)
    {
        await RunAsync(async () =>
        {
            var appConfig = await host.ProfileService.GetConfigurationAsync().ConfigureAwait(true);
            staticProfiles = appConfig.Profiles
                .Where(profile => profile.Mode == ProfileMode.Static)
                .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            TargetProfileComboBox.ItemsSource = staticProfiles
                .Select(profile => new ProfileOption(profile.Id, $"{profile.Name} ({profile.Id})"))
                .ToArray();
            configuration = await host.SplitDnsRuleService.GetConfigurationAsync().ConfigureAwait(true);

            suppressEnabledChanged = true;
            SplitDnsEnabledCheckBox.IsChecked = configuration.Enabled;
            suppressEnabledChanged = false;

            RefreshRulesList(selectedRuleId);
            SetStatus(BuildConfigurationStatus());
        }).ConfigureAwait(true);
    }

    private async Task SaveRuleAsync()
    {
        await RunAsync(async () =>
        {
            var rule = BuildEditedRule();
            var rules = configuration.Rules.ToList();
            var selectedRuleId = (RulesListBox.SelectedItem as RuleItem)?.Rule.Id;
            var existingIndex = rules.FindIndex(existing =>
                string.Equals(existing.Id, selectedRuleId ?? rule.Id, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                rules[existingIndex] = rule;
            }
            else
            {
                rules.Add(rule);
            }

            configuration = configuration with
            {
                Enabled = SplitDnsEnabledCheckBox.IsChecked == true,
                Rules = rules,
            };

            await host.SplitDnsRuleService.SaveConfigurationAsync(configuration).ConfigureAwait(true);
            await LoadAsync(rule.Id).ConfigureAwait(true);
            SetStatus($"Rule '{rule.Id}' saved.");
        }).ConfigureAwait(true);
    }

    private async Task SaveCurrentConfigurationAsync()
    {
        configuration = configuration with { Enabled = SplitDnsEnabledCheckBox.IsChecked == true };
        await host.SplitDnsRuleService.SaveConfigurationAsync(configuration).ConfigureAwait(true);
    }

    private SplitDnsRule BuildEditedRule()
    {
        var namespaceValue = NamespaceTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(namespaceValue))
        {
            throw new InvalidDataException("Split DNS namespace/domain must not be empty.");
        }

        if (TargetProfileComboBox.SelectedValue is not string profileId || string.IsNullOrWhiteSpace(profileId))
        {
            throw new InvalidDataException("Select a target DNS profile.");
        }

        if (!int.TryParse(PriorityTextBox.Text, out var priority))
        {
            throw new InvalidDataException("Priority must be a number.");
        }

        var id = string.IsNullOrWhiteSpace(RuleIdTextBox.Text)
            ? CreateRuleId(namespaceValue)
            : RuleIdTextBox.Text.Trim();

        return new SplitDnsRule
        {
            Id = id,
            Namespace = namespaceValue,
            ProfileId = profileId,
            Enabled = RuleEnabledCheckBox.IsChecked == true,
            Priority = priority,
            Comment = string.IsNullOrWhiteSpace(CommentTextBox.Text) ? null : CommentTextBox.Text.Trim(),
        };
    }

    private string CreateRuleId(string namespaceValue)
    {
        var normalized = namespaceValue.Trim().TrimStart('*', '.').TrimEnd('.');
        var baseId = new string(normalized
            .Select(character => char.IsAsciiLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray())
            .Trim('-');

        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = "rule";
        }

        var candidate = baseId;
        var suffix = 2;

        while (configuration.Rules.Any(rule => string.Equals(rule.Id, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseId}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private void RefreshRulesList(string? selectedRuleId)
    {
        var items = configuration.Rules
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Namespace, StringComparer.OrdinalIgnoreCase)
            .Select(rule => new RuleItem(rule, ResolveProfileName(rule.ProfileId)))
            .ToArray();

        RulesListBox.ItemsSource = items;

        if (!string.IsNullOrWhiteSpace(selectedRuleId))
        {
            RulesListBox.SelectedItem = items.FirstOrDefault(item =>
                string.Equals(item.Rule.Id, selectedRuleId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private string ResolveProfileName(string profileId)
    {
        var profile = staticProfiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
        return profile?.Name ?? "missing profile";
    }

    private string BuildConfigurationStatus()
    {
        var enabledCount = configuration.Rules.Count(rule => rule.Enabled);
        return
            $"Split DNS enabled: {configuration.Enabled}{Environment.NewLine}" +
            $"Mode: {configuration.Mode}{Environment.NewLine}" +
            $"Default behavior: {configuration.DefaultBehavior}{Environment.NewLine}" +
            $"Rules: {configuration.Rules.Count}{Environment.NewLine}" +
            $"Enabled rules: {enabledCount}{Environment.NewLine}" +
            $"Config file: {host.Paths.SplitDnsRulesFilePath}";
    }

    private void SetBusy(bool busy)
    {
        IsEnabled = !busy;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
    }

    private async Task RunAsync(Func<Task> action)
    {
        SetBusy(true);

        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            SetStatus(FriendlyExceptionFormatter.ToUserMessage(exception));
            MessageBox.Show(
                FriendlyExceptionFormatter.ToUserMessage(exception),
                "Split DNS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetStatus(string message)
    {
        StatusTextBox.Text = message;
        StatusTextBox.ScrollToEnd();
    }

    private sealed record ProfileOption(string ProfileId, string DisplayName);

    private sealed record RuleItem(SplitDnsRule Rule, string ProfileName)
    {
        public override string ToString()
        {
            var enabledText = Rule.Enabled ? "enabled" : "disabled";
            return $"{Rule.Namespace} -> {ProfileName} ({Rule.ProfileId}) | {enabledText} | priority {Rule.Priority}";
        }
    }
}
