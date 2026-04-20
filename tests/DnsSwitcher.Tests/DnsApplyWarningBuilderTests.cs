using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Dns;

namespace DnsSwitcher.Tests;

public sealed class DnsApplyWarningBuilderTests
{
    [Fact]
    public void Build_ReturnsIpv6SkippedWarning_WhenProfileHasIpv6_AndAdapterDoesNotSupportIpv6()
    {
        var profile = new DnsProfile
        {
            Id = "dual",
            Name = "Dual stack",
            Mode = ProfileMode.Static,
            Ipv4 = ["1.1.1.1"],
            Ipv6 = ["2606:4700:4700::1111"],
        };

        var adapter = new NetworkAdapter
        {
            Id = "adapter-id",
            Name = "Wi-Fi",
            SupportedStacks = NetworkStackSupport.Ipv4,
        };

        var warnings = DnsApplyWarningBuilder.Build(profile, adapter);

        var warning = Assert.Single(warnings);
        Assert.Equal(DnsApplyWarningKind.UnsupportedIpv6Skipped, warning.Kind);
        Assert.Equal("Wi-Fi", warning.AdapterName);
        Assert.Equal("dual", warning.ProfileId);
    }

    [Fact]
    public void Build_ReturnsNoWarnings_ForDhcpProfile()
    {
        var profile = new DnsProfile
        {
            Id = "dhcp",
            Name = "Automatic DNS",
            Mode = ProfileMode.Dhcp,
            Ipv4 = [],
            Ipv6 = ["2606:4700:4700::1111"],
        };

        var adapter = new NetworkAdapter
        {
            Id = "adapter-id",
            Name = "Wi-Fi",
            SupportedStacks = NetworkStackSupport.Ipv4,
        };

        var warnings = DnsApplyWarningBuilder.Build(profile, adapter);

        Assert.Empty(warnings);
    }
}
