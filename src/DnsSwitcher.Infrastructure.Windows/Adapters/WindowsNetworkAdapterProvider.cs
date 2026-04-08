using System.Net;
using System.Net.NetworkInformation;
using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;
using Microsoft.Extensions.Logging;

namespace DnsSwitcher.Infrastructure.Windows.Adapters;

public sealed class WindowsNetworkAdapterProvider(ILogger<WindowsNetworkAdapterProvider> logger) : INetworkAdapterProvider
{
    private static readonly string[] VirtualKeywords =
    [
        "virtual",
        "hyper-v",
        "vmware",
        "vethernet",
        "wsl",
        "loopback",
        "pseudo",
        "tunnel",
        "vpn",
        "wireguard",
        "tailscale",
        "zerotier",
        "hamachi",
        "tap",
        "bluetooth",
    ];

    public Task<IReadOnlyList<NetworkAdapter>> GetAdaptersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Select(CreateAdapter)
            .Where(adapter => adapter.SupportedStacks != NetworkStackSupport.None)
            .ToList();

        logger.LogInformation("Discovered {AdapterCount} IP-capable network adapter(s).", adapters.Count);
        return Task.FromResult<IReadOnlyList<NetworkAdapter>>(adapters);
    }

    private static NetworkAdapter CreateAdapter(NetworkInterface networkInterface)
    {
        var properties = networkInterface.GetIPProperties();

        return new NetworkAdapter
        {
            Id = networkInterface.Id,
            Name = networkInterface.Name,
            Description = networkInterface.Description,
            IsActive = networkInterface.OperationalStatus == OperationalStatus.Up,
            IsPhysical = IsPhysical(networkInterface),
            IsLoopback = networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback,
            HasDefaultGateway = HasDefaultGateway(properties),
            SupportedStacks = GetSupportedStacks(networkInterface),
            InterfaceIndex = GetInterfaceIndex(properties),
        };
    }

    private static NetworkStackSupport GetSupportedStacks(NetworkInterface networkInterface)
    {
        var supportedStacks = NetworkStackSupport.None;

        if (networkInterface.Supports(NetworkInterfaceComponent.IPv4))
        {
            supportedStacks |= NetworkStackSupport.Ipv4;
        }

        if (networkInterface.Supports(NetworkInterfaceComponent.IPv6))
        {
            supportedStacks |= NetworkStackSupport.Ipv6;
        }

        return supportedStacks;
    }

    private static bool HasDefaultGateway(IPInterfaceProperties properties)
    {
        return properties.GatewayAddresses.Any(gateway =>
            gateway.Address is not null
            && gateway.Address != IPAddress.Any
            && gateway.Address != IPAddress.IPv6Any
            && gateway.Address != IPAddress.None);
    }

    private static int? GetInterfaceIndex(IPInterfaceProperties properties)
    {
        try
        {
            return properties.GetIPv4Properties()?.Index ?? properties.GetIPv6Properties()?.Index;
        }
        catch (NetworkInformationException)
        {
            return null;
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }

    private static bool IsPhysical(NetworkInterface networkInterface)
    {
        if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
        {
            return false;
        }

        var identity = $"{networkInterface.Name} {networkInterface.Description}".ToLowerInvariant();

        if (VirtualKeywords.Any(keyword => identity.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }
}
