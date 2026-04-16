using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Contracts;

public sealed record AgentRequest(
    int Version,
    AgentCommand Command,
    DnsProfile? Profile,
    string? AdapterSelection,
    SplitDnsConfiguration? SplitDnsConfiguration = null);
