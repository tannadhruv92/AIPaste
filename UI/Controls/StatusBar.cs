using System.Drawing;
using System.Windows.Forms;

namespace AIPaste.UI.Controls;

/// <summary>
/// Bottom strip showing ambient state: auth dot, current model,
/// custom action count and a contextual hint on the right.
/// </summary>
public class StatusBar : Control
{
    private bool _authenticated = true;
    private string _modelText = "(no model)";
    private int _customActionCount;
    private string _hint = string.Empty;
    private string _modeChip = string.Empty;

    public StatusBar()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.RailBg;
        Height = Theme.StatusBarHeight;
        Font = Theme.Small();
    }

    public bool Authenticated { get => _authenticated; set { _authenticated = value; Invalidate(); } }
    public string ModelText { get => _modelText; set { _modelText = value ?? string.Empty; Invalidate(); } }
    public int CustomActionCount { get => _customActionCount; set { _customActionCount = value; Invalidate(); } }
    public string Hint { get => _hint; set { _hint = value ?? string.Empty; Invalidate(); } }
    public string ModeChip { get => _modeChip; set { _modeChip = value ?? string.Empty; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Background
        using (var bg = new SolidBrush(Theme.RailBg)) g.FillRectangle(bg, ClientRectangle);
        using (var pen = new Pen(Theme.Border, 1f)) g.DrawLine(pen, 0, 0, Width, 0);

        int x = 14;
        int y = (Height - 14) / 2;

        // Auth dot
        Color dotColor = _authenticated ? Theme.Success : Theme.Danger;
        var dotRect = new Rectangle(x, y + 3, 8, 8);
        using (var b = new SolidBrush(dotColor)) g.FillEllipse(b, dotRect);
        x += 14;
        string authText = _authenticated ? "Authenticated" : "Not authenticated";
        var sz = TextRenderer.MeasureText(g, authText, Font, Size.Empty, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, authText, Font, new Point(x, y + (14 - sz.Height) / 2 + 1), Theme.TextDim, TextFormatFlags.NoPadding);
        x += sz.Width + 16;

        // Model
        if (!string.IsNullOrEmpty(_modelText))
        {
            string modelText = "⚡ " + _modelText;
            sz = TextRenderer.MeasureText(g, modelText, Font, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, modelText, Font, new Point(x, y + (14 - sz.Height) / 2 + 1), Theme.TextDim, TextFormatFlags.NoPadding);
            x += sz.Width + 16;
        }

        // Mode chip (e.g. "Hindi · Professional")
        if (!string.IsNullOrEmpty(_modeChip))
        {
            sz = TextRenderer.MeasureText(g, _modeChip, Font, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, _modeChip, Font, new Point(x, y + (14 - sz.Height) / 2 + 1), Theme.TextDim, TextFormatFlags.NoPadding);
            x += sz.Width + 16;
        }

        // Custom action count
        if (_customActionCount > 0)
        {
            string caText = $"📋 {_customActionCount} custom action{(_customActionCount == 1 ? "" : "s")}";
            sz = TextRenderer.MeasureText(g, caText, Font, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, caText, Font, new Point(x, y + (14 - sz.Height) / 2 + 1), Theme.TextDim, TextFormatFlags.NoPadding);
        }

        // Right-side hint
        if (!string.IsNullOrEmpty(_hint))
        {
            sz = TextRenderer.MeasureText(g, _hint, Font, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, _hint, Font, new Point(Width - sz.Width - 14, y + (14 - sz.Height) / 2 + 1), Theme.TextMuted, TextFormatFlags.NoPadding);
        }
    }
}
