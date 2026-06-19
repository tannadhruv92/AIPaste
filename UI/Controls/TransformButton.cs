using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace AIPaste.UI.Controls;

/// <summary>
/// Big round "Transform" action button for the Transform Studio spine. A gradient
/// circle with a white arrow, with the current verb label beneath.
/// </summary>
public class TransformButton : Control
{
    private const int CircleSize = 60;
    private const int CircleTop = 6;
    private string _verb = "Rewrite";
    private bool _hovered;

    public TransformButton()
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
        Font = Theme.BodyBold();
        Cursor = Cursors.Hand;
        Height = 92;
        TabStop = false;
    }

    /// <summary>Label shown under the circle (e.g. "Rewrite", "Translate", "Custom").</summary>
    public string Verb
    {
        get => _verb;
        set { value ??= string.Empty; if (_verb != value) { _verb = value; Invalidate(); } }
    }

    public event EventHandler? Clicked;

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left) Clicked?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        int cx = Width / 2;
        var circle = new Rectangle(cx - CircleSize / 2, CircleTop, CircleSize, CircleSize);

        // Gradient circle (no drop shadow / glow).
        g.FillGradientRounded(Theme.Accent, Theme.Accent2, circle, CircleSize / 2);

        // Subtle hover lift instead of a shadow.
        if (_hovered)
        {
            using var hi = new SolidBrush(Color.FromArgb(28, 255, 255, 255));
            g.FillEllipse(hi, circle);
        }

        // White arrow centred in the circle.
        using (var arrowFont = new Font(Theme.FontFamilyFallback, 20f, FontStyle.Regular))
            TextRenderer.DrawText(g, "→", arrowFont, circle, Theme.AccentInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        // Verb label beneath the circle.
        using var verbFont = Theme.BodyBold();
        var verbRect = new Rectangle(0, circle.Bottom, Width, Height - circle.Bottom);
        TextRenderer.DrawText(g, _verb, verbFont, verbRect, Theme.TextDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
            | TextFormatFlags.EndEllipsis);
    }
}
