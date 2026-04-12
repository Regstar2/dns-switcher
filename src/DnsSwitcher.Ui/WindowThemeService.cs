using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace DnsSwitcher.Ui;

internal static class WindowThemeService
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwaColorDefault = unchecked((int)0xFFFFFFFF);
    private const int DarkCaptionColor = 0x0023211B;
    private const int DarkCaptionTextColor = 0x00F6F4F3;

    public static void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        ApplyIcon(window);
        window.SourceInitialized += OnSourceInitialized;
    }

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        ApplyIcon(window);
        ApplyChrome(window);
    }

    private static void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            Apply(window);
        }
    }

    private static void ApplyIcon(Window window)
    {
        try
        {
            var iconPath = GetThemedIconPath();

            if (!File.Exists(iconPath))
            {
                return;
            }

            window.Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
        }
        catch
        {
            // The app must still run if optional icon files are missing or unreadable.
        }
    }

    private static string GetThemedIconPath()
    {
        var preferredIconName = App.IsDarkThemeActive ? "app-dark.ico" : "app.ico";
        var preferredIconPath = Path.Combine(AppContext.BaseDirectory, preferredIconName);

        if (File.Exists(preferredIconPath))
        {
            return preferredIconPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "app.ico");
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    private static void ApplyChrome(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;

            if (handle == IntPtr.Zero)
            {
                return;
            }

            var useDarkMode = App.IsDarkThemeActive ? 1 : 0;
            var result = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref useDarkMode, sizeof(int));

            if (result != 0)
            {
                _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref useDarkMode, sizeof(int));
            }

            var captionColor = App.IsDarkThemeActive ? DarkCaptionColor : DwmwaColorDefault;
            var textColor = App.IsDarkThemeActive ? DarkCaptionTextColor : DwmwaColorDefault;

            _ = DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref captionColor, sizeof(int));
            _ = DwmSetWindowAttribute(handle, DwmwaTextColor, ref textColor, sizeof(int));
        }
        catch
        {
            // Unsupported Windows builds should still run with the default title bar.
        }
    }
}
