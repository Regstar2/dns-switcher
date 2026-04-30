using System.Globalization;
using System.Net;
using System.Net.Sockets;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Infrastructure.Windows.Dns;

internal static class WindowsDnsCommandBuilder
{
    public static IReadOnlyList<WindowsProcessCommand> BuildApplyCommands(
        string interfaceTarget,
        string adapterDisplayName,
        NetworkStackSupport supportedStacks,
        DnsProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterDisplayName);
        ArgumentNullException.ThrowIfNull(profile);

        var commands = new List<WindowsProcessCommand>();
        var supportsIpv4 = supportedStacks.HasFlag(NetworkStackSupport.Ipv4);
        var supportsIpv6 = supportedStacks.HasFlag(NetworkStackSupport.Ipv6);
        var hasProfileServers = profile.Ipv4.Count > 0 || profile.Ipv6.Count > 0;
        var hasApplicableProfileServers =
            profile.Ipv4.Count > 0 && supportsIpv4
            || profile.Ipv6.Count > 0 && supportsIpv6;

        if (profile.Mode == ProfileMode.Static && hasProfileServers && !hasApplicableProfileServers)
        {
            throw new DnsOperationFailedException(
                $"Network adapter '{adapterDisplayName}' does not have any enabled IP stack required by profile '{profile.Id}'.");
        }

        AddFamilyCommands(
            commands,
            interfaceTarget,
            "ipv4",
            AddressFamily.InterNetwork,
            supportsIpv4,
            profile,
            profile.Ipv4);

        AddFamilyCommands(
            commands,
            interfaceTarget,
            "ipv6",
            AddressFamily.InterNetworkV6,
            supportsIpv6,
            profile,
            profile.Ipv6);

        return commands;
    }

    public static IReadOnlyList<WindowsProcessCommand> BuildResetCommands(string interfaceTarget, NetworkStackSupport supportedStacks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceTarget);

        var commands = new List<WindowsProcessCommand>();

        if (supportedStacks.HasFlag(NetworkStackSupport.Ipv4))
        {
            commands.Add(BuildResetFamilyCommand(interfaceTarget, "ipv4"));
        }

        if (supportedStacks.HasFlag(NetworkStackSupport.Ipv6))
        {
            commands.Add(BuildResetFamilyCommand(interfaceTarget, "ipv6"));
        }

        return commands;
    }

    private static void AddFamilyCommands(
        ICollection<WindowsProcessCommand> commands,
        string interfaceTarget,
        string familyToken,
        AddressFamily expectedAddressFamily,
        bool isSupported,
        DnsProfile profile,
        IReadOnlyList<string> servers)
    {
        if (!isSupported)
        {
            return;
        }

        if (profile.Mode == ProfileMode.Dhcp || servers.Count == 0)
        {
            commands.Add(BuildResetFamilyCommand(interfaceTarget, familyToken));
            return;
        }

        commands.Add(BuildSetPrimaryFamilyCommand(interfaceTarget, familyToken, ValidateServerAddress(servers[0], expectedAddressFamily, profile)));

        for (var index = 1; index < servers.Count; index++)
        {
            commands.Add(BuildAddFamilyCommand(
                interfaceTarget,
                familyToken,
                ValidateServerAddress(servers[index], expectedAddressFamily, profile),
                index + 1));
        }
    }

    private static string ValidateServerAddress(string address, AddressFamily expectedAddressFamily, DnsProfile profile)
    {
        if (IPAddress.TryParse(address, out var parsed) && parsed.AddressFamily == expectedAddressFamily)
        {
            return address;
        }

        var familyName = expectedAddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6";
        throw new DnsOperationFailedException(
            $"DNS profile '{profile.Id}' contains invalid {familyName} DNS address '{address}'.");
    }

    private static WindowsProcessCommand BuildResetFamilyCommand(string interfaceTarget, string familyToken)
    {
        return new WindowsProcessCommand(
            FileName: "netsh.exe",
            Arguments: $"interface {familyToken} set dnsservers name={Quote(interfaceTarget)} source=dhcp");
    }

    private static WindowsProcessCommand BuildSetPrimaryFamilyCommand(
        string interfaceTarget,
        string familyToken,
        string server)
    {
        return new WindowsProcessCommand(
            FileName: "netsh.exe",
            Arguments: $"interface {familyToken} set dnsservers name={Quote(interfaceTarget)} source=static address={server} validate=no");
    }

    private static WindowsProcessCommand BuildAddFamilyCommand(
        string interfaceTarget,
        string familyToken,
        string server,
        int index)
    {
        return new WindowsProcessCommand(
            FileName: "netsh.exe",
            Arguments: $"interface {familyToken} add dnsservers name={Quote(interfaceTarget)} address={server} index={index.ToString(CultureInfo.InvariantCulture)} validate=no");
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}

internal sealed record WindowsProcessCommand(string FileName, string Arguments);
