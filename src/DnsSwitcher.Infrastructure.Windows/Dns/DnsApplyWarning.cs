using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Infrastructure.Windows.Dns;

public enum DnsApplyWarningKind
{
    UnsupportedIpv4Skipped,
    UnsupportedIpv6Skipped,
}

public sealed record DnsApplyWarning(
    DnsApplyWarningKind Kind,
    string AdapterName,
    string ProfileId,
    string ProfileName);

public static class DnsApplyWarningBuilder
{
    public static IReadOnlyList<DnsApplyWarning> Build(DnsProfile profile, NetworkAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(adapter);

        if (profile.Mode == ProfileMode.Dhcp)
        {
            return [];
        }

        var warnings = new List<DnsApplyWarning>();

        if (profile.Ipv4.Count > 0 && !adapter.SupportedStacks.HasFlag(NetworkStackSupport.Ipv4))
        {
            warnings.Add(new DnsApplyWarning(
                DnsApplyWarningKind.UnsupportedIpv4Skipped,
                adapter.Name,
                profile.Id,
                profile.Name));
        }

        if (profile.Ipv6.Count > 0 && !adapter.SupportedStacks.HasFlag(NetworkStackSupport.Ipv6))
        {
            warnings.Add(new DnsApplyWarning(
                DnsApplyWarningKind.UnsupportedIpv6Skipped,
                adapter.Name,
                profile.Id,
                profile.Name));
        }

        return warnings;
    }
}
