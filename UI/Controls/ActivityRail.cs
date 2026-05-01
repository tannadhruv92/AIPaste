using System.Drawing;
using System.Windows.Forms;

namespace AIPaste.UI.Controls;

public enum AppPane
{
    Process,
    CustomActions,
    Settings
}

/// <summary>
/// Left activity rail (VS Code style). Logo at top, three pane icons,
/// settings cog at the bottom.
/// </summary>
public class ActivityRail : Control
{
    public event EventHandler<AppPane>? PaneSelected;

    private AppPane _selected = AppPane.Process;
    public AppPane Selected
    {
        get => _selected;
        set { if (_selected != value) { _selected = value; Invalidate(); PaneSelected?.Invoke(this, value); } }
    }

    private readonly List<RailItem> _items = new();
    private int _hoverIndex = -1;
    private ToolTip _tooltip = new();

    private record RailItem(AppPane Pane, string Glyph, string Tooltip, bool BottomAligned);

    public ActivityRail()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.RailBg;
        Width = Theme.RailWidth;

        _items.Add(new(AppPane.Process, "⚡", "Process (clipboard)", false));
        _items.Add(new(AppPane.CustomActions, "📋", "Custom Actions", false));
        _items.Add(new(AppPane.Settings, "⚙", "Settings", true));
    }

    private const int LogoTop = 8;
    private const int LogoSize = 36;
    private const int ItemSize = 40;
    private const int ItemTopOffset = LogoTop + LogoSize + 12;

    private Rectangle ItemRect(int index, bool bottom = false)
    {
        int x = (Width - ItemSize) / 2;
        if (!bottom)
        {
            int y = ItemTopOffset + index * (ItemSize + 4);
            return new Rectangle(x, y, ItemSize, ItemSize);
        }
        else
        {
            int y = Height - ItemSize - 8;
            return new Rectangle(x, y, ItemSize, ItemSize);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int idx = HitTest(e.Location);
        if (idx != _hoverIndex)
        {
            _hoverIndex = idx;
            if (idx >= 0)
                _tooltip.SetToolTip(this, _items[idx].Tooltip);
            else
                _tooltip.SetToolTip(this, string.Empty);
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hoverIndex = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int idx = HitTest(e.Location);
        if (idx >= 0) Selected = _items[idx].Pane;
    }

    private int HitTest(Point pt)
    {
        // top-aligned items
        int topCount = _items.Count(i => !i.BottomAligned);
        for (int i = 0; i < topCount; i++)
            if (ItemRect(i, false).Contains(pt)) return i;

        // bottom-aligned (single — settings)
        for (int i = topCount; i < _items.Count; i++)
            if (ItemRect(i - topCount, true).Contains(pt)) return i;
        return -1;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Background
        using (var bg = new SolidBrush(Theme.RailBg))
            g.FillRectangle(bg, ClientRectangle);

        // Right edge separator
        using (var pen = new Pen(Theme.Border, 1f))
            g.DrawLine(pen, Width - 1, 0, Width - 1, Height);

        // Logo
        var logoRect = new Rectangle((Width - LogoSize) / 2, LogoTop, LogoSize, LogoSize);
        g.FillGradientRounded(Theme.Accent, Theme.Accent2, logoRect, 8, 135f);
        using var logoFont = new Font(Theme.FontFamily, 13f, FontStyle.Bold);
        TextRenderer.DrawText(g, "A", logoFont, logoRect, Theme.AccentInk,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        // Items
        int topCount = _items.Count(i => !i.BottomAligned);
        for (int i = 0; i < topCount; i++)
            DrawItem(g, _items[i], ItemRect(i, false), i == _hoverIndex);

        for (int i = topCount; i < _items.Count; i++)
            DrawItem(g, _items[i], ItemRect(i - topCount, true), i == _hoverIndex);
    }

    private void DrawItem(Graphics g, RailItem item, Rectangle rect, bool hovered)
    {
        bool active = item.Pane == _selected;
        if (active)
        {
            // Left accent bar
            using var bar = new SolidBrush(Theme.Accent);
            g.FillRectangle(bar, new Rectangle(0, rect.Y + 8, 3, rect.Height - 16));
        }
        if (hovered && !active)
        {
            g.FillRounded(Theme.Surface2, rect, 8);
        }
        Color color = active ? Theme.Accent : (hovered ? Theme.Text : Theme.TextMuted);
        using var f = new Font("Segoe UI Emoji", 13f, FontStyle.Regular);
        TextRenderer.DrawText(g, item.Glyph, f, rect, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
