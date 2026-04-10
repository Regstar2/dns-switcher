using System.Drawing;

namespace DnsSwitcher.Tray;

internal sealed record TrayThemePalette(
    Color Background,
    Color BackgroundRaised,
    Color BackgroundMuted,
    Color Foreground,
    Color ForegroundMuted,
    Color Border,
    Color Hover,
    Color Pressed,
    Color Selection)
{
    public static TrayThemePalette Dark { get; } = new(
        Background: Color.FromArgb(0x25, 0x28, 0x2D),
        BackgroundRaised: Color.FromArgb(0x2B, 0x2F, 0x35),
        BackgroundMuted: Color.FromArgb(0x20, 0x23, 0x28),
        Foreground: Color.FromArgb(0xF3, 0xF4, 0xF6),
        ForegroundMuted: Color.FromArgb(0xB7, 0xBD, 0xC6),
        Border: Color.FromArgb(0x3E, 0x44, 0x4D),
        Hover: Color.FromArgb(0x33, 0x38, 0x41),
        Pressed: Color.FromArgb(0x3A, 0x40, 0x48),
        Selection: Color.FromArgb(0x41, 0x48, 0x51));

    public static TrayThemePalette Light { get; } = new(
        Background: Color.White,
        BackgroundRaised: Color.White,
        BackgroundMuted: Color.FromArgb(0xF3, 0xF4, 0xF6),
        Foreground: Color.FromArgb(0x11, 0x18, 0x27),
        ForegroundMuted: Color.FromArgb(0x4B, 0x55, 0x63),
        Border: Color.FromArgb(0xD1, 0xD5, 0xDB),
        Hover: Color.FromArgb(0xE8, 0xED, 0xF3),
        Pressed: Color.FromArgb(0xDD, 0xE4, 0xEC),
        Selection: Color.FromArgb(0xE1, 0xE7, 0xEF));
}
