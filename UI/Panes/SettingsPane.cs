using System.Drawing;
using System.Windows.Forms;
using AIPaste.UI.Controls;
using GitHub.Copilot.SDK;

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
    private TextBox _cliCmd = null!;
    private Label _cliSourceLabel = null!;
    private ToolTip _cliSourceTooltip = null!;
    private SurfaceCard _modelCard = null!;
    private ComboBox _modelCombo = null!;
    private SurfaceCard _customCard = null!;
    private TextBox _apiKeyBox = null!;
    private TextBox _endpointBox = null!;
    private TextBox _deploymentBox = null!;

    private AIProvider _selectedProvider;

    public event EventHandler? Saved;

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
        _copilotCard = new ProviderCard("⚡", "GitHub Copilot", "Uses your GitHub subscription via CLI auth", true);
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
            Height = 140,
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
        var authSub = new Label
        {
            Text = "Run copilot then type /login",
            ForeColor = Theme.TextDim,
            Font = Theme.Small(),
            AutoSize = true,
            Location = new Point(16, 34),
            BackColor = Color.Transparent,
        };
        _authStatus = new StatusPill { Location = new Point(460, 14) };
        _cliCmd = new TextBox
        {
            Text = "copilot",
            ReadOnly = true,
            BackColor = Color.FromArgb(10, 10, 12),
            ForeColor = Theme.Success,
            Font = Theme.Mono(),
            BorderStyle = BorderStyle.None,
            Location = new Point(16, 64),
            Size = new Size(380, 28),
        };
        var copyBtn = MakeMiniBtn("📋  Copy", () =>
        {
            Clipboard.SetText("copilot");
            _authStatus.Text = "Copied";
        });
        copyBtn.Location = new Point(404, 62);
        var recheckBtn = MakeMiniBtn("↻  Re-check", async () => await CheckAuthAsync());
        recheckBtn.Location = new Point(484, 62);
        _cliSourceLabel = new Label
        {
            Text = "CLI: (not yet connected)",
            ForeColor = Theme.TextDim,
            Font = Theme.Small(),
            AutoSize = true,
            Location = new Point(16, 104),
            BackColor = Color.Transparent,
        };
        _cliSourceTooltip = new ToolTip { AutoPopDelay = 10000, InitialDelay = 300 };
        _authCard.Controls.Add(authTitle);
        _authCard.Controls.Add(authSub);
        _authCard.Controls.Add(_authStatus);
        _authCard.Controls.Add(_cliCmd);
        _authCard.Controls.Add(copyBtn);
        _authCard.Controls.Add(recheckBtn);
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
        try
        {
            var (ok, msg) = await ConfigManager.CheckCopilotAuthAsync();
            if (ok)
            {
                _authStatus.Set("Authenticated", StatusKind.Success);
                await LoadModelsAsync();
            }
            else
            {
                _authStatus.Set("Not signed in", StatusKind.Danger);
                _modelCombo.Items.Clear();
            }
        }
        catch
        {
            _authStatus.Set("Error", StatusKind.Danger);
        }
        UpdateCliSourceLabel();
    }

    private void UpdateCliSourceLabel()
    {
        var path = CopilotClientManager.Instance.ActiveCliPath;
        if (path != null)
        {
            // System CLI — show short marker and origin (winget / npm / PATH).
            _cliSourceLabel.Text = $"● CLI: System  ({DescribeCliOrigin(path)})";
            _cliSourceLabel.ForeColor = Theme.Success;
            _cliSourceTooltip.SetToolTip(_cliSourceLabel, path);
        }
        else if (CopilotClientManager.Instance.IsUsingBundledCli)
        {
            _cliSourceLabel.Text = "● CLI: Bundled  (install Copilot CLI for latest models)";
            _cliSourceLabel.ForeColor = Theme.TextDim;
            _cliSourceTooltip.SetToolTip(_cliSourceLabel,
                "Using runtimes\\win-x64\\native\\copilot.exe shipped with the SDK.\n" +
                "Run `winget install GitHub.Copilot` or `npm i -g @github/copilot` to use the latest CLI.");
        }
        else
        {
            _cliSourceLabel.Text = "CLI: (not yet connected)";
            _cliSourceLabel.ForeColor = Theme.TextDim;
            _cliSourceTooltip.SetToolTip(_cliSourceLabel, string.Empty);
        }
    }

    private static string DescribeCliOrigin(string path)
    {
        if (path.Contains(@"\WinGet\Packages\", StringComparison.OrdinalIgnoreCase)) return "winget";
        if (path.Contains(@"\npm\node_modules\", StringComparison.OrdinalIgnoreCase)) return "npm";
        return "PATH";
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
            set { _selected = value; BorderColor = value ? Theme.Accent : Theme.Border; FillColor = value ? Color.FromArgb(255, 30, 25, 50) : Theme.Surface2; Invalidate(); }
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
