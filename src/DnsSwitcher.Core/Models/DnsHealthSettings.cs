namespace DnsSwitcher.Core.Models;

public sealed record DnsHealthSettings
{
    public bool Enabled { get; init; }

    public int MonitorIntervalSeconds { get; init; } = 60;

    public int FailureThreshold { get; init; } = 3;

    public int RecoveryThreshold { get; init; } = 2;

    public int CooldownSeconds { get; init; } = 300;

    public DnsHealthCheckMode CheckMode { get; init; } = DnsHealthCheckMode.ResolveOnly;

    public DnsHealthFailureAction ActionOnFailure { get; init; } = DnsHealthFailureAction.NotifyOnly;

    public string? FallbackProfileId { get; init; }

    public List<string> FailoverChain { get; init; } = [];

    public List<string> TestDomains { get; init; } =
    [
        "cloudflare.com",
        "github.com",
        "openai.com",
    ];

    public Dictionary<string, List<string>> ExpectedAddresses { get; init; } = [];

    public static DnsHealthSettings Default => new();
}
