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
        return $"{localizer["TrayStatusLabel"]}: {Trim(BuildStatusLabel(configuration, status, localizer), MaxStatusLabelLength)}";
    }

    public static string? BuildAdapterMenuText(DnsStatus status, TraySettings settings, AppLocalizer localizer)
    {
        if (!settings.ShowAdapterName)
        {
            return null;
        }

        return $"{localizer["TrayAdapterLabel"]}: {Trim(status.AdapterName ?? localizer["NoAdapterSelected"], MaxAdapterLabelLength)}";
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
}
