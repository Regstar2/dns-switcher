using DnsSwitcher.Infrastructure.Windows.Configuration;
using DnsSwitcher.Infrastructure.Windows.Presentation;

namespace DnsSwitcher.Tray;

internal static class TrayDialogs
{
    public static void ShowInformation(string title, string message, AppTheme themePreference)
    {
        ResultDialog.ShowDialog(title, message, ThemeModeResolver.IsDarkTheme(themePreference));
    }

    public static void ShowError(string title, string message, AppTheme themePreference)
    {
        ResultDialog.ShowDialog(title, message, ThemeModeResolver.IsDarkTheme(themePreference));
    }
}
