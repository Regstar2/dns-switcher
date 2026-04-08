namespace DnsSwitcher.Core.Models;

public sealed record DnsServerState(
    DnsMode Mode,
    IReadOnlyList<string> NameServers);
