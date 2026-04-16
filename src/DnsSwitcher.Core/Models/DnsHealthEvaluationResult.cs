namespace DnsSwitcher.Core.Models;

public sealed record DnsHealthEvaluationResult(
    DnsHealthStatus Status,
    bool SwitchedProfile,
    string? ActiveProfileId,
    string? TargetProfileId,
    string Details,
    DnsHealthState State,
    DnsTestResult? TestResult);
