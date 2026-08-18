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
    private readonly AppLocalizer localizer;
    private SplitDnsConfiguration configuration = SplitDnsConfiguration.Default;
    private IReadOnlyList<DnsProfile> staticProfiles = [];
    private bool suppressEnabledChanged;
    private bool suppressRuleEnabledChanged;
    private bool suppressSelectionChanged;
    private bool isCreatingNew;
    private bool pendingApply;
    private string? editingRuleId;
    private string? lastTestDetails;

    public SplitDnsRulesWindow(WindowsDnsSwitcherHost host, AppLocalizer localizer)
    {
        InitializeComponent();
        WindowThemeService.Attach(this);
        this.host = host;
        this.localizer = localizer;
        ApplyLocalization();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await LoadAsync().ConfigureAwait(true);
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        await LoadAsync(editingRuleId).ConfigureAwait(true);
    }

    private void OnRulesSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        RefreshRulesList(editingRuleId);
        UpdateEditorState();
    }

    private void OnNewRuleClicked(object sender, RoutedEventArgs e)
    {
        suppressSelectionChanged = true;
        RulesListBox.SelectedItem = null;
        suppressSelectionChanged = false;

        isCreatingNew = true;
        editingRuleId = null;
        RuleIdTextBox.Text = string.Empty;
        NamespaceTextBox.Text = string.Empty;
        TargetProfileComboBox.SelectedIndex = staticProfiles.Count > 0 ? 0 : -1;
        PriorityTextBox.Text = "0";
        SetRuleEnabledEditorValue(true);
        CommentTextBox.Text = string.Empty;
        TestResultTextBlock.Text = string.Empty;
        EditorContextTextBlock.Text = localizer["NewButton"];
        UpdateEditorState();
        NamespaceTextBox.Focus();
    }

    private void OnCancelRuleClicked(object sender, RoutedEventArgs e)
    {
        if (isCreatingNew)
        {
            isCreatingNew = false;
            editingRuleId = null;

            if (RulesListBox.Items.Count > 0)
            {
                RulesListBox.SelectedIndex = 0;
            }
            else
            {
                ClearEditor();
            }
        }
        else if (!string.IsNullOrWhiteSpace(editingRuleId))
        {
            var rule = configuration.Rules.FirstOrDefault(rule =>
                string.Equals(rule.Id, editingRuleId, StringComparison.OrdinalIgnoreCase));

            if (rule is not null)
            {
                PopulateEditor(rule);
            }
        }

        UpdateEditorState();
    }

    private async void OnSaveRuleClicked(object sender, RoutedEventArgs e)
    {
        await SaveRuleAsync().ConfigureAwait(true);
    }

    private async void OnDeleteRuleClicked(object sender, RoutedEventArgs e)
    {
        if (RulesListBox.SelectedItem is not RuleItem item)
        {
            SetStatus(localizer["SplitDnsSelectRuleDelete"]);
            return;
        }

        var result = MessageBox.Show(
            localizer.Format("SplitDnsDeleteRuleConfirmFormat", item.Rule.Id),
            localizer["SplitDnsTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await host.SplitDnsRuleService.RemoveRuleAsync(item.Rule.Id).ConfigureAwait(true);
            configuration = await host.SplitDnsRuleService.GetConfigurationAsync().ConfigureAwait(true);
            isCreatingNew = false;
            editingRuleId = null;
            MarkPendingApply();
            RefreshRulesList(null, selectFirstWhenNone: true);

            if (RulesListBox.SelectedItem is not RuleItem)
            {
                ClearEditor();
            }

            UpdateConfigurationState();
            UpdateTechnicalDetails();
            UpdateEditorState();
            SetStatus(localizer.Format("SplitDnsRuleDeletedFormat", item.Rule.Id));
        }).ConfigureAwait(true);
    }

    private async void OnRuleEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (suppressRuleEnabledChanged || isCreatingNew || string.IsNullOrWhiteSpace(editingRuleId))
        {
            return;
        }

        var rule = configuration.Rules.FirstOrDefault(rule =>
            string.Equals(rule.Id, editingRuleId, StringComparison.OrdinalIgnoreCase));

        if (rule is null)
        {
            return;
        }

        var enabled = RuleEnabledCheckBox.IsChecked == true;

        if (rule.Enabled == enabled)
        {
            return;
        }

        var ruleId = rule.Id;
        await RunAsync(async () =>
        {
            await host.SplitDnsRuleService.SetRuleEnabledAsync(ruleId, enabled).ConfigureAwait(true);
            configuration = configuration with
            {
                Rules = configuration.Rules
                    .Select(existing => string.Equals(existing.Id, ruleId, StringComparison.OrdinalIgnoreCase)
                        ? existing with { Enabled = enabled }
                        : existing)
                    .ToArray(),
            };

            MarkPendingApply();
            RefreshRulesListPreservingEditor(ruleId);
            UpdateTechnicalDetails();
            UpdateEditorState();
            SetStatus(localizer.Format("SplitDnsRuleSavedFormat", ruleId));
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
            MarkPendingApply();
            UpdateConfigurationState();
            UpdateTechnicalDetails();
            SetStatus(
                $"{localizer["SplitDnsEnabledLine"]} " +
                $"{(configuration.Enabled ? localizer["YesValue"] : localizer["NoValue"])}");
        }).ConfigureAwait(true);
    }

    private void OnRuleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionChanged)
        {
            return;
        }

        if (RulesListBox.SelectedItem is RuleItem item)
        {
            isCreatingNew = false;
            editingRuleId = item.Rule.Id;
            PopulateEditor(item.Rule);
        }

        UpdateEditorState();
    }

    private async void OnTestRuleClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TestDomainTextBox.Text))
        {
            SetStatus(localizer["SplitDnsEnterDomainToTest"]);
            return;
        }

        await RunAsync(async () =>
        {
            var match = await host.SplitDnsRuleService.TestMatchAsync(TestDomainTextBox.Text).ConfigureAwait(true);
            TestResultTextBlock.Text = match.Matched && match.Rule is not null
                ? $"{match.Domain} → {match.Rule.Namespace} → {ResolveProfileName(match.Rule.ProfileId)}"
                : $"{match.Domain} → {localizer["NoneValue"]}";
            lastTestDetails = match.Details;
            UpdateTechnicalDetails();
        }).ConfigureAwait(true);
    }

    private async void OnApplyRulesClicked(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            await SaveCurrentConfigurationAsync().ConfigureAwait(true);
            await host.AgentSplitDnsService.ApplyAsync(configuration).ConfigureAwait(true);
            pendingApply = false;
            UpdatePendingApplyState();
            UpdateTechnicalDetails();
            SetStatus(localizer["SplitDnsAppliedStatus"]);
        }).ConfigureAwait(true);
    }

    private async void OnResetRulesClicked(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            localizer["SplitDnsResetRulesConfirm"],
            localizer["SplitDnsTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await host.AgentSplitDnsService.ResetAsync().ConfigureAwait(true);
            pendingApply = configuration.Enabled && configuration.Rules.Count > 0;
            UpdatePendingApplyState();
            SetStatus(localizer["SplitDnsResetStatus"]);
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

            isCreatingNew = false;
            editingRuleId = selectedRuleId;
            RefreshRulesList(selectedRuleId, selectFirstWhenNone: selectedRuleId is null);

            if (RulesListBox.SelectedItem is not RuleItem)
            {
                var selectedRule = !string.IsNullOrWhiteSpace(selectedRuleId)
                    ? configuration.Rules.FirstOrDefault(rule =>
                        string.Equals(rule.Id, selectedRuleId, StringComparison.OrdinalIgnoreCase))
                    : null;

                if (selectedRule is not null)
                {
                    editingRuleId = selectedRule.Id;
                    PopulateEditor(selectedRule);
                }
                else if (configuration.Rules.Count == 0)
                {
                    editingRuleId = null;
                    ClearEditor();
                }
            }

            UpdateConfigurationState();
            UpdatePendingApplyState();
            UpdateTechnicalDetails();
            UpdateEditorState();
            SetStatus(localizer["ReadyStatus"]);
        }).ConfigureAwait(true);
    }

    private async Task SaveRuleAsync()
    {
        await RunAsync(async () =>
        {
            var rule = BuildEditedRule();
            var rules = configuration.Rules.ToList();
            var existingIndex = rules.FindIndex(existing =>
                string.Equals(existing.Id, editingRuleId ?? rule.Id, StringComparison.OrdinalIgnoreCase));

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
            isCreatingNew = false;
            editingRuleId = rule.Id;
            MarkPendingApply();
            RefreshRulesList(rule.Id);
            UpdateConfigurationState();
            UpdateTechnicalDetails();
            UpdateEditorState();
            SetStatus(localizer.Format("SplitDnsRuleSavedFormat", rule.Id));
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
            throw new InvalidDataException(localizer["SplitDnsNamespaceRequired"]);
        }

        if (TargetProfileComboBox.SelectedValue is not string profileId || string.IsNullOrWhiteSpace(profileId))
        {
            throw new InvalidDataException(localizer["SplitDnsTargetProfileRequired"]);
        }

        if (!int.TryParse(PriorityTextBox.Text, out var priority))
        {
            throw new InvalidDataException(localizer["SplitDnsPriorityNumberRequired"]);
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

    private void RefreshRulesList(string? selectedRuleId, bool selectFirstWhenNone = false)
    {
        var query = RulesSearchTextBox.Text.Trim();
        var items = configuration.Rules
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Namespace, StringComparer.OrdinalIgnoreCase)
            .Select(rule => new RuleItem(
                rule,
                ResolveProfileName(rule.ProfileId),
                rule.Enabled ? localizer["EnabledValue"] : localizer["DisabledValue"],
                localizer["SplitDnsPriorityListText"]))
            .Where(item => MatchesSearch(item, query))
            .ToArray();

        suppressSelectionChanged = true;
        RulesListBox.ItemsSource = items;

        if (!string.IsNullOrWhiteSpace(selectedRuleId))
        {
            RulesListBox.SelectedItem = items.FirstOrDefault(item =>
                string.Equals(item.Rule.Id, selectedRuleId, StringComparison.OrdinalIgnoreCase));
        }
        else if (selectFirstWhenNone && items.Length > 0)
        {
            RulesListBox.SelectedIndex = 0;
        }

        suppressSelectionChanged = false;

        if (RulesListBox.SelectedItem is RuleItem selectedItem && !isCreatingNew)
        {
            editingRuleId = selectedItem.Rule.Id;
            PopulateEditor(selectedItem.Rule);
        }

        RulesCountTextBlock.Text = string.IsNullOrWhiteSpace(query)
            ? configuration.Rules.Count.ToString(System.Globalization.CultureInfo.CurrentCulture)
            : $"{items.Length}/{configuration.Rules.Count}";

        EmptyStatePanel.Visibility = items.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateTextBlock.Text = configuration.Rules.Count == 0
            ? localizer["SplitDnsNoRulesConfigured"]
            : localizer["NoneValue"];
        EmptyCreateRuleButton.Visibility = configuration.Rules.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void RefreshRulesListPreservingEditor(string? selectedRuleId)
    {
        suppressSelectionChanged = true;
        var query = RulesSearchTextBox.Text.Trim();
        var items = configuration.Rules
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Namespace, StringComparer.OrdinalIgnoreCase)
            .Select(rule => new RuleItem(
                rule,
                ResolveProfileName(rule.ProfileId),
                rule.Enabled ? localizer["EnabledValue"] : localizer["DisabledValue"],
                localizer["SplitDnsPriorityListText"]))
            .Where(item => MatchesSearch(item, query))
            .ToArray();

        RulesListBox.ItemsSource = items;
        RulesListBox.SelectedItem = !string.IsNullOrWhiteSpace(selectedRuleId)
            ? items.FirstOrDefault(item => string.Equals(item.Rule.Id, selectedRuleId, StringComparison.OrdinalIgnoreCase))
            : null;
        suppressSelectionChanged = false;

        RulesCountTextBlock.Text = string.IsNullOrWhiteSpace(query)
            ? configuration.Rules.Count.ToString(System.Globalization.CultureInfo.CurrentCulture)
            : $"{items.Length}/{configuration.Rules.Count}";
        EmptyStatePanel.Visibility = items.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateTextBlock.Text = configuration.Rules.Count == 0
            ? localizer["SplitDnsNoRulesConfigured"]
            : localizer["NoneValue"];
        EmptyCreateRuleButton.Visibility = configuration.Rules.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static bool MatchesSearch(RuleItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return item.Rule.Namespace.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Rule.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Rule.ProfileId.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.ProfileName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveProfileName(string profileId)
    {
        var profile = staticProfiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
        return profile?.Name ?? localizer["MissingProfileValue"];
    }

    private void PopulateEditor(SplitDnsRule rule)
    {
        RuleIdTextBox.Text = rule.Id;
        NamespaceTextBox.Text = rule.Namespace;
        TargetProfileComboBox.SelectedValue = rule.ProfileId;
        PriorityTextBox.Text = rule.Priority.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SetRuleEnabledEditorValue(rule.Enabled);
        CommentTextBox.Text = rule.Comment ?? string.Empty;
        EditorContextTextBlock.Text = rule.Namespace;
    }

    private void ClearEditor()
    {
        RuleIdTextBox.Text = string.Empty;
        NamespaceTextBox.Text = string.Empty;
        TargetProfileComboBox.SelectedIndex = -1;
        PriorityTextBox.Text = "0";
        SetRuleEnabledEditorValue(false);
        CommentTextBox.Text = string.Empty;
        EditorContextTextBlock.Text = localizer["NoneValue"];
        TestResultTextBlock.Text = string.Empty;
    }

    private void SetRuleEnabledEditorValue(bool enabled)
    {
        suppressRuleEnabledChanged = true;
        RuleEnabledCheckBox.IsChecked = enabled;
        suppressRuleEnabledChanged = false;
    }

    private void UpdateEditorState()
    {
        var hasEditorTarget = isCreatingNew || !string.IsNullOrWhiteSpace(editingRuleId);
        EditorFormPanel.IsEnabled = hasEditorTarget;
        RuleEnabledCheckBox.IsEnabled = hasEditorTarget;
        SaveRuleButton.IsEnabled = hasEditorTarget;
        CancelRuleButton.IsEnabled = hasEditorTarget;
        DeleteRuleButton.IsEnabled = !isCreatingNew && RulesListBox.SelectedItem is RuleItem;
    }

    private void UpdateConfigurationState()
    {
        ConfigurationStateTextBlock.Text = configuration.Enabled
            ? localizer["EnabledValue"]
            : localizer["DisabledValue"];
    }

    private void MarkPendingApply()
    {
        pendingApply = true;
        UpdatePendingApplyState();
    }

    private void UpdatePendingApplyState()
    {
        PendingApplyBorder.Visibility = pendingApply ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateTechnicalDetails()
    {
        var details = BuildConfigurationStatus();

        if (!string.IsNullOrWhiteSpace(lastTestDetails))
        {
            details += Environment.NewLine + Environment.NewLine +
                       $"{localizer["SplitDnsDetailsLine"]} {lastTestDetails}";
        }

        TechnicalDetailsTextBlock.Text = details;
    }

    private string BuildConfigurationStatus()
    {
        var enabledCount = configuration.Rules.Count(rule => rule.Enabled);
        return
            $"{localizer["SplitDnsEnabledLine"]} {(configuration.Enabled ? localizer["YesValue"] : localizer["NoValue"])}{Environment.NewLine}" +
            $"{localizer["SplitDnsModeLine"]} {configuration.Mode}{Environment.NewLine}" +
            $"{localizer["SplitDnsDefaultBehaviorLine"]} {configuration.DefaultBehavior}{Environment.NewLine}" +
            $"{localizer["SplitDnsRulesLine"]} {configuration.Rules.Count}{Environment.NewLine}" +
            $"{localizer["SplitDnsEnabledRulesLine"]} {enabledCount}{Environment.NewLine}" +
            $"{localizer["SplitDnsConfigFileLine"]} {host.Paths.SplitDnsRulesFilePath}";
    }

    private void SetBusy(bool busy)
    {
        IsEnabled = !busy;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
        BusyTextBlock.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
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
                localizer["SplitDnsTitle"],
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
        StatusTextBlock.Text = message;
    }

    private void ApplyLocalization()
    {
        Title = localizer["SplitDnsTitle"];
        PageTitleTextBlock.Text = localizer["SplitDnsTitle"];
        PageSubtitleTextBlock.Text = localizer["SettingsSplitDnsDescription"];
        ConfigurationStateLabelTextBlock.Text = localizer["SplitDnsLabel"];
        SplitDnsWarningTextBlock.Text = localizer["SplitDnsWarningText"];
        SplitDnsEnabledLabelTextBlock.Text = localizer["SplitDnsLabel"];
        RulesHeaderTextBlock.Text = localizer["SplitDnsRulesHeader"];
        NewRuleButton.Content = localizer["NewButton"];
        EmptyCreateRuleButton.Content = localizer["NewButton"];
        DeleteRuleButton.Content = localizer["DeleteProfileButton"];
        RefreshButton.Content = localizer["ReloadButton"];
        RuleEditorHeaderTextBlock.Text = localizer["SplitDnsRuleEditorHeader"];
        RuleIdLabelTextBlock.Text = localizer["SplitDnsRuleIdLabel"];
        NamespaceLabelTextBlock.Text = localizer["SplitDnsNamespaceLabel"];
        NamespaceHintTextBlock.Text = localizer["SplitDnsNamespaceExamplesTooltip"];
        TargetProfileLabelTextBlock.Text = localizer["SplitDnsTargetProfileLabel"];
        PriorityLabelTextBlock.Text = localizer["SplitDnsPriorityLabel"];
        PriorityHintTextBlock.Visibility = Visibility.Collapsed;
        RuleEnabledLabelTextBlock.Text = localizer["SplitDnsRuleEnabledLabel"];
        CommentLabelTextBlock.Text = localizer["SplitDnsCommentLabel"];
        SaveRuleButton.Content = localizer["SaveButton"];
        CancelRuleButton.Content = localizer["CancelButton"];
        TestHeaderTextBlock.Text = localizer["SplitDnsTestStatusHeader"];
        TestRuleButton.Content = localizer["TestButton"];
        DetailsExpander.Header = localizer["SplitDnsDetailsLine"];
        ApplyRulesButton.Content = localizer["ApplyButton"];
        ResetRulesButton.Content = localizer["TraySplitDnsReset"];
        CloseButton.Content = localizer["CloseButton"];
        PendingApplyTextBlock.Text = localizer["TraySplitDnsApply"];
        BusyTextBlock.Text = localizer["WorkingStatus"];
        NamespaceTextBox.ToolTip = localizer["SplitDnsNamespaceExamplesTooltip"];
        TestDomainTextBox.ToolTip = localizer["SplitDnsTestDomainTooltip"];
        EmptyStateTextBlock.Text = localizer["SplitDnsNoRulesConfigured"];
        EditorContextTextBlock.Text = localizer["NoneValue"];
    }

    private sealed record ProfileOption(string ProfileId, string DisplayName);

    private sealed record RuleItem(
        SplitDnsRule Rule,
        string ProfileName,
        string EnabledText,
        string PriorityText);
}
