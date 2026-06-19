using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace AIPaste.UI.Controls;

/// <summary>
/// Full-width spine mode button used in the Transform Studio layout. Acts like a
/// native select: shows a glyph + title (and optional subtitle), with a caret.
/// </summary>
public class SpineDropdownButton : Control
{
    private string _glyph = string.Empty;
    private string _title = string.Empty;
    private string? _subtitle;
    private bool _active;
    private bool _showCaret = true;
    private bool _hovered;

    public SpineDropdownButton()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor
            | ControlStyles.Selectable,
            true);
        BackColor = Color.Transparent;
        ForeColor = Theme.TextDim;
        Font = Theme.Body();
        Cursor = Cursors.Hand;
        Height = 44;
        TabStop = true;
    }

    /// <summary>Leading emoji/glyph, e.g. "✨", "🌐", "⚡", "📝".</summary>
    [DefaultValue("")]
    public string Glyph
    {
        get => _glyph;
        set { value ??= string.Empty; if (_glyph != value) { _glyph = value; Invalidate(); } }
    }

    /// <summary>Primary label drawn next to the glyph.</summary>
    [DefaultValue("")]
    public string Title
    {
        get => _title;
        set { value ??= string.Empty; if (_title != value) { _title = value; Invalidate(); } }
    }

    /// <summary>Optional second line shown under the title; toggles the control height.</summary>
    [DefaultValue(null)]
    public string? Subtitle
    {
        get => _subtitle;
        set
        {
            if (_subtitle == value) return;
            bool hadSubtitle = !string.IsNullOrEmpty(_subtitle);
            bool hasSubtitle = !string.IsNullOrEmpty(value);
            _subtitle = value;
            if (hadSubtitle != hasSubtitle) Height = hasSubtitle ? 54 : 44;
            Invalidate();
        }
    }

    [DefaultValue(false)]
    public bool Active
    {
        get => _active;
        set { if (_active != value) { _active = value; Invalidate(); } }
    }

    [DefaultValue(true)]
    public bool ShowCaret
    {
        get => _showCaret;
        set { if (_showCaret != value) { _showCaret = value; Invalidate(); } }
    }

    public event EventHandler? Clicked;

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left) Clicked?.Invoke(this, EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys keyData)
        => keyData == Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            Clicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        bool hasSub = !string.IsNullOrEmpty(_subtitle);

        Color fill, border, titleColor;
        if (_active)
        {
            fill = Theme.AccentSoft;
            border = Theme.Accent;
            titleColor = Theme.Accent;
        }
        else
        {
            fill = Theme.Surface3;
            border = _hovered ? Theme.BorderStrong : Theme.Border;
            titleColor = Theme.TextDim;
        }

        g.FillRounded(fill, rect, 10);
        g.DrawRoundedBorder(border, rect, 10, 1f);

        using var glyphFont = new Font("Segoe UI Emoji", 12.5f, FontStyle.Regular);
        using var titleFont = Theme.BodyBold();
        using var subFont = Theme.Small();
        using var caretFont = new Font(Theme.FontFamilyFallback, 9f, FontStyle.Regular);

        const int padX = 12;
        const int caretSlot = 22;
        int lineH = Math.Max(titleFont.Height, glyphFont.Height);
        int lineTop = hasSub ? (Height - (lineH + subFont.Height)) / 2 : 0;
        int lineHeight = hasSub ? lineH : Height;

        int x = padX;
        if (_glyph.Length > 0)
        {
            int glyphW = TextRenderer.MeasureText(g, _glyph, glyphFont, Size.Empty, TextFormatFlags.NoPadding).Width;
            var glyphRect = new Rectangle(x, lineTop, glyphW + 2, lineHeight);
            TextRenderer.DrawText(g, _glyph, glyphFont, glyphRect, titleColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            x += glyphW + 7;
        }

        var titleRect = new Rectangle(x, lineTop, Math.Max(0, Width - x - caretSlot), lineHeight);
        TextRenderer.DrawText(g, _title, titleFont, titleRect, titleColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
            | TextFormatFlags.EndEllipsis);

        if (hasSub)
        {
            var subRect = new Rectangle(padX, lineTop + lineH, Math.Max(0, Width - padX - caretSlot), subFont.Height + 2);
            TextRenderer.DrawText(g, _subtitle, subFont, subRect, Theme.Accent,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding
                | TextFormatFlags.EndEllipsis);
        }

        if (_showCaret)
        {
            var caretColor = _active ? Theme.Accent : Theme.TextMuted;
            var caretRect = new Rectangle(Width - caretSlot, 0, caretSlot - 6, Height);
            TextRenderer.DrawText(g, "▾", caretFont, caretRect, caretColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        // Focus ring — shown when the button holds keyboard focus.
        if (Focused)
        {
            var ring = new Rectangle(2, 2, Width - 5, Height - 5);
            using var ringPath = GraphicsExt.RoundedRect(ring, 8);
            using var ringPen = new Pen(Theme.Accent, 1.5f);
            g.DrawPath(ringPen, ringPath);
        }
    }
}
