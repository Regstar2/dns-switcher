using System.Net;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Services;

public static class DnsStatusMatcher
{
    public static DnsProfile? MatchProfile(AppConfig configuration, DnsStatus status)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(status);

        return configuration.Profiles.FirstOrDefault(profile => Matches(profile, status));
    }

    public static bool Matches(DnsProfile profile, DnsStatus status)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(status);

        return profile.Mode switch
        {
            ProfileMode.Dhcp => MatchesDhcp(status),
            ProfileMode.Static => MatchesStatic(profile, status),
            _ => false,
        };
    }

    private static bool MatchesDhcp(DnsStatus status)
    {
        return status.Mode == DnsMode.Dhcp
            && status.Ipv4.Mode is DnsMode.Dhcp or DnsMode.Unknown
            && status.Ipv6.Mode is DnsMode.Dhcp or DnsMode.Unknown;
    }

    private static bool MatchesStatic(DnsProfile profile, DnsStatus status)
    {
        return FamilyMatches(profile.Ipv4, status.Ipv4)
            && FamilyMatches(profile.Ipv6, status.Ipv6);
    }

    private static bool FamilyMatches(IReadOnlyList<string> expectedServers, DnsServerState actualState)
    {
        var normalizedExpected = Normalize(expectedServers);
        var normalizedActual = Normalize(actualState.NameServers);

        if (normalizedExpected.Count == 0)
        {
            return actualState.Mode != DnsMode.Manual;
        }

        if (actualState.Mode != DnsMode.Manual)
        {
            return false;
        }

        return normalizedExpected.SequenceEqual(normalizedActual, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => IPAddress.Parse(value).ToString())
            .ToArray();
    }
}
