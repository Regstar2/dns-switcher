namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed record TraySettings
{
    public bool NotificationsEnabled { get; init; } = true;

    public bool ShowAdapterName { get; init; } = true;

    public bool ShowDnsActions { get; init; } = true;

    public bool ShowDiagnostics { get; init; } = true;

    public bool ShowSplitDns { get; init; } = true;

    public bool ShowAgent { get; init; } = true;

    public bool ShowProfiles { get; init; } = true;

    public static TraySettings Default { get; } = new();
}
