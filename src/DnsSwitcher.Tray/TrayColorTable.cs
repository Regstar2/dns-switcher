using System.Drawing;

namespace DnsSwitcher.Tray;

internal sealed class TrayColorTable(TrayThemePalette palette) : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => palette.BackgroundRaised;
    public override Color MenuItemSelected => palette.Hover;
    public override Color MenuItemSelectedGradientBegin => palette.Hover;
    public override Color MenuItemSelectedGradientEnd => palette.Hover;
    public override Color MenuItemPressedGradientBegin => palette.Pressed;
    public override Color MenuItemPressedGradientMiddle => palette.Pressed;
    public override Color MenuItemPressedGradientEnd => palette.Pressed;
    public override Color MenuBorder => palette.Border;
    public override Color SeparatorDark => palette.Border;
    public override Color SeparatorLight => palette.Border;
    public override Color ImageMarginGradientBegin => palette.BackgroundRaised;
    public override Color ImageMarginGradientMiddle => palette.BackgroundRaised;
    public override Color ImageMarginGradientEnd => palette.BackgroundRaised;
    public override Color CheckBackground => palette.Selection;
    public override Color CheckSelectedBackground => palette.Selection;
    public override Color CheckPressedBackground => palette.Selection;
}
