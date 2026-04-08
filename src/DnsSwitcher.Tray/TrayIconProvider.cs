using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace DnsSwitcher.Tray;

internal sealed class TrayIconProvider : IDisposable
{
    private readonly Dictionary<TrayIconState, Icon> icons = [];

    public Icon GetIcon(TrayIconState state)
    {
        if (!icons.TryGetValue(state, out var icon))
        {
            icon = CreateIcon(state);
            icons[state] = icon;
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
    }

    private static Icon CreateIcon(TrayIconState state)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.Transparent);

        var backgroundColor = Color.FromArgb(32, 87, 201);
        var accentColor = GetAccentColor(state);

        using var bodyBrush = new SolidBrush(backgroundColor);
        using var accentBrush = new SolidBrush(accentColor);
        using var glyphBrush = new SolidBrush(Color.White);
        using var bodyPath = CreateRoundedRectangle(new RectangleF(3, 3, 26, 26), 8);

        graphics.FillPath(bodyBrush, bodyPath);
        graphics.FillEllipse(accentBrush, 21, 21, 8, 8);

        using var font = new Font("Segoe UI", 13.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        graphics.DrawString("D", font, glyphBrush, new RectangleF(4, 3, 20, 20), format);

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

    private static GraphicsPath CreateRoundedRectangle(RectangleF rectangle, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;

        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
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
