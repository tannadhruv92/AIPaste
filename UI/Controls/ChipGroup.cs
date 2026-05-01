using System.Drawing;
using System.Windows.Forms;

namespace AIPaste.UI.Controls;

/// <summary>
/// A labelled group of mutually-exclusive ChipButtons (radio-style).
/// E.g. "TONE  [Professional] [Casual] [Informative]"
/// </summary>
public class ChipGroup : FlowLayoutPanel
{
    private readonly Label _label;
    private readonly bool _exclusive;

    public ChipGroup(string label, bool exclusive = true)
    {
        _exclusive = exclusive;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FlowDirection = FlowDirection.LeftToRight;
        WrapContents = false;
        BackColor = Color.Transparent;
        Margin = new Padding(0);
        Padding = new Padding(0);

        _label = new Label
        {
            Text = label.ToUpperInvariant(),
            ForeColor = Theme.TextMuted,
            Font = new Font(Theme.FontFamily, 7.5f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 7, 8, 0),
        };
        Controls.Add(_label);
    }

    public string Selected => GetSelectedChip()?.Text ?? string.Empty;
    public ChipButton? SelectedChip => GetSelectedChip();
    public event EventHandler? SelectionChanged;

    public ChipButton AddChip(string text, string glyph = "", bool active = false, bool dashed = false)
    {
        var chip = new ChipButton
        {
            Text = text,
            Glyph = glyph,
            Active = active,
            Dashed = dashed,
            Margin = new Padding(0, 0, 6, 0),
        };
        chip.Click += OnChipClicked;
        Controls.Add(chip);
        chip.AdjustWidth();
        return chip;
    }

    public void ClearChips()
    {
        for (int i = Controls.Count - 1; i >= 0; i--)
        {
            if (Controls[i] is ChipButton cb)
            {
                cb.Click -= OnChipClicked;
                Controls.RemoveAt(i);
                cb.Dispose();
            }
        }
    }

    public void SelectChipByText(string text)
    {
        foreach (Control c in Controls)
        {
            if (c is ChipButton cb)
                cb.Active = string.Equals(cb.Text, text, StringComparison.OrdinalIgnoreCase);
        }
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnChipClicked(object? sender, EventArgs e)
    {
        if (sender is not ChipButton clicked) return;
        if (clicked.Dashed)
        {
            // Dashed chips are "actions" (e.g. ＋ More…); just bubble click, don't change selection.
            DashedClicked?.Invoke(clicked, EventArgs.Empty);
            return;
        }
        if (_exclusive)
        {
            foreach (Control c in Controls)
                if (c is ChipButton cb)
                    cb.Active = ReferenceEquals(cb, clicked);
        }
        else
        {
            clicked.Active = !clicked.Active;
        }
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? DashedClicked;

    private ChipButton? GetSelectedChip()
    {
        foreach (Control c in Controls)
            if (c is ChipButton cb && cb.Active)
                return cb;
        return null;
    }
}
