namespace DnsSwitcher.Core.Models;

public sealed record SplitDnsRule
{
    public string Id { get; init; } = string.Empty;

    public string Namespace { get; init; } = string.Empty;

    public string ProfileId { get; init; } = string.Empty;

    public bool Enabled { get; init; } = true;

    public int Priority { get; init; }

    public string? Comment { get; init; }
}
