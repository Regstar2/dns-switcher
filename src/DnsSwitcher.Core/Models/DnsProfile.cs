namespace DnsSwitcher.Core.Models;

public sealed record DnsProfile
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public ProfileMode Mode { get; init; } = ProfileMode.Static;

    public List<string> Ipv4 { get; init; } = [];

    public List<string> Ipv6 { get; init; } = [];

    public List<string> Tags { get; init; } = [];

    public List<string> TestDomains { get; init; } = [];

    public List<string> TestUrls { get; init; } = [];
}
