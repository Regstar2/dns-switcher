using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Infrastructure.Windows.Tray;

public static class TrayTextFormatter
{
    private const int MaxNotifyIconTextLength = 63;
    private const int MaxStatusLabelLength = 34;
    private const int MaxAdapterLabelLength = 34;
    private const int MaxActionProfileNameLength = 18;
    private const int MaxProfileNameLength = 26;

    public static string BuildStatusMenuText(AppConfig configuration, DnsStatus status, AppLocalizer localizer)
    {
        return BuildDetailLine(
            localizer["TrayStatusLabel"],
            Trim(BuildStatusLabel(configuration, status, localizer), MaxStatusLabelLength));
    }

    public static string? BuildAdapterMenuText(DnsStatus status, TraySettings settings, AppLocalizer localizer)
    {
        if (!settings.ShowAdapterName)
        {
            return null;
        }

        return BuildDetailLine(
            localizer["TrayAdapterLabel"],
            Trim(status.AdapterName ?? localizer["NoAdapterSelected"], MaxAdapterLabelLength));
    }

    public static string BuildNotifyIconText(AppConfig configuration, DnsStatus status, TraySettings settings, AppLocalizer localizer)
    {
        var parts = new List<string>
        {
            localizer["DnsSwitcherTrayTitle"],
            BuildStatusLabel(configuration, status, localizer),
        };

        if (settings.ShowAdapterName && !string.IsNullOrWhiteSpace(status.AdapterName))
        {
            parts.Add(status.AdapterName);
        }

        return Trim(string.Join(" | ", parts), MaxNotifyIconTextLength);
    }

    public static string BuildErrorNotifyIconText(string message, AppLocalizer localizer)
    {
        return Trim($"{localizer["DnsSwitcherTrayTitle"]}: {localizer["TrayErrorStatus"]} - {message}", MaxNotifyIconTextLength);
    }

    public static string BuildOverviewDetails(
        AppConfig configuration,
        DnsStatus status,
        DnsHealthSettings healthSettings,
        DnsHealthState healthState,
        SplitDnsConfiguration splitDnsConfiguration,
        string? preferredProfileId,
        AppLocalizer localizer)
    {
        var lines = new[]
        {
            BuildDetailLine(localizer["TrayStatusLabel"], BuildStatusLabel(configuration, status, localizer)),
            BuildDetailLine(localizer["TrayAdapterLabel"], status.AdapterName ?? localizer["NoneValue"]),
            BuildDetailLine(localizer["TrayModeLabel"], status.Mode.ToString()),
            BuildDetailLine(localizer["TrayMatchedProfileLabel"], status.MatchedProfileId ?? localizer["NoneValue"]),
            BuildDetailLine(localizer["TraySelectedProfileLabel"], preferredProfileId ?? localizer["NoneValue"]),
            BuildDetailLine(
                localizer["HealthMonitorLabel"],
                $"{FormatEnabled(healthSettings.Enabled, localizer)} ({BuildHealthStatusText(healthState.Status, localizer)})"),
            BuildDetailLine(
                localizer["SplitDnsLabel"],
                $"{FormatEnabled(splitDnsConfiguration.Enabled, localizer)} ({BuildDetailLine(localizer["SplitDnsRulesLine"], splitDnsConfiguration.Rules.Count.ToString(System.Globalization.CultureInfo.CurrentCulture))})"),
            BuildDetailLine(localizer["TrayIpv4Label"], FormatServers(status.Ipv4.NameServers, localizer)),
            BuildDetailLine(localizer["TrayIpv6Label"], FormatServers(status.Ipv6.NameServers, localizer)),
        };

        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildHealthDetails(DnsHealthEvaluationResult result, AppLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(localizer);

        return
            $"{BuildDetailLine(localizer["HealthStateStatusLine"], BuildHealthStatusText(result.Status, localizer))}{Environment.NewLine}" +
            $"{BuildDetailLine(localizer["HealthResultSwitchedProfileLine"], result.SwitchedProfile ? localizer["YesValue"] : localizer["NoValue"])}{Environment.NewLine}" +
            $"{BuildDetailLine(localizer["HealthStateActiveProfileLine"], result.ActiveProfileId ?? localizer["NoneValue"])}{Environment.NewLine}" +
            $"{BuildDetailLine(localizer["HealthResultTargetProfileLine"], result.TargetProfileId ?? localizer["NoneValue"])}{Environment.NewLine}" +
            $"{BuildDetailLine(localizer["HealthStateLastActionLine"], result.State.LastAction ?? localizer["NoneValue"])}{Environment.NewLine}" +
            $"{BuildDetailLine(localizer["HealthStateFailureReasonLine"], result.State.LastFailureReason ?? localizer["NoneValue"])}{Environment.NewLine}" +
            $"{BuildDetailLine(localizer["HealthStateLastCheckedLine"], result.State.LastCheckedUtc?.ToString("O") ?? localizer["NeverValue"])}{Environment.NewLine}" +
            $"{BuildDetailLine(localizer["HealthStateCooldownLine"], result.State.CooldownUntilUtc?.ToString("O") ?? localizer["NoneValue"])}{Environment.NewLine}" +
            $"{Environment.NewLine}{result.Details}";
    }

    public static string BuildSplitDnsDetails(SplitDnsConfiguration configuration, AppLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(localizer);

        var lines = new List<string>
        {
            BuildDetailLine(localizer["SplitDnsEnabledLine"], FormatEnabled(configuration.Enabled, localizer)),
            BuildDetailLine(localizer["SplitDnsModeLine"], configuration.Mode.ToString()),
            BuildDetailLine(localizer["SplitDnsDefaultBehaviorLine"], configuration.DefaultBehavior.ToString()),
            BuildDetailLine(localizer["SplitDnsRulesLine"], configuration.Rules.Count.ToString(System.Globalization.CultureInfo.CurrentCulture)),
            string.Empty,
        };

        foreach (var rule in configuration.Rules
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Namespace, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add(
                $"{rule.Id}: {rule.Namespace} -> {rule.ProfileId} | " +
                $"{BuildDetailLine(localizer["SplitDnsRuleEnabledLabel"], FormatEnabled(rule.Enabled, localizer))} | " +
                $"{BuildDetailLine(localizer["SplitDnsPriorityListText"], rule.Priority.ToString(System.Globalization.CultureInfo.CurrentCulture))}" +
                $"{(string.IsNullOrWhiteSpace(rule.Comment) ? string.Empty : $" | {rule.Comment}")}");
        }

        if (configuration.Rules.Count == 0)
        {
            lines.Add(localizer["SplitDnsNoRulesConfigured"]);
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildEnableMenuText(DnsProfile? profile, AppLocalizer localizer)
    {
        return profile is null
            ? localizer["TrayEnableDns"]
            : $"{localizer["TrayEnableDns"]} ({Trim(profile.Name, MaxActionProfileNameLength)})";
    }

    public static string BuildSwitchNextMenuText(DnsProfile? profile, AppLocalizer localizer)
    {
        return profile is null
            ? localizer["TraySwitchNext"]
            : $"{localizer["TraySwitchNext"]} ({Trim(profile.Name, MaxActionProfileNameLength)})";
    }

    public static string BuildProfileMenuText(DnsProfile profile, bool isCurrent, bool isPreferred, AppLocalizer localizer)
    {
        var suffix = isCurrent
            ? $" [{localizer["ActiveSuffix"]}]"
            : isPreferred
                ? $" [{localizer["SelectedSuffix"]}]"
                : string.Empty;

        return $"{Trim(profile.Name, MaxProfileNameLength)}{suffix}";
    }

    public static string BuildStatusLabel(AppConfig configuration, DnsStatus status, AppLocalizer localizer)
    {
        var currentProfile = configuration.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, status.MatchedProfileId, StringComparison.OrdinalIgnoreCase));

        return currentProfile?.Name ?? status.Mode switch
        {
            DnsMode.Dhcp => localizer["DhcpStatus"],
            DnsMode.Manual => localizer["ManualDnsStatus"],
            DnsMode.Mixed => localizer["MixedDnsStatus"],
            _ => localizer["UnknownStatus"],
        };
    }

    public static string Trim(string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (maxLength <= 3 || value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }

    public static string BuildDetailLine(string label, string value)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(value);

        return label.EndsWith(':')
            ? $"{label} {value}"
            : $"{label}: {value}";
    }

    public static string BuildHealthStatusText(DnsHealthStatus status, AppLocalizer localizer)
    {
        return status switch
        {
            DnsHealthStatus.Healthy => localizer["HealthStatusHealthy"],
            DnsHealthStatus.Degraded => localizer["HealthStatusDegraded"],
            DnsHealthStatus.Failed => localizer["HealthStatusFailed"],
            DnsHealthStatus.Cooldown => localizer["HealthStatusCooldown"],
            _ => localizer["HealthStatusDisabled"],
        };
    }

    private static string FormatServers(IReadOnlyList<string> servers, AppLocalizer localizer)
    {
        return servers.Count == 0 ? localizer["NoneValue"] : string.Join(", ", servers);
    }

    private static string FormatEnabled(bool enabled, AppLocalizer localizer)
    {
        return enabled ? localizer["EnabledValue"] : localizer["DisabledValue"];
    }
}
