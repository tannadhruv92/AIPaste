using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AIPaste.UI.Controls;

/// <summary>
/// Pill-shaped split button. Left half = model picker (lightning + name + caret).
/// Right half = sparkle ✨ + action label (e.g. "Process" / "Translate").
/// Single rounded outline + glow + a subtle inner divider line.
/// </summary>
public class SplitActionButton : Control
{
    private string _modelName = "gpt-4o";
    private string _actionLabel = "Process";
    private bool _hovered;
    private bool _hoverModel;
    private bool _hoverAction;

    public event EventHandler? ModelClicked;
    public event EventHandler? ActionClicked;

    public string ModelName
    {
        get => _modelName;
        set { _modelName = value ?? string.Empty; RecalcSize(); Invalidate(); }
    }

    public string ActionLabel
    {
        get => _actionLabel;
        set { _actionLabel = value ?? string.Empty; RecalcSize(); Invalidate(); }
    }

    public SplitActionButton()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Font = Theme.Body();
        Cursor = Cursors.Hand;
        Height = 36;
        TabStop = true;
        RecalcSize();
    }

    private const int PaddingX = 14;
    private const int Gap = 7;

    private Rectangle _modelRect;
    private Rectangle _actionRect;

    public void RecalcSize()
    {
        using var g = CreateGraphics();
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        var modelText = $"⚡  {_modelName}  ▾";
        var actionText = $"✨  {_actionLabel}";
        var sizeM = TextRenderer.MeasureText(g, modelText, Font, Size.Empty, TextFormatFlags.NoPadding);
        var sizeA = TextRenderer.MeasureText(g, actionText, Theme.BodyBold(), Size.Empty, TextFormatFlags.NoPadding);
        int w = sizeM.Width + sizeA.Width + PaddingX * 4 + 2; // 2 = divider
        Width = w;
        if (Height < 36) Height = 36;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ComputeRects();
    }

    private void ComputeRects()
    {
        using var g = CreateGraphics();
        var modelText = $"⚡  {_modelName}  ▾";
        var sizeM = TextRenderer.MeasureText(g, modelText, Font, Size.Empty, TextFormatFlags.NoPadding);
        int modelW = sizeM.Width + PaddingX * 2;
        _modelRect = new Rectangle(0, 0, modelW, Height);
        _actionRect = new Rectangle(modelW + 2, 0, Math.Max(0, Width - modelW - 2), Height);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var inModel = _modelRect.Contains(e.Location);
        var inAction = _actionRect.Contains(e.Location);
        if (inModel != _hoverModel || inAction != _hoverAction)
        {
            _hoverModel = inModel;
            _hoverAction = inAction;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; _hoverModel = _hoverAction = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        if (_modelRect.Contains(e.Location)) ModelClicked?.Invoke(this, EventArgs.Empty);
        else if (_actionRect.Contains(e.Location)) ActionClicked?.Invoke(this, EventArgs.Empty);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (Focused && (keyData == Keys.Enter || keyData == Keys.Space))
        {
            ActionClicked?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ComputeRects();
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var fullRect = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Height / 2;

        // Glow halo (drawn slightly outside)
        using (var path = GraphicsExt.RoundedRect(fullRect, radius))
        using (var pgb = new PathGradientBrush(path))
        {
            pgb.CenterColor = Theme.AccentGlow;
            pgb.SurroundColors = new[] { Color.FromArgb(0, Theme.Accent) };
            // very subtle — already handled by drop-shadow look below
        }

        // Body fill (model side = surface, action side = gradient)
        using (var path = GraphicsExt.RoundedRect(fullRect, radius))
        {
            // Clip and fill the model half
            using var modelClip = GraphicsExt.RoundedRect(fullRect, radius);
            g.SetClip(modelClip);
            using (var modelBrush = new SolidBrush(_hoverModel ? Theme.Surface3 : Theme.Surface))
            {
                g.FillRectangle(modelBrush, _modelRect);
            }
            // Action half gradient
            using (var actBrush = new LinearGradientBrush(
                new Rectangle(_actionRect.X, _actionRect.Y, Math.Max(1, _actionRect.Width), Math.Max(1, _actionRect.Height)),
                Theme.Accent, Theme.Accent2, 135f))
            {
                if (_hoverAction)
                {
                    using var b2 = new LinearGradientBrush(
                        new Rectangle(_actionRect.X, _actionRect.Y, Math.Max(1, _actionRect.Width), Math.Max(1, _actionRect.Height)),
                        Lighten(Theme.Accent, 0.1f), Lighten(Theme.Accent2, 0.1f), 135f);
                    g.FillRectangle(b2, _actionRect);
                }
                else
                {
                    g.FillRectangle(actBrush, _actionRect);
                }
            }
            g.ResetClip();
        }

        // Inner divider
        using (var pen = new Pen(Theme.Border, 1f))
        {
            g.DrawLine(pen, _modelRect.Right, 6, _modelRect.Right, Height - 7);
        }

        // Outer border
        Color borderCol = _hovered ? Theme.Accent : Theme.Border;
        g.DrawRoundedBorder(borderCol, fullRect, radius, 1f);

        // Model text: lightning emoji + name + caret
        var modelText = $"⚡  {_modelName}  ▾";
        TextRenderer.DrawText(g, modelText, Font, _modelRect, Theme.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        // Action text: sparkle + label  (dark ink on accent gradient)
        var actionText = $"✨  {_actionLabel}";
        using var boldFont = Theme.BodyBold();
        TextRenderer.DrawText(g, actionText, boldFont, _actionRect, Theme.AccentInk,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static Color Lighten(Color c, float amount)
    {
        int r = Math.Min(255, c.R + (int)(255 * amount));
        int gg = Math.Min(255, c.G + (int)(255 * amount));
        int b = Math.Min(255, c.B + (int)(255 * amount));
        return Color.FromArgb(c.A, r, gg, b);
    }
}
