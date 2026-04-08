using DnsSwitcher.Core.Models;
using DnsSwitcher.Core.Services;

namespace DnsSwitcher.Tests;

public sealed class NetworkAdapterSelectorTests
{
    [Fact]
    public void SelectDefault_PrefersActivePhysicalAdapterWithGateway()
    {
        var adapters = new[]
        {
            CreateAdapter("vpn", isActive: true, isPhysical: false, hasDefaultGateway: true),
            CreateAdapter("ethernet", isActive: true, isPhysical: true, hasDefaultGateway: true),
        };

        var selectedAdapter = NetworkAdapterSelector.SelectDefault(adapters);

        Assert.NotNull(selectedAdapter);
        Assert.Equal("ethernet", selectedAdapter!.Id);
    }

    [Fact]
    public void SelectDefault_FallsBackToActiveAdapterWithGateway_WhenPhysicalCandidateDoesNotExist()
    {
        var adapters = new[]
        {
            CreateAdapter("vpn", isActive: true, isPhysical: false, hasDefaultGateway: true),
            CreateAdapter("inactive", isActive: false, isPhysical: true, hasDefaultGateway: true),
        };

        var selectedAdapter = NetworkAdapterSelector.SelectDefault(adapters);

        Assert.NotNull(selectedAdapter);
        Assert.Equal("vpn", selectedAdapter!.Id);
    }

    [Fact]
    public void SelectDefault_IgnoresLoopbackAndUnsupportedAdapters()
    {
        var adapters = new[]
        {
            CreateAdapter("loopback", isActive: true, isLoopback: true),
            CreateAdapter("unsupported", isActive: true, supportedStacks: NetworkStackSupport.None),
            CreateAdapter("wifi", isActive: true, isPhysical: true),
        };

        var selectedAdapter = NetworkAdapterSelector.SelectDefault(adapters);

        Assert.NotNull(selectedAdapter);
        Assert.Equal("wifi", selectedAdapter!.Id);
    }

    [Fact]
    public void SelectDefault_PrefersAdapterWithMoreSupportedStacks_WhenPriorityIsEqual()
    {
        var adapters = new[]
        {
            CreateAdapter("ipv4-only", isActive: true, isPhysical: true, supportedStacks: NetworkStackSupport.Ipv4),
            CreateAdapter("dual-stack", isActive: true, isPhysical: true, supportedStacks: NetworkStackSupport.Ipv4 | NetworkStackSupport.Ipv6),
        };

        var selectedAdapter = NetworkAdapterSelector.SelectDefault(adapters);

        Assert.NotNull(selectedAdapter);
        Assert.Equal("dual-stack", selectedAdapter!.Id);
    }

    [Fact]
    public void SelectDefault_ReturnsNull_WhenNoSupportedAdaptersExist()
    {
        var adapters = new[]
        {
            CreateAdapter("loopback", isActive: true, isLoopback: true),
            CreateAdapter("unsupported", isActive: true, supportedStacks: NetworkStackSupport.None),
        };

        var selectedAdapter = NetworkAdapterSelector.SelectDefault(adapters);

        Assert.Null(selectedAdapter);
    }

    private static NetworkAdapter CreateAdapter(
        string id,
        bool isActive,
        bool isPhysical = true,
        bool isLoopback = false,
        bool hasDefaultGateway = false,
        NetworkStackSupport supportedStacks = NetworkStackSupport.Ipv4 | NetworkStackSupport.Ipv6,
        int? interfaceIndex = null)
    {
        return new NetworkAdapter
        {
            Id = id,
            Name = id,
            Description = id,
            IsActive = isActive,
            IsPhysical = isPhysical,
            IsLoopback = isLoopback,
            HasDefaultGateway = hasDefaultGateway,
            SupportedStacks = supportedStacks,
            InterfaceIndex = interfaceIndex,
        };
    }
}
