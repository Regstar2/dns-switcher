using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Infrastructure.Windows.Dns;

internal static class WindowsDnsCommandBuilder
{
    public static string BuildApplyScript(string interfaceAlias, NetworkStackSupport supportedStacks, DnsProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceAlias);
        ArgumentNullException.ThrowIfNull(profile);

        var commands = new List<string>
        {
            "$ErrorActionPreference = 'Stop'",
        };

        AddFamilyCommand(commands, interfaceAlias, "IPv4", supportedStacks.HasFlag(NetworkStackSupport.Ipv4), profile, profile.Ipv4);
        AddFamilyCommand(commands, interfaceAlias, "IPv6", supportedStacks.HasFlag(NetworkStackSupport.Ipv6), profile, profile.Ipv6);

        return string.Join(Environment.NewLine, commands);
    }

    public static string BuildResetScript(string interfaceAlias, NetworkStackSupport supportedStacks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceAlias);

        var commands = new List<string>
        {
            "$ErrorActionPreference = 'Stop'",
        };

        if (supportedStacks.HasFlag(NetworkStackSupport.Ipv4))
        {
            commands.Add(BuildResetFamilyCommand(interfaceAlias, "IPv4"));
        }

        if (supportedStacks.HasFlag(NetworkStackSupport.Ipv6))
        {
            commands.Add(BuildResetFamilyCommand(interfaceAlias, "IPv6"));
        }

        return string.Join(Environment.NewLine, commands);
    }

    private static void AddFamilyCommand(
        ICollection<string> commands,
        string interfaceAlias,
        string addressFamily,
        bool isSupported,
        DnsProfile profile,
        IReadOnlyList<string> servers)
    {
        if (!isSupported)
        {
            if (servers.Count > 0)
            {
                throw new DnsOperationFailedException(
                    $"Network adapter '{interfaceAlias}' does not support {addressFamily}, but profile '{profile.Id}' requires it.");
            }

            return;
        }

        if (profile.Mode == ProfileMode.Dhcp || servers.Count == 0)
        {
            commands.Add(BuildResetFamilyCommand(interfaceAlias, addressFamily));
            return;
        }

        commands.Add(BuildSetFamilyCommand(interfaceAlias, addressFamily, servers));
    }

    private static string BuildResetFamilyCommand(string interfaceAlias, string addressFamily)
    {
        return $"Set-DnsClientServerAddress -InterfaceAlias {Quote(interfaceAlias)} -ResetServerAddresses -AddressFamily {addressFamily}";
    }

    private static string BuildSetFamilyCommand(string interfaceAlias, string addressFamily, IReadOnlyList<string> servers)
    {
        var encodedServers = string.Join(", ", servers.Select(Quote));
        return $"Set-DnsClientServerAddress -InterfaceAlias {Quote(interfaceAlias)} -ServerAddresses @({encodedServers}) -AddressFamily {addressFamily}";
    }

    private static string Quote(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }
}
