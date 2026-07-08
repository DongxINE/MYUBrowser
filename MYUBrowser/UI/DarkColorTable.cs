namespace MYUBrowser.UI;

/// <summary>深色工具栏/菜单配色，供各窗口与视图复用。</summary>
public sealed class DarkColorTable : ProfessionalColorTable
{
    private static readonly Color Bg = Color.FromArgb(32, 32, 32);
    private static readonly Color Hover = Color.FromArgb(55, 55, 55);
    private static readonly Color Border = Color.FromArgb(64, 64, 64);

    public override Color ToolStripGradientBegin => Bg;
    public override Color ToolStripGradientMiddle => Bg;
    public override Color ToolStripGradientEnd => Bg;
    public override Color ToolStripBorder => Bg;
    public override Color ButtonSelectedHighlight => Hover;
    public override Color ButtonSelectedGradientBegin => Hover;
    public override Color ButtonSelectedGradientMiddle => Hover;
    public override Color ButtonSelectedGradientEnd => Hover;
    public override Color ButtonSelectedBorder => Border;
    public override Color ButtonPressedGradientBegin => Hover;
    public override Color ButtonPressedGradientMiddle => Hover;
    public override Color ButtonPressedGradientEnd => Hover;
    public override Color ButtonCheckedGradientBegin => Hover;
    public override Color ButtonCheckedGradientMiddle => Hover;
    public override Color ButtonCheckedGradientEnd => Hover;
    public override Color SeparatorDark => Border;
    public override Color SeparatorLight => Bg;
    public override Color MenuBorder => Border;
    public override Color ToolStripDropDownBackground => Bg;
    public override Color ImageMarginGradientBegin => Bg;
    public override Color ImageMarginGradientMiddle => Bg;
    public override Color ImageMarginGradientEnd => Bg;
    public override Color MenuItemSelected => Hover;
    public override Color MenuItemBorder => Border;
    public override Color MenuItemSelectedGradientBegin => Hover;
    public override Color MenuItemSelectedGradientEnd => Hover;
}
