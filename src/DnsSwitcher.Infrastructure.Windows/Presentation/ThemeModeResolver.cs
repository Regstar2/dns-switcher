using DnsSwitcher.Infrastructure.Windows.Configuration;

namespace DnsSwitcher.Infrastructure.Windows.Presentation;

public static class ThemeModeResolver
{
    public static bool IsDarkTheme(AppTheme themePreference)
    {
        return themePreference switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => WindowsThemeDetector.IsDarkModeEnabled(),
        };
    }
}
