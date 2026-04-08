using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Exceptions;
using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;

namespace DnsSwitcher.Tests;

public sealed class NetworkAdapterServiceTests
{
    [Fact]
    public async Task GetSelectedAdapterAsync_ReturnsDefaultAdapter_WhenSelectionIsEmpty()
    {
        var service = CreateService(
            CreateAdapter("wifi", "Wi-Fi", isActive: true, isPhysical: true, hasDefaultGateway: true),
            CreateAdapter("vpn", "VPN", isActive: true, isPhysical: false, hasDefaultGateway: true));

        var adapter = await service.GetSelectedAdapterAsync(null);

        Assert.NotNull(adapter);
        Assert.Equal("wifi", adapter!.Id);
    }

    [Fact]
    public async Task GetSelectedAdapterAsync_ReturnsAdapter_ByIdFirst()
    {
        var service = CreateService(CreateAdapter("wifi-1", "Wi-Fi"));

        var adapter = await service.GetSelectedAdapterAsync("wifi-1");

        Assert.NotNull(adapter);
        Assert.Equal("wifi-1", adapter!.Id);
    }

    [Fact]
    public async Task GetSelectedAdapterAsync_ReturnsAdapter_ByName()
    {
        var service = CreateService(CreateAdapter("wifi-1", "Wi-Fi"));

        var adapter = await service.GetSelectedAdapterAsync("Wi-Fi");

        Assert.NotNull(adapter);
        Assert.Equal("wifi-1", adapter!.Id);
    }

    [Fact]
    public async Task GetSelectedAdapterAsync_Throws_WhenNameIsAmbiguous()
    {
        var service = CreateService(
            CreateAdapter("wifi-1", "Wi-Fi"),
            CreateAdapter("wifi-2", "Wi-Fi"));

        var exception = await Assert.ThrowsAsync<NetworkAdapterNotFoundException>(() => service.GetSelectedAdapterAsync("Wi-Fi"));

        Assert.Contains("Use adapter id instead", exception.Message);
    }

    private static NetworkAdapterService CreateService(params NetworkAdapter[] adapters)
    {
        return new NetworkAdapterService(new FakeNetworkAdapterProvider(adapters));
    }

    private static NetworkAdapter CreateAdapter(
        string id,
        string name,
        bool isActive = true,
        bool isPhysical = true,
        bool hasDefaultGateway = true)
    {
        return new NetworkAdapter
        {
            Id = id,
            Name = name,
            Description = name,
            IsActive = isActive,
            IsPhysical = isPhysical,
            IsLoopback = false,
            HasDefaultGateway = hasDefaultGateway,
            SupportedStacks = NetworkStackSupport.Ipv4 | NetworkStackSupport.Ipv6,
            InterfaceIndex = 1,
        };
    }

    private sealed class FakeNetworkAdapterProvider(params NetworkAdapter[] adapters) : INetworkAdapterProvider
    {
        public Task<IReadOnlyList<NetworkAdapter>> GetAdaptersAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<NetworkAdapter>>(adapters);
        }
    }
}
