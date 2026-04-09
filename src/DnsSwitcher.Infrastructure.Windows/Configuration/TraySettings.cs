namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed record TraySettings
{
    public bool NotificationsEnabled { get; init; } = true;

    public bool ShowAdapterName { get; init; } = true;

    public static TraySettings Default { get; } = new();
}
