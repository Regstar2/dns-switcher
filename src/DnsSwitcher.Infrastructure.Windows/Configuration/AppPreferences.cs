namespace DnsSwitcher.Infrastructure.Windows.Configuration;

public sealed record AppPreferences
{
    public AppLanguage Language { get; init; } = AppLanguage.System;

    public AppTheme Theme { get; init; } = AppTheme.System;

    public static AppPreferences Default { get; } = new();
}
