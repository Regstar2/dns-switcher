using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;

namespace DnsSwitcher.Tests;

public sealed class DnsStatusMatcherTests
{
    [Fact]
    public void MatchProfile_ReturnsDhcpProfile_ForDhcpStatus()
    {
        var configuration = new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "dhcp",
                    Name = "DHCP",
                    Mode = ProfileMode.Dhcp,
                },
                new DnsProfile
                {
                    Id = "cloudflare",
                    Name = "Cloudflare",
                    Mode = ProfileMode.Static,
                    Ipv4 = ["1.1.1.1", "1.0.0.1"],
                },
            ],
        };

        var status = CreateStatus(
            mode: DnsMode.Dhcp,
            ipv4Mode: DnsMode.Dhcp,
            ipv4Servers: ["192.168.1.1", "192.168.1.2"],
            ipv6Mode: DnsMode.Dhcp,
            ipv6Servers: ["fe80::1"]);

        var matchedProfile = DnsStatusMatcher.MatchProfile(configuration, status);

        Assert.NotNull(matchedProfile);
        Assert.Equal("dhcp", matchedProfile!.Id);
    }

    [Fact]
    public void MatchProfile_ReturnsDhcpProfile_WhenOneFamilyIsUnsupported()
    {
        var configuration = new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "dhcp",
                    Name = "DHCP",
                    Mode = ProfileMode.Dhcp,
                },
            ],
        };

        var status = CreateStatus(
            mode: DnsMode.Dhcp,
            ipv4Mode: DnsMode.Dhcp,
            ipv4Servers: ["192.168.1.1"],
            ipv6Mode: DnsMode.Unknown,
            ipv6Servers: []);

        var matchedProfile = DnsStatusMatcher.MatchProfile(configuration, status);

        Assert.NotNull(matchedProfile);
        Assert.Equal("dhcp", matchedProfile!.Id);
    }

    [Fact]
    public void MatchProfile_ReturnsStaticProfile_ForExactDnsMatch()
    {
        var configuration = new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "cloudflare",
                    Name = "Cloudflare",
                    Mode = ProfileMode.Static,
                    Ipv4 = ["1.1.1.1", "1.0.0.1"],
                    Ipv6 = ["2606:4700:4700::1111", "2606:4700:4700::1001"],
                },
            ],
        };

        var status = CreateStatus(
            mode: DnsMode.Manual,
            ipv4Mode: DnsMode.Manual,
            ipv4Servers: ["1.1.1.1", "1.0.0.1"],
            ipv6Mode: DnsMode.Manual,
            ipv6Servers: ["2606:4700:4700::1111", "2606:4700:4700::1001"]);

        var matchedProfile = DnsStatusMatcher.MatchProfile(configuration, status);

        Assert.NotNull(matchedProfile);
        Assert.Equal("cloudflare", matchedProfile!.Id);
    }

    [Fact]
    public void MatchProfile_ReturnsStaticProfile_WhenUnusedFamilyIsEmpty()
    {
        var configuration = new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "quad9-v4",
                    Name = "Quad9 IPv4",
                    Mode = ProfileMode.Static,
                    Ipv4 = ["9.9.9.9", "149.112.112.112"],
                    Ipv6 = [],
                },
            ],
        };

        var status = CreateStatus(
            mode: DnsMode.Mixed,
            ipv4Mode: DnsMode.Manual,
            ipv4Servers: ["9.9.9.9", "149.112.112.112"],
            ipv6Mode: DnsMode.Dhcp,
            ipv6Servers: []);

        var matchedProfile = DnsStatusMatcher.MatchProfile(configuration, status);

        Assert.NotNull(matchedProfile);
        Assert.Equal("quad9-v4", matchedProfile!.Id);
    }

    [Fact]
    public void MatchProfile_ReturnsStaticProfile_WhenUnusedFamilyUsesDhcpServers()
    {
        var configuration = new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "quad9-v4",
                    Name = "Quad9 IPv4",
                    Mode = ProfileMode.Static,
                    Ipv4 = ["9.9.9.9", "149.112.112.112"],
                    Ipv6 = [],
                },
            ],
        };

        var status = CreateStatus(
            mode: DnsMode.Mixed,
            ipv4Mode: DnsMode.Manual,
            ipv4Servers: ["9.9.9.9", "149.112.112.112"],
            ipv6Mode: DnsMode.Dhcp,
            ipv6Servers: ["fe80::1"]);

        var matchedProfile = DnsStatusMatcher.MatchProfile(configuration, status);

        Assert.NotNull(matchedProfile);
        Assert.Equal("quad9-v4", matchedProfile!.Id);
    }

    [Fact]
    public void MatchProfile_ReturnsNull_WhenServerOrderDiffers()
    {
        var configuration = new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "google",
                    Name = "Google",
                    Mode = ProfileMode.Static,
                    Ipv4 = ["8.8.8.8", "8.8.4.4"],
                },
            ],
        };

        var status = CreateStatus(
            mode: DnsMode.Manual,
            ipv4Mode: DnsMode.Manual,
            ipv4Servers: ["8.8.4.4", "8.8.8.8"],
            ipv6Mode: DnsMode.Dhcp,
            ipv6Servers: []);

        var matchedProfile = DnsStatusMatcher.MatchProfile(configuration, status);

        Assert.Null(matchedProfile);
    }

    [Fact]
    public void MatchProfile_ReturnsNull_WhenFamilyUsesDhcpInsteadOfManual()
    {
        var configuration = new AppConfig
        {
            Profiles =
            [
                new DnsProfile
                {
                    Id = "google",
                    Name = "Google",
                    Mode = ProfileMode.Static,
                    Ipv4 = ["8.8.8.8", "8.8.4.4"],
                },
            ],
        };

        var status = CreateStatus(
            mode: DnsMode.Dhcp,
            ipv4Mode: DnsMode.Dhcp,
            ipv4Servers: ["8.8.8.8", "8.8.4.4"],
            ipv6Mode: DnsMode.Dhcp,
            ipv6Servers: []);

        var matchedProfile = DnsStatusMatcher.MatchProfile(configuration, status);

        Assert.Null(matchedProfile);
    }

    private static DnsStatus CreateStatus(
        DnsMode mode,
        DnsMode ipv4Mode,
        IReadOnlyList<string> ipv4Servers,
        DnsMode ipv6Mode,
        IReadOnlyList<string> ipv6Servers)
    {
        return new DnsStatus(
            IsManaged: false,
            MatchedProfileId: null,
            AdapterName: "Wi-Fi",
            Mode: mode,
            Ipv4: new DnsServerState(ipv4Mode, ipv4Servers),
            Ipv6: new DnsServerState(ipv6Mode, ipv6Servers),
            Details: string.Empty);
    }
}
