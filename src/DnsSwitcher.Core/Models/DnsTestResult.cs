namespace DnsSwitcher.Core.Models;

public sealed record DnsTestResult(
    string? AdapterName,
    string? ProfileId,
    string? ProfileName,
    IReadOnlyList<string> DnsServers,
    IReadOnlyList<string> Domains,
    IReadOnlyList<DnsDomainTestResult> DomainResults,
    DnsTestStatus Status,
    TimeSpan? AverageLatency,
    string Details);
