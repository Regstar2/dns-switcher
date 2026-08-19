namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed record UpdateState
{
    public DateTimeOffset? LastCheckedUtc { get; init; }

    public string? LastNotifiedVersion { get; init; }

    public static UpdateState Default { get; } = new();
}
