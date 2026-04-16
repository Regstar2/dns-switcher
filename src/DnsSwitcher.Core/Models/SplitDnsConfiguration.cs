namespace DnsSwitcher.Core.Models;

public sealed record SplitDnsConfiguration
{
    public bool Enabled { get; init; }

    public SplitDnsMode Mode { get; init; } = SplitDnsMode.WindowsNrpt;

    public SplitDnsDefaultBehavior DefaultBehavior { get; init; } = SplitDnsDefaultBehavior.SystemDns;

    public List<SplitDnsRule> Rules { get; init; } = [];

    public static SplitDnsConfiguration Default => new();
}
