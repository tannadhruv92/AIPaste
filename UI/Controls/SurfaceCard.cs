using System.Drawing;
using System.Windows.Forms;

namespace AIPaste.UI.Controls;

/// <summary>
/// Container that draws a rounded background + border around child content.
/// Used for the chip toolbar, action bar, original-text card and result card.
/// </summary>
public class SurfaceCard : Panel
{
    public int CornerRadius { get; set; } = Theme.CornerRadiusLg;
    public Color FillColor { get; set; } = Theme.Surface2;
    public Color FillColor2 { get; set; } = Color.Empty; // optional gradient end
    public Color BorderColor { get; set; } = Theme.Border;
    public bool DrawBorder { get; set; } = true;

    public SurfaceCard()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);
        // Use the outer surface as our backcolor so the rounded corners blend
        // with the pane behind us. The custom paint draws our actual fill on top.
        BackColor = Theme.Surface;
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (FillColor2 == Color.Empty || FillColor2.IsEmpty)
            g.FillRounded(FillColor, rect, CornerRadius);
        else
            g.FillGradientRounded(FillColor, FillColor2, rect, CornerRadius, 90f);
        if (DrawBorder)
            g.DrawRoundedBorder(BorderColor, rect, CornerRadius, 1f);
    }
}
