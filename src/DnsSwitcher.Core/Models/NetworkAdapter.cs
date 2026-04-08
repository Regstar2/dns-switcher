namespace DnsSwitcher.Core.Models;

public sealed record NetworkAdapter
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public bool IsActive { get; init; }

    public bool IsPhysical { get; init; }

    public bool IsLoopback { get; init; }

    public bool HasDefaultGateway { get; init; }

    public NetworkStackSupport SupportedStacks { get; init; }

    public int? InterfaceIndex { get; init; }
}
