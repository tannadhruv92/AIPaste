using System.Drawing;
using System.Windows.Forms;
using AIPaste.UI.Controls;

namespace AIPaste.UI.Panes;

/// <summary>
/// Master-detail Custom Actions editor. Sidebar list + detail pane on the right.
/// </summary>
public class CustomActionsPane : UserControl
{
    private SurfaceCard _listCard = null!;
    private Panel _listFlow = null!;
    private SurfaceCard _detailCard = null!;
    private TextBox _nameBox = null!;
    private TextBox _promptBox = null!;
    private Label _hintLabel = null!;

    private List<CustomAction> _actions = new();
    private CustomAction? _selected;

    public event EventHandler? ActionsChanged;

    public CustomActionsPane()
    {
        BackColor = Theme.Surface;
        Padding = new Padding(20);
        BuildLayout();
        ReloadList();
        HandleCreated += (_, _) => RelayoutListItems();
        VisibleChanged += (_, _) => { if (Visible) RelayoutListItems(); };
    }

    private void BuildLayout()
    {
        var top = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 6, 0, 8),
        };
        var newBtn = new Button
        {
            Text = "＋ New Action",
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Accent,
            ForeColor = Theme.AccentInk,
            Font = Theme.BodyBold(),
            Size = new Size(120, 32),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        newBtn.FlatAppearance.BorderSize = 0;
        newBtn.FlatAppearance.MouseOverBackColor = Theme.Accent2;
        newBtn.Click += (_, _) => StartNew();
        top.Controls.Add(newBtn);
        // Pin to right whenever the panel resizes.
        void RepositionNewBtn()
        {
            newBtn.Location = new Point(top.ClientSize.Width - newBtn.Width, 2);
        }
        top.SizeChanged += (_, _) => RepositionNewBtn();
        RepositionNewBtn();
        Controls.Add(top);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 8, 0, 0),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _listCard = new SurfaceCard
        {
            FillColor = Theme.Surface2,
            BorderColor = Theme.Border,
            CornerRadius = 0,  // disable rounded corners — they were clipping list items
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(0),
        };
        // Plain panel — items are positioned manually by ReloadList().
        _listFlow = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = false,
            BackColor = Theme.Surface2,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        _listCard.Controls.Add(_listFlow);
        _listFlow.SizeChanged += (_, _) => RelayoutListItems();

        _detailCard = new SurfaceCard
        {
            FillColor = Theme.Surface2,
            BorderColor = Theme.Border,
            CornerRadius = 8,
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
        };

        var detailLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent,
        };
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        var nameLbl = MakeFieldLabel("Name");
        _nameBox = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Surface3,
            ForeColor = Theme.Text,
            Font = Theme.Body(),
            Dock = DockStyle.Top,
            Margin = new Padding(0, 4, 0, 8),
            Height = 28,
        };
        var promptLbl = MakeFieldLabel("Prompt Template");

        _promptBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Surface3,
            ForeColor = Theme.Text,
            Font = Theme.Mono(),
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
        };

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 10, 0, 0),
        };
        var deleteBtn = new Button
        {
            Text = "🗑  Delete",
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface2,
            ForeColor = Theme.Danger,
            Font = Theme.Body(),
            AutoSize = true,
            Location = new Point(0, 10),
            Cursor = Cursors.Hand,
        };
        deleteBtn.FlatAppearance.BorderSize = 0;
        deleteBtn.Click += (_, _) => DeleteSelected();

        var saveBtn = new Button
        {
            Text = "💾  Save",
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Accent,
            ForeColor = Theme.AccentInk,
            Font = Theme.BodyBold(),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Size = new Size(96, 32),
            Cursor = Cursors.Hand,
        };
        saveBtn.FlatAppearance.BorderSize = 0;
        saveBtn.FlatAppearance.MouseOverBackColor = Theme.Accent2;
        saveBtn.Click += (_, _) => SaveSelected();
        saveBtn.Location = new Point(0, 8); // will reposition on resize

        _hintLabel = new Label
        {
            Text = "Use {text} as the placeholder for clipboard content.",
            ForeColor = Theme.TextMuted,
            Font = Theme.Small(),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Location = new Point(0, 16),
            BackColor = Color.Transparent,
        };

        footer.Controls.Add(deleteBtn);
        footer.Controls.Add(_hintLabel);
        footer.Controls.Add(saveBtn);
        footer.SizeChanged += (_, _) =>
        {
            saveBtn.Left = footer.ClientSize.Width - saveBtn.Width;
            _hintLabel.Left = deleteBtn.Right + 12;
        };

        detailLayout.Controls.Add(nameLbl, 0, 0);
        detailLayout.Controls.Add(_nameBox, 0, 1);
        detailLayout.Controls.Add(promptLbl, 0, 2);
        detailLayout.Controls.Add(_promptBox, 0, 3);
        detailLayout.Controls.Add(footer, 0, 4);

        _detailCard.Controls.Add(detailLayout);

        grid.Controls.Add(_listCard, 0, 0);
        grid.Controls.Add(_detailCard, 1, 0);
        Controls.Add(grid);
    }

    private Label MakeFieldLabel(string text) => new()
    {
        Text = text,
        ForeColor = Theme.TextDim,
        Font = Theme.SmallBold(),
        AutoSize = true,
        Margin = new Padding(0),
        BackColor = Color.Transparent,
    };

    public void ReloadList()
    {
        _actions = ConfigManager.GetCustomActions();
        _listFlow.SuspendLayout();
        _listFlow.Controls.Clear();
        foreach (var a in _actions)
        {
            var item = new CustomActionListItem(a);
            item.Click += (_, _) => Select(a);
            _listFlow.Controls.Add(item);
        }
        RelayoutListItems();
        _listFlow.ResumeLayout();
        if (_actions.Count > 0) Select(_actions[0]);
        else ClearForm();
    }

    /// <summary>
    /// Manually position every list item so they fill the panel width and stack vertically.
    /// </summary>
    private void RelayoutListItems()
    {
        if (_listFlow == null) return;
        int width = _listFlow.Width;
        if (width <= 0 && _listCard != null) width = _listCard.ClientSize.Width;
        if (width <= 0) width = 280;
        const int sidePadding = 18;
        const int topPadding = 35;
        const int gap = 10;
        const int itemHeight = 78;
        int innerWidth = width - sidePadding * 2;
        int y = topPadding;
        foreach (Control c in _listFlow.Controls)
        {
            c.SetBounds(sidePadding, y, innerWidth, itemHeight);
            c.Invalidate();
            y += itemHeight + gap;
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        // Belt-and-braces: re-layout list items whenever the pane resizes.
        RelayoutListItems();
    }

    private void Select(CustomAction a)
    {
        _selected = a;
        _nameBox.Text = a.Name;
        _promptBox.Text = a.Prompt;
        foreach (Control c in _listFlow.Controls)
            if (c is CustomActionListItem item)
                item.Active = item.ActionId == a.Id;
    }

    private void ClearForm()
    {
        _selected = null;
        _nameBox.Text = string.Empty;
        _promptBox.Text = string.Empty;
    }

    private void StartNew()
    {
        var n = new CustomAction { Name = "New Action", Prompt = "Process the following text: {text}" };
        ConfigManager.SaveCustomAction(n);
        ReloadList();
        _selected = ConfigManager.GetCustomActions().LastOrDefault();
        if (_selected != null) Select(_selected);
        ActionsChanged?.Invoke(this, EventArgs.Empty);
        _nameBox.Focus();
        _nameBox.SelectAll();
    }

    private void SaveSelected()
    {
        if (_selected == null)
        {
            // Create new
            var n = new CustomAction { Name = _nameBox.Text, Prompt = _promptBox.Text };
            if (string.IsNullOrWhiteSpace(n.Name))
            {
                MessageBox.Show("Please enter a name.", "AIPaste", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ConfigManager.SaveCustomAction(n);
        }
        else
        {
            _selected.Name = _nameBox.Text;
            _selected.Prompt = _promptBox.Text;
            ConfigManager.SaveCustomAction(_selected);
        }
        ReloadList();
        ActionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DeleteSelected()
    {
        if (_selected == null) return;
        if (MessageBox.Show($"Delete \"{_selected.Name}\"?", "Confirm",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        ConfigManager.DeleteCustomAction(_selected.Id);
        ReloadList();
        ActionsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ============== List item ==============
    private sealed class CustomActionListItem : Control
    {
        public string ActionId { get; }
        private readonly string _name;
        private readonly string _preview;
        private bool _active;
        private bool _hovered;

        public bool Active { get => _active; set { _active = value; Invalidate(); } }

        public CustomActionListItem(CustomAction action)
        {
            ActionId = action.Id;
            _name = action.Name;
            _preview = action.Prompt.Length > 64 ? action.Prompt[..64] + "…" : action.Prompt;
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
            Height = 52;
            Cursor = Cursors.Hand;
            Margin = new Padding(0, 0, 0, 4);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Active row gets a distinctly brighter background.
            Color background = _active
                ? Color.FromArgb(60, 45, 80)
                : (_hovered ? Theme.Surface3 : Theme.Surface2);
            using (var bg = new SolidBrush(background))
                g.FillRectangle(bg, ClientRectangle);

            if (_active)
            {
                using var pen = new Pen(Theme.Accent, 3);
                g.DrawLine(pen, 1, 0, 1, Height);
            }

            // Icon
            var icoRect = new Rectangle(_active ? 12 : 10, (Height - 28) / 2, 28, 28);
            if (_active)
                g.FillGradientRounded(Theme.Accent, Theme.Accent2, icoRect, 7, 135f);
            else
                g.FillRounded(Theme.Surface3, icoRect, 7);
            string letter = _name.Length > 0 ? _name.Substring(0, 1).ToUpper() : "?";
            using var icoFont = Theme.BodyBold();
            TextRenderer.DrawText(g, letter, icoFont, icoRect, _active ? Theme.AccentInk : Theme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            // Name + preview
            int xText = icoRect.Right + 10;
            int textWidth = Math.Max(0, Width - xText - 8);
            using var nameFont = Theme.BodyBold();
            TextRenderer.DrawText(g, _name, nameFont, new Rectangle(xText, 8, textWidth, 18), Theme.Text,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            using var prevFont = Theme.Small();
            TextRenderer.DrawText(g, _preview, prevFont, new Rectangle(xText, 28, textWidth, 18), Theme.TextMuted,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }
}
