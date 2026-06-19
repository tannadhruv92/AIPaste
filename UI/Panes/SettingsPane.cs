using System.Drawing;
using System.Windows.Forms;
using AIPaste.UI.Controls;

namespace AIPaste.UI.Panes;

/// <summary>
/// Settings pane: provider cards (Copilot / Azure OpenAI), auth status,
/// CLI command, default model, save/cancel.
/// </summary>
public class SettingsPane : UserControl
{
    private readonly Action _onClose;

    private ProviderCard _copilotCard = null!;
    private ProviderCard _azureCard = null!;
    private SurfaceCard _authCard = null!;
    private StatusPill _authStatus = null!;
    private Label _authHint = null!;
    private TextBox _cliCmdBox = null!;
    private Button _copyCmdBtn = null!;
    private Button _recheckBtn = null!;
    private Label _cliSourceLabel = null!;
    private SurfaceCard _modelCard = null!;
    private ComboBox _modelCombo = null!;
    private SurfaceCard _customCard = null!;
    private TextBox _apiKeyBox = null!;
    private TextBox _endpointBox = null!;
    private TextBox _deploymentBox = null!;

    private AIProvider _selectedProvider;

    private Button _themeSystemBtn = null!;
    private Button _themeLightBtn = null!;
    private Button _themeDarkBtn = null!;

    public event EventHandler? Saved;
    public event EventHandler? AuthChanged;
    public event EventHandler? ThemeChanged;

    public SettingsPane(Action onClose)
    {
        _onClose = onClose;
        BackColor = Theme.Surface;
        Padding = new Padding(20);
        AutoScroll = true;

        BuildLayout();
        Load += async (_, _) => await LoadAsync();
    }

    private void BuildLayout()
    {
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };

        // Appearance / theme switcher
        stack.Controls.Add(MakeFieldLabel("Appearance"));
        var themeRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent,
            Width = 600,
            Margin = new Padding(0, 4, 0, 18),
            Padding = new Padding(0),
        };
        _themeSystemBtn = MakeThemeOption("🖥  System", ThemeMode.System);
        _themeLightBtn = MakeThemeOption("☀  Light", ThemeMode.Light);
        _themeDarkBtn = MakeThemeOption("🌙  Dark", ThemeMode.Dark);
        themeRow.Controls.Add(_themeSystemBtn);
        themeRow.Controls.Add(_themeLightBtn);
        themeRow.Controls.Add(_themeDarkBtn);
        stack.Controls.Add(themeRow);
        RefreshThemeButtons(ConfigManager.GetThemeMode());

        // Provider cards
        stack.Controls.Add(MakeFieldLabel("AI Provider"));
        var providerRow = new Panel
        {
            BackColor = Color.Transparent,
            AutoSize = false,
            Height = 90,
            Width = 600,
            Margin = new Padding(0, 4, 0, 18),
        };
        _copilotCard = new ProviderCard("⚡", "GitHub Copilot", "Uses your GitHub subscription — sign in once", true);
        _copilotCard.Location = new Point(0, 0);
        _copilotCard.Size = new Size(290, 86);
        _copilotCard.Click += (_, _) => SelectProvider(AIProvider.GitHubCopilot);
        _azureCard = new ProviderCard("☁", "Azure OpenAI", "Bring your own endpoint & API key", false);
        _azureCard.Location = new Point(298, 0);
        _azureCard.Size = new Size(290, 86);
        _azureCard.Click += (_, _) => SelectProvider(AIProvider.Custom);
        providerRow.Controls.Add(_copilotCard);
        providerRow.Controls.Add(_azureCard);
        stack.Controls.Add(providerRow);

        // Auth card (Copilot only)
        _authCard = new SurfaceCard
        {
            FillColor = Theme.Surface2,
            BorderColor = Theme.Border,
            CornerRadius = Theme.CornerRadiusLg,
            Width = 600,
            Height = 150,
            Margin = new Padding(0, 0, 0, 16),
            Padding = new Padding(16),
        };
        var authTitle = new Label
        {
            Text = "Authentication",
            ForeColor = Theme.Text,
            Font = Theme.BodyBold(),
            AutoSize = true,
            Location = new Point(16, 14),
            BackColor = Color.Transparent,
        };
        _authHint = new Label
        {
            Text = "Run copilot in a terminal and type /login to authenticate.",
            ForeColor = Theme.TextDim,
            Font = Theme.Small(),
            AutoSize = true,
            Location = new Point(16, 38),
            MaximumSize = new Size(560, 0),
            BackColor = Color.Transparent,
        };
        _authStatus = new StatusPill { Location = new Point(460, 14) };
        _cliCmdBox = new TextBox
        {
            Text = "copilot",
            ReadOnly = true,
            BackColor = Theme.Surface3,
            ForeColor = Theme.Success,
            Font = Theme.Mono(),
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(16, 66),
            Size = new Size(380, 28),
        };
        _copyCmdBtn = MakeMiniBtn("📋  Copy", () =>
        {
            Clipboard.SetText("copilot");
            _authStatus.Set("Copied", StatusKind.Pending);
        });
        _copyCmdBtn.Location = new Point(404, 66);
        _recheckBtn = MakeAccentBtn("↻  Re-check", async () => await CheckAuthAsync());
        _recheckBtn.Location = new Point(484, 66);
        _cliSourceLabel = new Label
        {
            Text = "CLI: (not yet connected)",
            ForeColor = Theme.TextDim,
            Font = Theme.Small(),
            AutoSize = true,
            Location = new Point(16, 104),
            MaximumSize = new Size(560, 0),
            BackColor = Color.Transparent,
        };
        _authCard.Controls.Add(authTitle);
        _authCard.Controls.Add(_authHint);
        _authCard.Controls.Add(_authStatus);
        _authCard.Controls.Add(_cliCmdBox);
        _authCard.Controls.Add(_copyCmdBtn);
        _authCard.Controls.Add(_recheckBtn);
        _authCard.Controls.Add(_cliSourceLabel);
        stack.Controls.Add(_authCard);

        // Model picker (Copilot only)
        _modelCard = new SurfaceCard
        {
            FillColor = Theme.Surface2,
            BorderColor = Theme.Border,
            CornerRadius = Theme.CornerRadiusLg,
            Width = 600,
            Height = 92,
            Margin = new Padding(0, 0, 0, 16),
            Padding = new Padding(16),
        };
        var modelLbl = new Label
        {
            Text = "Default Model",
            ForeColor = Theme.Text,
            Font = Theme.BodyBold(),
            AutoSize = true,
            Location = new Point(16, 12),
            BackColor = Color.Transparent,
        };
        _modelCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.Surface3,
            ForeColor = Theme.Text,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(16, 36),
            Size = new Size(560, 28),
            Font = Theme.Body(),
        };
        var modelHint = new Label
        {
            Text = "Default for new requests. Override per-request via the model pill in the popup.",
            ForeColor = Theme.TextMuted,
            Font = Theme.Small(),
            AutoSize = true,
            Location = new Point(16, 70),
            BackColor = Color.Transparent,
        };
        _modelCard.Controls.Add(modelLbl);
        _modelCard.Controls.Add(_modelCombo);
        _modelCard.Controls.Add(modelHint);
        stack.Controls.Add(_modelCard);

        // Custom provider card (Azure)
        _customCard = new SurfaceCard
        {
            FillColor = Theme.Surface2,
            BorderColor = Theme.Border,
            CornerRadius = Theme.CornerRadiusLg,
            Width = 600,
            Height = 230,
            Margin = new Padding(0, 0, 0, 16),
            Padding = new Padding(16),
            Visible = false,
        };
        AddField(_customCard, "API Key", out _apiKeyBox, 0, password: true);
        AddField(_customCard, "Endpoint URL", out _endpointBox, 70);
        AddField(_customCard, "Deployment ID / Model", out _deploymentBox, 140);
        stack.Controls.Add(_customCard);

        // Footer
        var footer = new Panel
        {
            Width = 600,
            Height = 50,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 0),
        };
        var cancel = MakeGhostBtn("Cancel", _onClose);
        cancel.Location = new Point(420, 12);
        var save = MakeAccentBtn("Save", OnSave);
        save.Location = new Point(498, 12);
        footer.Controls.Add(cancel);
        footer.Controls.Add(save);
        stack.Controls.Add(footer);

        Controls.Add(stack);
    }

    private void AddField(SurfaceCard host, string label, out TextBox tb, int yOffset, bool password = false)
    {
        var lbl = new Label
        {
            Text = label,
            ForeColor = Theme.TextDim,
            Font = Theme.Small(),
            AutoSize = true,
            Location = new Point(16, 14 + yOffset),
            BackColor = Color.Transparent,
        };
        tb = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Surface3,
            ForeColor = Theme.Text,
            Font = Theme.Body(),
            Location = new Point(16, 36 + yOffset),
            Size = new Size(560, 28),
            UseSystemPasswordChar = password,
        };
        host.Controls.Add(lbl);
        host.Controls.Add(tb);
    }

    private Label MakeFieldLabel(string text) => new()
    {
        Text = text,
        ForeColor = Theme.TextDim,
        Font = Theme.Small(),
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 6),
        BackColor = Color.Transparent,
    };

    private Button MakeThemeOption(string text, ThemeMode mode)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            Font = Theme.Body(),
            AutoSize = false,
            Size = new Size(128, 38),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 8, 0),
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = 1;
        b.Click += (_, _) => OnThemeOptionClicked(mode);
        return b;
    }

    /// <summary>Repaints the three theme options so the active one reads from the live Theme tokens.</summary>
    private void RefreshThemeButtons(ThemeMode active)
    {
        StyleThemeOption(_themeSystemBtn, "🖥  System", ThemeMode.System, active);
        StyleThemeOption(_themeLightBtn, "☀  Light", ThemeMode.Light, active);
        StyleThemeOption(_themeDarkBtn, "🌙  Dark", ThemeMode.Dark, active);
    }

    private static void StyleThemeOption(Button b, string label, ThemeMode mode, ThemeMode active)
    {
        bool on = mode == active;
        b.Text = on ? "✓  " + label : label;
        b.Font = on ? Theme.BodyBold() : Theme.Body();
        b.BackColor = on ? Theme.AccentSelected : Theme.Surface3;
        b.ForeColor = on ? Theme.Accent : Theme.TextDim;
        b.FlatAppearance.BorderColor = on ? Theme.Accent : Theme.Border;
    }

    private void OnThemeOptionClicked(ThemeMode mode)
    {
        if (ConfigManager.GetThemeMode() == mode) return;
        ConfigManager.SetThemeMode(mode);
        AIPaste.UI.Theme.ApplyMode(mode);
        RefreshThemeButtons(mode);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private Button MakeMiniBtn(string text, Action onClick)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface3,
            ForeColor = Theme.Text,
            Font = Theme.Small(),
            Size = new Size(76, 28),
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderColor = Theme.Border;
        b.FlatAppearance.BorderSize = 1;
        b.Click += (_, _) => onClick();
        return b;
    }

    private Button MakeGhostBtn(string text, Action onClick)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextDim,
            Font = Theme.Body(),
            Size = new Size(72, 30),
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize = 0;
        b.MouseEnter += (_, _) => b.ForeColor = Theme.Text;
        b.MouseLeave += (_, _) => b.ForeColor = Theme.TextDim;
        b.Click += (_, _) => onClick();
        return b;
    }

    private Button MakeAccentBtn(string text, Action onClick)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Accent,
            ForeColor = Theme.AccentInk,
            Font = Theme.BodyBold(),
            Size = new Size(96, 30),
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Theme.Accent2;
        b.Click += (_, _) => onClick();
        return b;
    }

    // ============== Logic ==============
    private async Task LoadAsync()
    {
        var p = ConfigManager.GetProvider();
        SelectProvider(p == AIProvider.NotConfigured ? AIProvider.GitHubCopilot : p);

        if (!string.IsNullOrEmpty(ConfigManager.GetCustomApiKey()))
            _apiKeyBox.Text = "•••••••••••••••••••";
        _endpointBox.Text = ConfigManager.GetCustomEndpoint();
        _deploymentBox.Text = ConfigManager.GetCustomDeploymentId();

        if (_selectedProvider == AIProvider.GitHubCopilot)
            await CheckAuthAsync();
    }

    private void SelectProvider(AIProvider provider)
    {
        _selectedProvider = provider;
        _copilotCard.Selected = provider == AIProvider.GitHubCopilot;
        _azureCard.Selected = provider == AIProvider.Custom;
        bool isCopilot = provider == AIProvider.GitHubCopilot;
        _authCard.Visible = isCopilot;
        _modelCard.Visible = isCopilot;
        _customCard.Visible = !isCopilot;
    }

    private async Task CheckAuthAsync()
    {
        _authStatus.Set("Checking…", StatusKind.Pending);
        _recheckBtn.Enabled = false;
        try
        {
            var (ok, msg) = await ConfigManager.CheckCopilotAuthAsync();
            if (ok)
            {
                _authHint.Text = "Connected to GitHub Copilot via the CLI.";
                _authStatus.Set("Authenticated", StatusKind.Success);
                UpdateCliSourceLabel();
                await LoadModelsAsync();
            }
            else
            {
                _authHint.Text = msg;
                _authStatus.Set("Not signed in", StatusKind.Danger);
                _modelCombo.Items.Clear();
            }
        }
        catch (Exception ex)
        {
            _authHint.Text = $"Error: {ex.Message}";
            _authStatus.Set("Error", StatusKind.Danger);
        }
        finally
        {
            _recheckBtn.Enabled = true;
            AuthChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateCliSourceLabel()
    {
        var mgr = CopilotClientManager.Instance;
        if (mgr.IsUsingBundledCli)
            _cliSourceLabel.Text = "CLI: bundled fallback — install/update the CLI for the latest models";
        else if (!string.IsNullOrEmpty(mgr.ActiveCliPath))
            _cliSourceLabel.Text = $"CLI: {mgr.ActiveCliPath}";
        else
            _cliSourceLabel.Text = "CLI: system";
    }

    private async Task LoadModelsAsync()
    {
        try
        {
            var models = await ConfigManager.GetCopilotModelsAsync();
            _modelCombo.Items.Clear();
            if (models == null) return;
            foreach (var m in models.OrderBy(x => x.Name))
                _modelCombo.Items.Add(m.Id);
            string preferred = ConfigManager.GetCopilotPreferredModel();
            int idx = _modelCombo.Items.IndexOf(preferred);
            if (idx >= 0) _modelCombo.SelectedIndex = idx;
            else if (_modelCombo.Items.Count > 0) _modelCombo.SelectedIndex = 0;
        }
        catch { }
    }

    private void OnSave()
    {
        ConfigManager.SetProvider(_selectedProvider);
        if (_selectedProvider == AIProvider.GitHubCopilot)
        {
            if (_modelCombo.SelectedItem != null)
                ConfigManager.SetCopilotPreferredModel(_modelCombo.SelectedItem.ToString() ?? "gpt-4o");
        }
        else
        {
            if (_apiKeyBox.Text != "•••••••••••••••••••" && !string.IsNullOrWhiteSpace(_apiKeyBox.Text))
                ConfigManager.SetCustomApiKey(_apiKeyBox.Text);
            ConfigManager.SetCustomEndpoint(_endpointBox.Text);
            ConfigManager.SetCustomDeploymentId(_deploymentBox.Text);
        }
        Saved?.Invoke(this, EventArgs.Empty);
        _onClose();
    }

    // ============== ProviderCard control ==============
    private sealed class ProviderCard : SurfaceCard
    {
        private bool _selected;
        private readonly string _glyph;
        private readonly string _name;
        private readonly string _desc;

        public ProviderCard(string glyph, string name, string desc, bool selected)
        {
            _glyph = glyph; _name = name; _desc = desc; _selected = selected;
            FillColor = Theme.Surface2;
            BorderColor = Theme.Border;
            CornerRadius = 10;
            Cursor = Cursors.Hand;
        }
        public bool Selected
        {
            get => _selected;
            set { _selected = value; BorderColor = value ? Theme.Accent : Theme.Border; FillColor = value ? Theme.AccentSelected : Theme.Surface2; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Icon badge
            var iconRect = new Rectangle(14, 14, 30, 30);
            g.FillGradientRounded(Theme.Accent, Theme.Accent2, iconRect, 7, 135f);
            using var iconFont = new Font(Theme.FontFamily, 13f, FontStyle.Bold);
            TextRenderer.DrawText(g, _glyph, iconFont, iconRect, Theme.AccentInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            // Name
            using var nameFont = Theme.BodyBold();
            TextRenderer.DrawText(g, _name, nameFont, new Point(54, 14), Theme.Text, TextFormatFlags.NoPadding);
            // Desc
            using var descFont = Theme.Small();
            TextRenderer.DrawText(g, _desc, descFont, new Rectangle(54, 36, Width - 60, 40), Theme.TextDim,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

            if (_selected)
            {
                var dot = new Rectangle(Width - 26, 10, 16, 16);
                using var b = new SolidBrush(Theme.Accent);
                g.FillEllipse(b, dot);
                using var f = new Font(Theme.FontFamily, 9f, FontStyle.Bold);
                TextRenderer.DrawText(g, "✓", f, dot, Theme.AccentInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    private enum StatusKind { Pending, Success, Danger }

    private sealed class StatusPill : Label
    {
        public StatusPill()
        {
            AutoSize = false;
            Size = new Size(120, 24);
            Font = Theme.SmallBold();
            BackColor = Color.Transparent;
            ForeColor = Theme.TextMuted;
            TextAlign = ContentAlignment.MiddleCenter;
            Text = "Not checked";
        }
        public void Set(string text, StatusKind kind)
        {
            Text = text;
            ForeColor = kind switch
            {
                StatusKind.Success => Theme.Success,
                StatusKind.Danger => Theme.Danger,
                _ => Theme.TextMuted,
            };
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = ForeColor switch
            {
                _ when ForeColor == Theme.Success => Color.FromArgb(40, Theme.Success),
                _ when ForeColor == Theme.Danger => Color.FromArgb(40, Theme.Danger),
                _ => Color.FromArgb(40, Theme.TextMuted),
            };
            g.FillRounded(fill, rect, rect.Height / 2);
            g.DrawRoundedBorder(Color.FromArgb(80, ForeColor), rect, rect.Height / 2);

            // Tiny dot
            var dot = new Rectangle(10, (Height - 7) / 2, 7, 7);
            using var b = new SolidBrush(ForeColor);
            g.FillEllipse(b, dot);

            // Text
            TextRenderer.DrawText(g, Text, Font, new Rectangle(20, 0, Width - 24, Height), ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        }
    }
}
