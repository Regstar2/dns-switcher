namespace DnsSwitcher.Core.Models;

public sealed record DnsBenchmarkProfileResult(
    string ProfileId,
    string ProfileName,
    DnsTestResult TestResult,
    bool IsBest);
