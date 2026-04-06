namespace DnsSwitcher.Core.Models;

public sealed record DnsStatus(
    bool IsManaged,
    string? ActiveProfileId,
    string? AdapterName,
    IReadOnlyList<string> NameServers,
    string Details);
