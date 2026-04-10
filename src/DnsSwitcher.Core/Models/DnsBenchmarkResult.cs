namespace DnsSwitcher.Core.Models;

public sealed record DnsBenchmarkResult(
    DateTimeOffset ExecutedAtUtc,
    string? AdapterName,
    int TotalProfiles,
    IReadOnlyList<DnsBenchmarkProfileResult> ProfileResults,
    string? BestProfileId,
    string? BestProfileName,
    DnsTestStatus OverallStatus,
    TimeSpan? BestLatency,
    bool RestoreSucceeded,
    string RestoreDetails,
    bool WasInterrupted,
    string? InterruptionReason,
    string Details);
