using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using System.Globalization;
using System.Security.Principal;
using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DnsSwitcher.Infrastructure.Windows.Dns;

[SupportedOSPlatform("windows")]
public sealed class WindowsDnsManager(
    NetworkAdapterService networkAdapterService,
    DnsProfileService profileService,
    ILogger<WindowsDnsManager> logger) : IDnsManager
{
    public async Task<DnsStatus> GetStatusAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
    {
        var selectedAdapter = await networkAdapterService.GetSelectedAdapterAsync(adapterIdOrName, cancellationToken).ConfigureAwait(false);

        if (selectedAdapter is null)
        {
            if (!string.IsNullOrWhiteSpace(adapterIdOrName))
            {
                throw new NetworkAdapterNotFoundException($"Network adapter '{adapterIdOrName}' was not found.");
            }

            return new DnsStatus(
                IsManaged: false,
                MatchedProfileId: null,
                AdapterName: null,
                Mode: DnsMode.Unknown,
                Ipv4: CreateUnknownFamilyState(),
                Ipv6: CreateUnknownFamilyState(),
                Details: "No suitable network adapter was selected.");
        }

        var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(candidate => string.Equals(candidate.Id, selectedAdapter.Id, StringComparison.OrdinalIgnoreCase));

        if (networkInterface is null)
        {
            logger.LogWarning("Selected adapter {AdapterId} was not found in current network interfaces.", selectedAdapter.Id);

            return new DnsStatus(
                IsManaged: false,
                MatchedProfileId: null,
                AdapterName: selectedAdapter.Name,
                Mode: DnsMode.Unknown,
                Ipv4: CreateUnknownFamilyState(),
                Ipv6: CreateUnknownFamilyState(),
                Details: $"Selected adapter '{selectedAdapter.Name}' is no longer available.");
        }

        var ipv4 = CreateFamilyState(networkInterface, selectedAdapter.Id, NetworkInterfaceComponent.IPv4);
        var ipv6 = CreateFamilyState(networkInterface, selectedAdapter.Id, NetworkInterfaceComponent.IPv6);
        var mode = GetOverallMode(ipv4, ipv6);
        var configuration = await profileService.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var matchedProfile = DnsStatusMatcher.MatchProfile(configuration, new DnsStatus(
            IsManaged: false,
            MatchedProfileId: null,
            AdapterName: selectedAdapter.Name,
            Mode: mode,
            Ipv4: ipv4,
            Ipv6: ipv6,
            Details: string.Empty));

        var status = new DnsStatus(
            IsManaged: matchedProfile is not null,
            MatchedProfileId: matchedProfile?.Id,
            AdapterName: selectedAdapter.Name,
            Mode: mode,
            Ipv4: ipv4,
            Ipv6: ipv6,
            Details: BuildDetails(selectedAdapter.Name, mode, matchedProfile?.Name));

        return status;
    }

    public Task ApplyProfileAsync(DnsProfile profile, string? adapterIdOrName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return ApplyProfileCoreAsync(profile, adapterIdOrName, cancellationToken);
    }

    public Task ResetToDhcpAsync(string? adapterIdOrName = null, CancellationToken cancellationToken = default)
    {
        return ResetToDhcpCoreAsync(adapterIdOrName, cancellationToken);
    }

    private static string BuildDetails(string adapterName, DnsMode mode, string? matchedProfileName)
    {
        var details = $"Selected adapter '{adapterName}'. DNS mode: {mode}.";

        if (!string.IsNullOrWhiteSpace(matchedProfileName))
        {
            details += $" Matched profile: '{matchedProfileName}'.";
        }

        return details;
    }

    private static DnsServerState CreateFamilyState(
        NetworkInterface networkInterface,
        string adapterId,
        NetworkInterfaceComponent networkInterfaceComponent)
    {
        if (!networkInterface.Supports(networkInterfaceComponent))
        {
            return CreateUnknownFamilyState();
        }

        var mode = HasManualOverride(adapterId, networkInterfaceComponent) ? DnsMode.Manual : DnsMode.Dhcp;
        var servers = networkInterface.GetIPProperties().DnsAddresses
            .Where(address =>
                networkInterfaceComponent == NetworkInterfaceComponent.IPv4
                    ? address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                    : address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            .Select(address => address.ToString())
            .ToArray();

        return new DnsServerState(mode, servers);
    }

    private static DnsServerState CreateUnknownFamilyState()
    {
        return new DnsServerState(DnsMode.Unknown, []);
    }

    private static bool HasManualOverride(string adapterId, NetworkInterfaceComponent networkInterfaceComponent)
    {
        var keyPath = networkInterfaceComponent == NetworkInterfaceComponent.IPv4
            ? $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{adapterId}"
            : $@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces\{adapterId}";

        using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: false);

        if (key is null)
        {
            return false;
        }

        var nameServer = key.GetValue("NameServer");
        var normalizedValue = NormalizeRegistryValue(nameServer);

        return !string.IsNullOrWhiteSpace(normalizedValue);
    }

    private static string NormalizeRegistryValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text.Replace("\0", string.Empty).Trim(),
            string[] values => string.Join(",", values).Replace("\0", string.Empty).Trim(),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.Replace("\0", string.Empty).Trim()
                ?? string.Empty,
        };
    }

    private static DnsMode GetOverallMode(DnsServerState ipv4, DnsServerState ipv6)
    {
        if (ipv4.Mode == DnsMode.Unknown && ipv6.Mode == DnsMode.Unknown)
        {
            return DnsMode.Unknown;
        }

        if (ipv4.Mode == DnsMode.Unknown)
        {
            return ipv6.Mode;
        }

        if (ipv6.Mode == DnsMode.Unknown)
        {
            return ipv4.Mode;
        }

        return ipv4.Mode == ipv6.Mode ? ipv4.Mode : DnsMode.Mixed;
    }

    private async Task ApplyProfileCoreAsync(DnsProfile profile, string? adapterIdOrName, CancellationToken cancellationToken)
    {
        var targetAdapter = await ResolveTargetAdapterAsync(adapterIdOrName, cancellationToken).ConfigureAwait(false);
        EnsureAdministrator();

        var interfaceTarget = GetInterfaceTarget(targetAdapter);
        var commands = WindowsDnsCommandBuilder.BuildApplyCommands(
            interfaceTarget,
            targetAdapter.Name,
            targetAdapter.SupportedStacks,
            profile);

        await ExecuteCommandsAsync(commands, $"apply DNS profile '{profile.Id}'", cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Applied DNS profile {ProfileId} to adapter {AdapterName}.", profile.Id, targetAdapter.Name);
    }

    private async Task ResetToDhcpCoreAsync(string? adapterIdOrName, CancellationToken cancellationToken)
    {
        var targetAdapter = await ResolveTargetAdapterAsync(adapterIdOrName, cancellationToken).ConfigureAwait(false);
        EnsureAdministrator();

        var interfaceTarget = GetInterfaceTarget(targetAdapter);
        var commands = WindowsDnsCommandBuilder.BuildResetCommands(interfaceTarget, targetAdapter.SupportedStacks);

        await ExecuteCommandsAsync(commands, $"reset DNS to DHCP on adapter '{targetAdapter.Name}'", cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Reset DNS to DHCP on adapter {AdapterName}.", targetAdapter.Name);
    }

    private async Task<NetworkAdapter> ResolveTargetAdapterAsync(string? adapterIdOrName, CancellationToken cancellationToken)
    {
        var selectedAdapter = await networkAdapterService.GetSelectedAdapterAsync(adapterIdOrName, cancellationToken).ConfigureAwait(false);

        if (selectedAdapter is null)
        {
            if (!string.IsNullOrWhiteSpace(adapterIdOrName))
            {
                throw new NetworkAdapterNotFoundException($"Network adapter '{adapterIdOrName}' was not found.");
            }

            throw new NetworkAdapterNotFoundException("No suitable network adapter was selected.");
        }

        var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(candidate => string.Equals(candidate.Id, selectedAdapter.Id, StringComparison.OrdinalIgnoreCase));

        if (networkInterface is null)
        {
            logger.LogWarning("Selected adapter {AdapterId} was not found in current network interfaces.", selectedAdapter.Id);
            throw new NetworkAdapterNotFoundException($"Network adapter '{selectedAdapter.Name}' was not found.");
        }

        if (networkInterface.OperationalStatus != OperationalStatus.Up || !selectedAdapter.IsActive)
        {
            throw new NetworkAdapterDisabledException(selectedAdapter.Name);
        }

        return selectedAdapter with { Name = networkInterface.Name };
    }

    private static void EnsureAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);

        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            throw new DnsOperationRequiresAdminException();
        }
    }

    private static string GetInterfaceTarget(NetworkAdapter adapter)
    {
        return adapter.InterfaceIndex?.ToString(CultureInfo.InvariantCulture) ?? adapter.Name;
    }

    private async Task ExecuteCommandsAsync(
        IReadOnlyList<WindowsProcessCommand> commands,
        string operationDescription,
        CancellationToken cancellationToken)
    {
        foreach (var command in commands)
        {
            await ExecuteCommandAsync(command, operationDescription, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteCommandAsync(
        WindowsProcessCommand command,
        string operationDescription,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command.FileName,
                Arguments = command.Arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };

        try
        {
            if (!process.Start())
            {
                throw new DnsOperationFailedException($"Failed to start '{command.FileName}' to {operationDescription}.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            throw new DnsOperationFailedException($"Failed to start '{command.FileName}' to {operationDescription}.", exception);
        }

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Win32Exception)
            {
            }
        });

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var standardOutput = (await standardOutputTask.ConfigureAwait(false)).Trim();
        var standardError = (await standardErrorTask.ConfigureAwait(false)).Trim();

        if (process.ExitCode != 0)
        {
            var details = !string.IsNullOrWhiteSpace(standardError)
                ? standardError
                : !string.IsNullOrWhiteSpace(standardOutput)
                    ? standardOutput
                    : $"{command.FileName} exited with code {process.ExitCode}.";

            throw new DnsOperationFailedException(
                $"Failed to {operationDescription}. Command: {command.FileName} {command.Arguments}. Details: {details}");
        }

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            logger.LogDebug(
                "Command output for operation '{OperationDescription}' ({CommandFileName} {CommandArguments}): {Output}",
                operationDescription,
                command.FileName,
                command.Arguments,
                standardOutput);
        }
    }
}
