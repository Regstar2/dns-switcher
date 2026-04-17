namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed record UiSettings
{
    public bool MinimizeToTray { get; init; }

    public string? LastAdapterId { get; init; }

    public string? LastSelectedProfileId { get; init; }

    public bool AgentManagerShownOnFirstLaunch { get; init; }

    public static UiSettings Default { get; } = new();
}
