using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace AIPaste.UI.Controls;

/// <summary>
/// Factory for polished, theme-aware context menus that match the "Transform Studio"
/// light/dark palette: a rounded card, an accent-tinted hover row, and a trailing ✓ on
/// the active item. All colours come from <see cref="Theme"/> so the menu auto-adapts to
/// Light and Dark themes.
/// </summary>
public static class ThemedMenu
{
    /// <summary>Creates a styled <see cref="ContextMenuStrip"/> using the themed renderer.</summary>
    public static ContextMenuStrip Create()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            ShowImageMargin = false,
            ShowCheckMargin = false,
            DropShadowEnabled = false,
            Font = Theme.Body(),
            Padding = new Padding(5),
            Renderer = new ThemedMenuRenderer(),
        };

        // Give each row comfortable padding with room for text + a trailing check (~30px tall).
        menu.ItemAdded += (_, e) =>
        {
            if (e.Item is ToolStripMenuItem item)
                item.Padding = new Padding(8, 6, 24, 6);
        };

        // Round the actual window corners once the final size is known.
        menu.Opened += (_, _) =>
        {
            using var p = GraphicsExt.RoundedRect(new Rectangle(0, 0, menu.Width, menu.Height), 10);
            menu.Region = new System.Drawing.Region(p);
        };

        return menu;
    }
}

/// <summary>Professional renderer that paints the themed rounded card + accent selection.</summary>
internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
{
    // Cached to avoid per-paint allocation (fonts are stable across theme switches).
    private static readonly Font _normal = Theme.Body();
    private static readonly Font _bold = Theme.BodyBold();

    public ThemedMenuRenderer() : base(new Colors()) { RoundedEdges = true; }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        => e.Graphics.Clear(Theme.Surface);

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        using var path = GraphicsExt.RoundedRect(rect, 10);
        using var pen = new Pen(Theme.Border, 1f);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var item = e.Item;

        if (item.Selected && item.Enabled)
        {
            var sel = new Rectangle(3, 1, item.Width - 6, item.Height - 2);
            e.Graphics.FillRounded(Theme.AccentSoft, sel, 7);
        }

        if (item is ToolStripMenuItem mi && mi.Checked)
        {
            var slot = new Rectangle(item.Width - 20, 0, 18, item.Height);
            TextRenderer.DrawText(e.Graphics, "✓", _bold, slot, Theme.Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        bool isChecked = (e.Item as ToolStripMenuItem)?.Checked == true;
        e.TextColor = e.Item.Selected ? Theme.Accent : (isChecked ? Theme.Accent : Theme.Text);
        e.TextFont = isChecked ? _bold : _normal;
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(Theme.Border, 1f);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    /// <summary>Flattens the default gradients so our own painting shows through.</summary>
    private sealed class Colors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Surface;
        public override Color ImageMarginGradientBegin => Theme.Surface;
        public override Color ImageMarginGradientMiddle => Theme.Surface;
        public override Color ImageMarginGradientEnd => Theme.Surface;
        public override Color MenuStripGradientBegin => Theme.Surface;
        public override Color MenuStripGradientEnd => Theme.Surface;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Color.Transparent;
        public override Color MenuItemSelectedGradientBegin => Color.Transparent;
        public override Color MenuItemSelectedGradientEnd => Color.Transparent;
        public override Color MenuItemPressedGradientBegin => Color.Transparent;
        public override Color MenuItemPressedGradientEnd => Color.Transparent;
    }
}
