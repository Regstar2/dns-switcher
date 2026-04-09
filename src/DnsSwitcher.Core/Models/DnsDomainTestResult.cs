namespace DnsSwitcher.Core.Models;

public sealed record DnsDomainTestResult(
    string Domain,
    DnsTestStatus Status,
    int SuccessfulAttempts,
    int TotalAttempts,
    TimeSpan? AverageLatency,
    TimeSpan? BestLatency,
    string Details);
