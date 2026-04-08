using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Dns;

namespace DnsSwitcher.Tests;

public sealed class WindowsDnsCommandBuilderTests
{
    [Fact]
    public void BuildApplyScript_ResetsEmptyFamilyToDhcp()
    {
        var profile = new DnsProfile
        {
            Id = "google-v4",
            Name = "Google IPv4",
            Mode = ProfileMode.Static,
            Ipv4 = ["8.8.8.8", "8.8.4.4"],
            Ipv6 = [],
        };

        var script = WindowsDnsCommandBuilder.BuildApplyScript("Wi-Fi", NetworkStackSupport.Ipv4 | NetworkStackSupport.Ipv6, profile);

        Assert.Contains("Set-DnsClientServerAddress -InterfaceAlias 'Wi-Fi' -ServerAddresses @('8.8.8.8', '8.8.4.4') -AddressFamily IPv4", script);
        Assert.Contains("Set-DnsClientServerAddress -InterfaceAlias 'Wi-Fi' -ResetServerAddresses -AddressFamily IPv6", script);
    }

    [Fact]
    public void BuildApplyScript_Throws_WhenProfileRequiresUnsupportedStack()
    {
        var profile = new DnsProfile
        {
            Id = "ipv6-only",
            Name = "IPv6 only",
            Mode = ProfileMode.Static,
            Ipv6 = ["2606:4700:4700::1111"],
        };

        var exception = Assert.Throws<DnsOperationFailedException>(() =>
            WindowsDnsCommandBuilder.BuildApplyScript("Ethernet", NetworkStackSupport.Ipv4, profile));

        Assert.Contains("does not support IPv6", exception.Message);
    }

    [Fact]
    public void BuildResetScript_UsesOnlySupportedFamilies()
    {
        var script = WindowsDnsCommandBuilder.BuildResetScript("Ethernet", NetworkStackSupport.Ipv4);

        Assert.Contains("AddressFamily IPv4", script);
        Assert.DoesNotContain("AddressFamily IPv6", script);
    }
}
