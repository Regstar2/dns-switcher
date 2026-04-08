using System.Globalization;
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

        AddFamilyCommands(
            commands,
            interfaceTarget,
            adapterDisplayName,
            "ipv4",
            "IPv4",
            supportedStacks.HasFlag(NetworkStackSupport.Ipv4),
            profile,
            profile.Ipv4);

        AddFamilyCommands(
            commands,
            interfaceTarget,
            adapterDisplayName,
            "ipv6",
            "IPv6",
            supportedStacks.HasFlag(NetworkStackSupport.Ipv6),
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
        string adapterDisplayName,
        string familyToken,
        string familyDisplayName,
        bool isSupported,
        DnsProfile profile,
        IReadOnlyList<string> servers)
    {
        if (!isSupported)
        {
            if (servers.Count > 0)
            {
                throw new DnsOperationFailedException(
                    $"Network adapter '{adapterDisplayName}' does not support {familyDisplayName}, but profile '{profile.Id}' requires it.");
            }

            return;
        }

        if (profile.Mode == ProfileMode.Dhcp || servers.Count == 0)
        {
            commands.Add(BuildResetFamilyCommand(interfaceTarget, familyToken));
            return;
        }

        commands.Add(BuildSetPrimaryFamilyCommand(interfaceTarget, familyToken, servers[0]));

        for (var index = 1; index < servers.Count; index++)
        {
            commands.Add(BuildAddFamilyCommand(interfaceTarget, familyToken, servers[index], index + 1));
        }
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
