namespace DnsSwitcher.Core.Models;

public sealed record SiteStageResult(
    bool Success,
    TimeSpan? Latency,
    string Details);
