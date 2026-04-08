using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Infrastructure.Windows.Dns;

namespace DnsSwitcher.Tests;

public sealed class WindowsDnsCommandBuilderTests
{
    [Fact]
    public void BuildApplyCommands_ResetsEmptyFamilyToDhcp()
    {
        var profile = new DnsProfile
        {
            Id = "google-v4",
            Name = "Google IPv4",
            Mode = ProfileMode.Static,
            Ipv4 = ["8.8.8.8", "8.8.4.4"],
            Ipv6 = [],
        };

        var commands = WindowsDnsCommandBuilder.BuildApplyCommands("12", "Wi-Fi", NetworkStackSupport.Ipv4 | NetworkStackSupport.Ipv6, profile);

        Assert.Collection(
            commands,
            command => Assert.Equal("interface ipv4 set dnsservers name=\"12\" source=static address=8.8.8.8 validate=no", command.Arguments),
            command => Assert.Equal("interface ipv4 add dnsservers name=\"12\" address=8.8.4.4 index=2 validate=no", command.Arguments),
            command => Assert.Equal("interface ipv6 set dnsservers name=\"12\" source=dhcp", command.Arguments));
    }

    [Fact]
    public void BuildApplyCommands_Throws_WhenProfileRequiresUnsupportedStack()
    {
        var profile = new DnsProfile
        {
            Id = "ipv6-only",
            Name = "IPv6 only",
            Mode = ProfileMode.Static,
            Ipv6 = ["2606:4700:4700::1111"],
        };

        var exception = Assert.Throws<DnsOperationFailedException>(() =>
            WindowsDnsCommandBuilder.BuildApplyCommands("7", "Ethernet", NetworkStackSupport.Ipv4, profile));

        Assert.Contains("does not support IPv6", exception.Message);
    }

    [Fact]
    public void BuildResetCommands_UsesOnlySupportedFamilies()
    {
        var commands = WindowsDnsCommandBuilder.BuildResetCommands("Ethernet", NetworkStackSupport.Ipv4);

        Assert.Single(commands);
        Assert.Equal("interface ipv4 set dnsservers name=\"Ethernet\" source=dhcp", commands[0].Arguments);
    }
}
