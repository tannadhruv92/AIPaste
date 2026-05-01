using System.Drawing;
using System.Drawing.Drawing2D;

namespace AIPaste.UI;

/// <summary>
/// Reusable drawing helpers for rounded surfaces, pill chips, glows, etc.
/// </summary>
internal static class GraphicsExt
{
    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        // Top-left
        path.AddArc(arc, 180, 90);
        // Top-right
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        // Bottom-right
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        // Bottom-left
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }

    public static GraphicsPath PillRect(Rectangle bounds)
        => RoundedRect(bounds, bounds.Height / 2);

    public static void FillRounded(this Graphics g, Color color, Rectangle bounds, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var path = RoundedRect(bounds, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedBorder(this Graphics g, Color color, Rectangle bounds, int radius, float thickness = 1f)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var inset = (int)Math.Ceiling(thickness / 2f);
        var rect = new Rectangle(bounds.X + inset - 1, bounds.Y + inset - 1, bounds.Width - inset, bounds.Height - inset);
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var path = RoundedRect(rect, radius);
        using var pen = new Pen(color, thickness);
        g.DrawPath(pen, path);
    }

    public static void FillGradientRounded(this Graphics g, Color start, Color end, Rectangle bounds, int radius, float angle = 135f)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var path = RoundedRect(bounds, radius);
        using var brush = new LinearGradientBrush(bounds, start, end, angle);
        g.FillPath(brush, path);
    }
}
