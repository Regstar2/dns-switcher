namespace DnsSwitcher.Core.Models;

public sealed record DnsStatus(
    bool IsManaged,
    string? MatchedProfileId,
    string? AdapterName,
    DnsMode Mode,
    DnsServerState Ipv4,
    DnsServerState Ipv6,
    string Details);
