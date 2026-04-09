using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Configuration;

namespace DnsSwitcher.Infrastructure.Windows.Tray;

public static class TrayTextFormatter
{
    private const int MaxNotifyIconTextLength = 63;
    private const int MaxStatusLabelLength = 34;
    private const int MaxAdapterLabelLength = 34;
    private const int MaxActionProfileNameLength = 18;
    private const int MaxProfileNameLength = 26;

    public static string BuildStatusMenuText(AppConfig configuration, DnsStatus status)
    {
        return $"Status: {Trim(BuildStatusLabel(configuration, status), MaxStatusLabelLength)}";
    }

    public static string? BuildAdapterMenuText(DnsStatus status, TraySettings settings)
    {
        if (!settings.ShowAdapterName)
        {
            return null;
        }

        return $"Adapter: {Trim(status.AdapterName ?? "no adapter selected", MaxAdapterLabelLength)}";
    }

    public static string BuildNotifyIconText(AppConfig configuration, DnsStatus status, TraySettings settings)
    {
        var parts = new List<string>
        {
            "DnsSwitcher",
            BuildStatusLabel(configuration, status),
        };

        if (settings.ShowAdapterName && !string.IsNullOrWhiteSpace(status.AdapterName))
        {
            parts.Add(status.AdapterName);
        }

        return Trim(string.Join(" | ", parts), MaxNotifyIconTextLength);
    }

    public static string BuildErrorNotifyIconText(string message)
    {
        return Trim($"DnsSwitcher: error - {message}", MaxNotifyIconTextLength);
    }

    public static string BuildEnableMenuText(DnsProfile? profile)
    {
        return profile is null
            ? "Enable DNS"
            : $"Enable DNS ({Trim(profile.Name, MaxActionProfileNameLength)})";
    }

    public static string BuildSwitchNextMenuText(DnsProfile? profile)
    {
        return profile is null
            ? "Switch Next"
            : $"Switch Next ({Trim(profile.Name, MaxActionProfileNameLength)})";
    }

    public static string BuildProfileMenuText(DnsProfile profile, bool isCurrent, bool isPreferred)
    {
        var suffix = isCurrent
            ? " [active]"
            : isPreferred
                ? " [selected]"
                : string.Empty;

        return $"{Trim(profile.Name, MaxProfileNameLength)}{suffix}";
    }

    public static string BuildStatusLabel(AppConfig configuration, DnsStatus status)
    {
        var currentProfile = configuration.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, status.MatchedProfileId, StringComparison.OrdinalIgnoreCase));

        return currentProfile?.Name ?? status.Mode switch
        {
            DnsMode.Dhcp => "DHCP",
            DnsMode.Manual => "Manual DNS",
            DnsMode.Mixed => "Mixed DNS",
            _ => "Unknown",
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
