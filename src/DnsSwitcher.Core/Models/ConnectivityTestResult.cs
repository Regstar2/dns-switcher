namespace DnsSwitcher.Core.Models;

public sealed record ConnectivityTestResult(
    string? AdapterName,
    string? ProfileId,
    string? ProfileName,
    IReadOnlyList<string> Urls,
    IReadOnlyList<UrlConnectivityTestResult> UrlResults,
    ConnectivityTestStatus Status,
    TimeSpan? AverageLatency,
    string Details);
