namespace DnsSwitcher.Core.Models;

public sealed record DnsHealthState
{
    public DnsHealthStatus Status { get; init; } = DnsHealthStatus.Disabled;

    public bool EnabledSnapshot { get; init; }

    public int ConsecutiveFailures { get; init; }

    public int ConsecutiveSuccesses { get; init; }

    public DateTimeOffset? LastCheckedUtc { get; init; }

    public DateTimeOffset? LastSuccessfulCheckUtc { get; init; }

    public DateTimeOffset? LastFailureUtc { get; init; }

    public DateTimeOffset? LastFailoverUtc { get; init; }

    public DateTimeOffset? CooldownUntilUtc { get; init; }

    public string? ActiveProfileId { get; init; }

    public string? LastFailoverProfileId { get; init; }

    public string? LastFailureReason { get; init; }

    public string? LastAction { get; init; }

    public static DnsHealthState Disabled(DateTimeOffset now)
    {
        return new DnsHealthState
        {
            Status = DnsHealthStatus.Disabled,
            EnabledSnapshot = false,
            LastCheckedUtc = now,
            LastAction = "Health monitoring is disabled.",
        };
    }
}
