using Microsoft.Win32;

namespace DnsSwitcher.Infrastructure.Windows.Presentation;

public static class WindowsThemeDetector
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

    public static bool IsDarkModeEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath, writable: false);
        var value = key?.GetValue(AppsUseLightThemeValueName);

        return value switch
        {
            int intValue => intValue == 0,
            _ => false,
        };
    }
}
