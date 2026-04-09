namespace DnsSwitcher.Core.Models;

public sealed record UrlConnectivityTestResult(
    string Url,
    ConnectivityTestStatus Status,
    int SuccessfulAttempts,
    int TotalAttempts,
    SiteStageResult Dns,
    SiteStageResult Connect,
    SiteStageResult Tls,
    SiteStageResult Http,
    int? HttpStatusCode,
    string HttpMethod,
    TimeSpan? AverageLatency,
    TimeSpan? BestLatency,
    string Details);
