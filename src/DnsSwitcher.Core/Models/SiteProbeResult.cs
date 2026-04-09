namespace DnsSwitcher.Core.Models;

public sealed record SiteProbeResult(
    string Url,
    bool Success,
    bool IsBlockedIndicator,
    SiteStageResult Dns,
    SiteStageResult Connect,
    SiteStageResult Tls,
    SiteStageResult Http,
    int? HttpStatusCode,
    string HttpMethod,
    TimeSpan? TotalLatency,
    string Details);
