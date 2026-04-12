using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DnsSwitcher.Tray;

internal sealed class TrayIconProvider : IDisposable
{
    private readonly Dictionary<(TrayIconState State, bool IsDarkTheme), Icon> icons = [];
    private readonly Icon lightBaseIcon;
    private readonly Icon darkBaseIcon;

    public TrayIconProvider()
    {
        lightBaseIcon = new Icon(GetAppIconPath(isDarkTheme: false), 32, 32);
        darkBaseIcon = new Icon(GetAppIconPath(isDarkTheme: true), 32, 32);
    }

    public Icon GetIcon(TrayIconState state, bool isDarkTheme)
    {
        var key = (state, isDarkTheme);

        if (!icons.TryGetValue(key, out var icon))
        {
            icon = CreateIcon(state, isDarkTheme);
            icons[key] = icon;
        }

        return icon;
    }

    public void Dispose()
    {
        foreach (var icon in icons.Values)
        {
            icon.Dispose();
        }

        icons.Clear();
        lightBaseIcon.Dispose();
        darkBaseIcon.Dispose();
    }

    private Icon CreateIcon(TrayIconState state, bool isDarkTheme)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);

        var baseIcon = isDarkTheme ? darkBaseIcon : lightBaseIcon;
        graphics.DrawIcon(baseIcon, new Rectangle(-1, -1, 34, 34));

        var accentColor = GetAccentColor(state);

        using var outlineBrush = new SolidBrush(isDarkTheme ? Color.FromArgb(210, 255, 255, 255) : Color.FromArgb(190, 18, 20, 24));
        using var accentBrush = new SolidBrush(accentColor);

        graphics.FillEllipse(outlineBrush, 22, 22, 8, 8);
        graphics.FillEllipse(accentBrush, 23, 23, 6, 6);

        var iconHandle = bitmap.GetHicon();

        try
        {
            using var sourceIcon = Icon.FromHandle(iconHandle);
            return (Icon)sourceIcon.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private static string GetAppIconPath(bool isDarkTheme)
    {
        var iconName = isDarkTheme ? "app-dark.ico" : "app.ico";
        var appContextIconPath = Path.Combine(AppContext.BaseDirectory, iconName);

        if (File.Exists(appContextIconPath))
        {
            return appContextIconPath;
        }

        var sourceIconPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", iconName));

        if (File.Exists(sourceIconPath))
        {
            return sourceIconPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "app.ico"));
    }

    private static Color GetAccentColor(TrayIconState state)
    {
        return state switch
        {
            TrayIconState.Managed => Color.FromArgb(40, 199, 111),
            TrayIconState.Dhcp => Color.FromArgb(128, 138, 151),
            TrayIconState.Warning => Color.FromArgb(245, 166, 35),
            TrayIconState.Error => Color.FromArgb(224, 61, 61),
            _ => Color.FromArgb(110, 168, 255),
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
