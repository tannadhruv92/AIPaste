using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace AIPaste.UI.Controls;

/// <summary>
/// Pill-shaped chip used in toolbar groups (Mode/Tone/Language/Action).
/// Supports active/inactive states, optional dashed style.
/// </summary>
public class ChipButton : Control
{
    private bool _active;
    private bool _dashed;
    private bool _hovered;
    private string _glyph = string.Empty;

    public ChipButton()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        ForeColor = Theme.TextDim;
        Font = Theme.Body();
        Cursor = Cursors.Hand;
        Padding = new Padding(12, 6, 12, 6);
        Height = 28;
        TabStop = false;
    }

    [DefaultValue(false)]
    public bool Active
    {
        get => _active;
        set { if (_active != value) { _active = value; Invalidate(); ActiveChanged?.Invoke(this, EventArgs.Empty); } }
    }

    [DefaultValue(false)]
    public bool Dashed
    {
        get => _dashed;
        set { if (_dashed != value) { _dashed = value; Invalidate(); } }
    }

    /// <summary>Optional emoji/glyph drawn before the text (e.g. "✨", "🌐").</summary>
    [DefaultValue("")]
    public string Glyph
    {
        get => _glyph;
        set { if (_glyph != (value ?? string.Empty)) { _glyph = value ?? string.Empty; AdjustWidth(); Invalidate(); } }
    }

    public event EventHandler? ActiveChanged;

    public override string Text
    {
        get => base.Text;
        set { base.Text = value ?? string.Empty; AdjustWidth(); Invalidate(); }
    }

    public void AdjustWidth()
    {
        using var g = CreateGraphics();
        var display = string.IsNullOrEmpty(_glyph) ? Text : $"{_glyph}  {Text}";
        var size = TextRenderer.MeasureText(g, display, Font, Size.Empty, TextFormatFlags.NoPadding);
        Width = size.Width + Padding.Horizontal;
        if (Height < 28) Height = 28;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        Color fill;
        Color border;
        Color textColor;
        if (_active)
        {
            fill = Theme.AccentSoft;
            border = Color.FromArgb(140, Theme.Accent);
            textColor = Theme.Accent;
        }
        else if (_dashed)
        {
            fill = Color.Transparent;
            border = Theme.Border;
            textColor = _hovered ? Theme.Accent : Theme.TextMuted;
            if (_hovered) border = Theme.Accent;
        }
        else
        {
            fill = _hovered ? Theme.Surface3 : Color.FromArgb(10, 255, 255, 255);
            border = Theme.Border;
            textColor = _hovered ? Theme.Text : Theme.TextDim;
        }

        if (fill.A > 0)
            g.FillRounded(fill, rect, rect.Height / 2);

        if (_dashed)
        {
            using var pen = new Pen(border, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            using var path = GraphicsExt.PillRect(new Rectangle(rect.X, rect.Y, rect.Width, rect.Height));
            g.DrawPath(pen, path);
        }
        else
        {
            g.DrawRoundedBorder(border, rect, rect.Height / 2, 1f);
        }

        var display = string.IsNullOrEmpty(_glyph) ? Text : $"{_glyph}  {Text}";
        TextRenderer.DrawText(g, display, Font, ClientRectangle, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
