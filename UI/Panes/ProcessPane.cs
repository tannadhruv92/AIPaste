using System.Drawing;
using System.Windows.Forms;
using AIPaste.UI.Controls;
using Azure;
using Azure.AI.OpenAI;
using GitHub.Copilot.SDK;

namespace AIPaste.UI.Panes;

public enum ProcessMode { Rewrite, Translate, Custom }

/// <summary>
/// The default pane: shows mode chips, tone/language/action chips,
/// the original text, the split action button and the AI result.
/// </summary>
public class ProcessPane : UserControl
{
    private readonly Action<string> _setStatusModel;
    private readonly Action<string> _setStatusModeChip;
    private readonly Action<string> _setStatusHint;
    private readonly Action _onRequestManageActions;
    private readonly Action _onRequestSettings;

    // Layout cards
    private SurfaceCard _toolbarCard = null!;
    private FlowLayoutPanel _toolbarFlow = null!;
    private ChipGroup _modeGroup = null!;
    private ChipGroup _toneGroup = null!;
    private ChipGroup _languageGroup = null!;
    private ChipGroup _actionGroup = null!;

    private SurfaceCard _originalCard = null!;
    private TextBox _originalBox = null!;
    private Label _charCount = null!;

    private SurfaceCard _actionBar = null!;
    private SplitActionButton _splitBtn = null!;

    private SurfaceCard _resultCard = null!;
    private TextBox _resultBox = null!;
    private Label _resultLive = null!;
    private FlowLayoutPanel _resultTools = null!;

    private string _originalText = string.Empty;
    private string _processedText = string.Empty;
    private ProcessMode _currentMode = ProcessMode.Rewrite;

    public string ProcessedText => _processedText;
    public event EventHandler<string>? AcceptedAndCopied;

    public ProcessPane(string originalText,
        Action<string> setStatusModel,
        Action<string> setStatusModeChip,
        Action<string> setStatusHint,
        Action onRequestManageActions,
        Action onRequestSettings)
    {
        _originalText = originalText ?? string.Empty;
        _setStatusModel = setStatusModel;
        _setStatusModeChip = setStatusModeChip;
        _setStatusHint = setStatusHint;
        _onRequestManageActions = onRequestManageActions;
        _onRequestSettings = onRequestSettings;

        BackColor = Theme.Surface;
        Padding = new Padding(18);
        BuildLayout();
        ApplyMode(ProcessMode.Rewrite);
        UpdateOriginal(_originalText);
        // Fire & forget: load models when handle is created
        HandleCreated += async (_, _) =>
        {
            await LoadModelsAsync();
            // Pre-warm a session for snappy first request (Rewrite default)
            PreWarmDefaultSession();
        };
    }

    public void UpdateOriginal(string text)
    {
        _originalText = text ?? string.Empty;
        _originalBox.Text = NormalizeLineEndings(_originalText);
        _charCount.Text = $"{_originalText.Length} chars";
    }

    private void BuildLayout()
    {
        SuspendLayout();

        // ===== TOOLBAR =====
        _toolbarCard = new SurfaceCard
        {
            FillColor = Theme.Surface3,
            FillColor2 = Theme.Surface2,
            BorderColor = Theme.Border,
            CornerRadius = Theme.CornerRadiusLg,
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(14, 12, 14, 12),
        };

        _toolbarFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        // Adds vertical breathing room between wrapped chip rows.
        _toolbarFlow.Layout += (_, _) => UpdateToolbarHeight();

        _modeGroup = new ChipGroup("Mode");
        _modeGroup.AddChip("Rewrite", "✨", active: true);
        _modeGroup.AddChip("Translate", "🌐");
        _modeGroup.AddChip("Custom", "⚡");
        _modeGroup.SelectionChanged += OnModeChanged;
        _modeGroup.Margin = new Padding(0, 0, 14, 8);

        _toneGroup = new ChipGroup("Tone");
        _toneGroup.AddChip("Professional", active: true);
        _toneGroup.AddChip("Casual");
        _toneGroup.AddChip("Informative");
        _toneGroup.AddChip("Enthusiastic");
        _toneGroup.SelectionChanged += (_, _) => UpdateStatusModeChip();
        _toneGroup.Margin = new Padding(0, 0, 14, 8);

        _languageGroup = new ChipGroup("Language");
        _languageGroup.AddChip("Hindi", "🇮🇳", active: true);
        _languageGroup.AddChip("Gujarati", "🇮🇳");
        _languageGroup.SelectionChanged += (_, _) => UpdateStatusModeChip();
        _languageGroup.Margin = new Padding(0, 0, 14, 8);

        _actionGroup = new ChipGroup("Action");
        BuildCustomActionChips();
        _actionGroup.DashedClicked += (_, _) => _onRequestManageActions();
        _actionGroup.SelectionChanged += (_, _) => UpdateStatusModeChip();
        _actionGroup.Margin = new Padding(0, 0, 14, 8);

        _toolbarFlow.Controls.AddRange(new Control[] { _modeGroup, _toneGroup, _languageGroup, _actionGroup });
        _toolbarCard.Controls.Add(_toolbarFlow);

        // ===== ORIGINAL CARD =====
        _originalCard = new SurfaceCard
        {
            FillColor = Theme.Surface2,
            BorderColor = Theme.Border,
            CornerRadius = Theme.CornerRadiusLg,
            Dock = DockStyle.Top,
            Height = 130,
            Padding = new Padding(0),
            Margin = new Padding(0, 12, 0, 0),
        };
        var origHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.Transparent,
            Padding = new Padding(14, 0, 14, 0),
        };
        var origLabel = new Label
        {
            Text = "📋  Original",
            ForeColor = Theme.TextDim,
            Font = Theme.SmallBold(),
            AutoSize = true,
            Location = new Point(14, 9),
            BackColor = Color.Transparent,
        };
        _charCount = new Label
        {
            Text = "0 chars",
            ForeColor = Theme.TextMuted,
            Font = Theme.Small(),
            AutoSize = true,
            BackColor = Color.Transparent,
        };
        origHeader.Controls.Add(origLabel);
        origHeader.Controls.Add(_charCount);
        // Pin _charCount to the right edge of the header.
        void PositionCharCount()
        {
            if (_charCount.IsDisposed) return;
            _charCount.Location = new Point(origHeader.ClientSize.Width - _charCount.Width - 14, 9);
        }
        origHeader.SizeChanged += (_, _) => PositionCharCount();
        _charCount.SizeChanged += (_, _) => PositionCharCount();
        var origDiv = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border };

        _originalBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface2,
            ForeColor = Theme.Text,
            Font = new Font(Theme.FontFamilyFallback, 10.5f),
            Dock = DockStyle.Fill,
        };
        var origPad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 8, 14, 12), BackColor = Color.Transparent };
        origPad.Controls.Add(_originalBox);

        _originalCard.Controls.Add(origPad);
        _originalCard.Controls.Add(origDiv);
        _originalCard.Controls.Add(origHeader);

        // ===== ACTION BAR =====
        _actionBar = new SurfaceCard
        {
            FillColor = Theme.Surface2,
            FillColor2 = Theme.Surface3,
            BorderColor = Theme.Border,
            CornerRadius = Theme.CornerRadiusLg,
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 12, 0, 0),
        };
        _splitBtn = new SplitActionButton
        {
            ActionLabel = "Process",
            ModelName = "claude-opus-4.6-1m",
        };
        _splitBtn.ModelClicked += OnModelPickerClicked;
        _splitBtn.ActionClicked += OnProcessClicked;
        _splitBtn.SizeChanged += (_, _) => PositionSplitButton();
        _actionBar.Controls.Add(_splitBtn);
        _actionBar.SizeChanged += (_, _) => PositionSplitButton();

        // ===== RESULT CARD =====
        _resultCard = new SurfaceCard
        {
            FillColor = Theme.Surface2,
            BorderColor = Theme.Border,
            CornerRadius = Theme.CornerRadiusLg,
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            Margin = new Padding(0, 12, 0, 0),
            MinimumSize = new Size(0, 200),
        };
        var resHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.Transparent,
        };
        var resLabel = new Label
        {
            Text = "✨  AI Result",
            ForeColor = Theme.TextDim,
            Font = Theme.SmallBold(),
            AutoSize = true,
            Location = new Point(14, 9),
            BackColor = Color.Transparent,
        };
        _resultLive = new Label
        {
            Text = "",
            ForeColor = Theme.Success,
            Font = Theme.Small(),
            AutoSize = true,
            BackColor = Color.Transparent,
            Visible = false,
        };
        resHeader.Controls.Add(resLabel);
        resHeader.Controls.Add(_resultLive);
        // Pin _resultLive to the right edge of the header (Anchor doesn't play
        // well with AutoSize labels, so reposition manually).
        void PositionLive()
        {
            if (_resultLive.IsDisposed) return;
            _resultLive.Location = new Point(resHeader.ClientSize.Width - _resultLive.Width - 14, 9);
        }
        resHeader.SizeChanged += (_, _) => PositionLive();
        _resultLive.SizeChanged += (_, _) => PositionLive();
        _resultLive.VisibleChanged += (_, _) => PositionLive();
        var resDiv = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border };

        _resultBox = new TextBox
        {
            Multiline = true,
            WordWrap = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface2,
            ForeColor = Theme.Text,
            ReadOnly = true,
            AcceptsReturn = true,
            Font = new Font(Theme.FontFamilyFallback, 10.5f),
            Dock = DockStyle.Fill,
        };
        var resPad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 8, 14, 8), BackColor = Color.Transparent };
        resPad.Controls.Add(_resultBox);

        _resultTools = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            BackColor = Theme.Surface3,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0),
            Visible = false, // not used; replaced by _resultToolsPanel
        };
        var divBeforeTools = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border };

        // Regular panel where we can dock children left/right reliably.
        var resultToolsPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            BackColor = Theme.Surface3,
            Padding = new Padding(8, 6, 8, 6),
        };

        var regenBtn = MakeToolBtn("↻  Regenerate", async () => await ExecuteCurrentRequestAsync(true));
        regenBtn.Dock = DockStyle.Left;
        regenBtn.AutoSize = true;
        regenBtn.Padding = new Padding(10, 6, 10, 6);

        var acceptBtn = MakeAccentBtn("✓  Accept & Copy", () =>
        {
            if (!string.IsNullOrEmpty(_processedText))
            {
                Clipboard.SetText(_processedText);
                AcceptedAndCopied?.Invoke(this, _processedText);
            }
        });
        acceptBtn.Dock = DockStyle.Right;
        acceptBtn.AutoSize = true;
        acceptBtn.Padding = new Padding(14, 6, 14, 6);

        // Add Right first so Left doesn't fill it.
        resultToolsPanel.Controls.Add(acceptBtn);
        resultToolsPanel.Controls.Add(regenBtn);

        _resultCard.Controls.Add(resPad);
        _resultCard.Controls.Add(divBeforeTools);
        _resultCard.Controls.Add(resultToolsPanel);
        _resultCard.Controls.Add(resDiv);
        _resultCard.Controls.Add(resHeader);

        // Add in z-order: result first (Fill), then bars on top
        Controls.Add(_resultCard);
        Controls.Add(_actionBar);
        Controls.Add(_originalCard);
        Controls.Add(_toolbarCard);
        ResumeLayout(true);
    }

    private void BuildCustomActionChips()
    {
        _actionGroup.ClearChips();
        var actions = ConfigManager.GetCustomActions();
        bool first = true;
        foreach (var a in actions)
        {
            _actionGroup.AddChip(a.Name, "📝", active: first);
            first = false;
        }
        _actionGroup.AddChip("Manage…", "⚙", dashed: true);
    }

    public void RefreshCustomActions()
    {
        BuildCustomActionChips();
    }

    private Control MakeToolBtn(string text, Action onClick)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface3,
            ForeColor = Theme.TextDim,
            Font = Theme.Small(),
            AutoSize = true,
            Margin = new Padding(0, 0, 4, 0),
            Padding = new Padding(8, 4, 8, 4),
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Theme.Surface;
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
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 4, 0),
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Theme.Accent2;
        b.Click += (_, _) => onClick();
        return b;
    }

    private void PositionSplitButton()
    {
        if (_splitBtn == null || _actionBar == null) return;
        _splitBtn.Top = (_actionBar.ClientSize.Height - _splitBtn.Height) / 2;
        _splitBtn.Left = _actionBar.ClientSize.Width - _splitBtn.Width - 12;
    }

    // ============== Mode handling ==============
    private void OnModeChanged(object? sender, EventArgs e)
    {
        var sel = _modeGroup.SelectedChip?.Text;
        if (sel == "Translate") ApplyMode(ProcessMode.Translate);
        else if (sel == "Custom") ApplyMode(ProcessMode.Custom);
        else ApplyMode(ProcessMode.Rewrite);
    }

    private void ApplyMode(ProcessMode mode)
    {
        _currentMode = mode;
        _toolbarFlow.SuspendLayout();
        _toneGroup.Visible = mode == ProcessMode.Rewrite || mode == ProcessMode.Translate;
        _languageGroup.Visible = mode == ProcessMode.Translate;
        _actionGroup.Visible = mode == ProcessMode.Custom;
        _toolbarFlow.ResumeLayout(true);
        UpdateToolbarHeight();

        _splitBtn.ActionLabel = mode switch
        {
            ProcessMode.Translate => "Translate",
            ProcessMode.Custom => "Process",
            _ => "Process"
        };
        UpdateStatusModeChip();
    }

    /// <summary>
    /// Manually compute the required toolbar height so wrapped chip rows are visible.
    /// FlowLayoutPanel doesn't expose its actual wrapped height through AutoSize +
    /// Dock=Top reliably, so we measure each visible row and add them up.
    /// </summary>
    private void UpdateToolbarHeight()
    {
        if (_toolbarFlow == null || _toolbarCard == null) return;
        if (_toolbarFlow.Width <= 0) return;

        int x = 0;
        int rowHeight = 0;
        int totalHeight = 0;
        int availableWidth = _toolbarFlow.ClientSize.Width;

        foreach (Control c in _toolbarFlow.Controls)
        {
            if (!c.Visible) continue;
            int w = c.Width + c.Margin.Horizontal;
            int h = c.Height + c.Margin.Vertical;
            if (x > 0 && x + w > availableWidth)
            {
                // Wrap to next row
                totalHeight += rowHeight;
                x = 0;
                rowHeight = 0;
            }
            x += w;
            rowHeight = Math.Max(rowHeight, h);
        }
        totalHeight += rowHeight;

        int target = totalHeight + _toolbarCard.Padding.Vertical;
        target = Math.Max(target, 56);
        if (_toolbarCard.Height != target)
            _toolbarCard.Height = target;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateToolbarHeight();
    }

    private void UpdateStatusModeChip()
    {
        switch (_currentMode)
        {
            case ProcessMode.Rewrite:
                _setStatusModeChip(_toneGroup.Selected);
                _setStatusHint("Press Enter to process");
                break;
            case ProcessMode.Translate:
                _setStatusModeChip($"{_languageGroup.Selected} · {_toneGroup.Selected}");
                _setStatusHint("Press Enter to translate");
                break;
            case ProcessMode.Custom:
                _setStatusModeChip(_actionGroup.Selected);
                _setStatusHint("Press Enter to run");
                break;
        }
    }

    // ============== Models ==============
    private async Task LoadModelsAsync()
    {
        try
        {
            if (ConfigManager.GetProvider() == AIProvider.GitHubCopilot)
            {
                var models = await ConfigManager.GetCopilotModelsAsync();
                if (models != null && models.Count > 0)
                {
                    string preferred = ConfigManager.GetCopilotPreferredModel();
                    var match = models.FirstOrDefault(m => m.Id == preferred) ?? models.First();
                    _splitBtn.ModelName = match.Id;
                    _setStatusModel(match.Id);
                    return;
                }
            }
            // Custom provider — show deployment id
            var dep = ConfigManager.GetCustomDeploymentId();
            if (!string.IsNullOrEmpty(dep))
            {
                _splitBtn.ModelName = dep;
                _setStatusModel(dep);
            }
        }
        catch
        {
            // Silent — status bar will show "(no model)"
        }
    }

    private void OnModelPickerClicked(object? sender, EventArgs e)
    {
        // Show a small popup menu of available models.
        if (ConfigManager.GetProvider() != AIProvider.GitHubCopilot) return;
        _ = ShowModelPickerAsync();
    }

    private async Task ShowModelPickerAsync()
    {
        IReadOnlyList<ModelInfo>? models = null;
        try { models = await ConfigManager.GetCopilotModelsAsync(); } catch { }
        if (models == null || models.Count == 0) return;

        var menu = new ContextMenuStrip
        {
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            ShowImageMargin = false,
            Renderer = new DarkMenuRenderer(),
        };
        foreach (var m in models.OrderBy(m => m.Name))
        {
            var item = new ToolStripMenuItem(m.Id) { Tag = m.Id, ForeColor = Theme.Text };
            if (m.Id == _splitBtn.ModelName) item.Checked = true;
            item.Click += (_, _) =>
            {
                _splitBtn.ModelName = m.Id;
                _setStatusModel(m.Id);
            };
            menu.Items.Add(item);
        }
        var screenPt = _splitBtn.PointToScreen(new Point(0, _splitBtn.Height + 4));
        menu.Show(screenPt);
    }

    // ============== Pre-warm + Process ==============
    private void PreWarmDefaultSession()
    {
        if (ConfigManager.GetProvider() != AIProvider.GitHubCopilot) return;
        if (string.IsNullOrEmpty(_splitBtn.ModelName)) return;
        try
        {
            CopilotClientManager.Instance.PreWarmSession(new SessionConfig
            {
                Model = _splitBtn.ModelName,
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Replace,
                    Content = "You are a helpful assistant that rewrites text based on the specified tone and translation requirements. Only return the rewritten text, nothing else."
                },
                AvailableTools = new List<string>(),
                OnPermissionRequest = PermissionHandler.ApproveAll,
                Streaming = true
            });
        }
        catch { /* swallow — pre-warm is best-effort */ }
    }

    private async void OnProcessClicked(object? sender, EventArgs e)
    {
        await ExecuteCurrentRequestAsync(false);
    }

    public async Task ExecuteCurrentRequestAsync(bool isRetry)
    {
        if (string.IsNullOrEmpty(_originalText)) return;
        if (!ConfigManager.IsConfigComplete())
        {
            _onRequestSettings();
            return;
        }
        _resultLive.Text = "Streaming";
        _resultLive.Visible = true;
        _resultBox.Text = isRetry ? string.Empty : "Processing…";

        try
        {
            string systemPrompt;
            string userPrompt;
            switch (_currentMode)
            {
                case ProcessMode.Rewrite:
                    systemPrompt = "You are a helpful assistant that rewrites text based on the specified tone. Only return the rewritten text, nothing else.";
                    userPrompt = $"Rewrite the following text in a {_toneGroup.Selected} tone:\n\n\"{_originalText}\"";
                    break;
                case ProcessMode.Translate:
                    systemPrompt = "You are a helpful assistant that translates text. Only return the translated text, nothing else.";
                    userPrompt = $"Translate the following text to {_languageGroup.Selected} in a {_toneGroup.Selected} tone:\n\n\"{_originalText}\"";
                    break;
                default:
                    var act = ConfigManager.GetCustomActions().FirstOrDefault(a => a.Name == _actionGroup.Selected);
                    if (act == null)
                    {
                        _resultBox.Text = "No custom action selected.";
                        _resultLive.Visible = false;
                        return;
                    }
                    systemPrompt = "You are a helpful assistant that processes text based on the given instructions. Only return the processed result, nothing else.";
                    userPrompt = act.Prompt.Contains("{text}")
                        ? act.Prompt.Replace("{text}", _originalText)
                        : $"{act.Prompt}\n\nText to process: \"{_originalText}\"";
                    break;
            }

            string result = ConfigManager.GetProvider() == AIProvider.GitHubCopilot
                ? await ExecuteCopilotAsync(systemPrompt, userPrompt)
                : ExecuteCustomProvider(systemPrompt, userPrompt);

            _processedText = result;
            _resultLive.Visible = false;
        }
        catch (Exception ex)
        {
            _resultBox.Text = $"Error: {ex.Message}";
            _processedText = string.Empty;
            _resultLive.Visible = false;
        }
    }

    private async Task<string> ExecuteCopilotAsync(string systemPrompt, string userPrompt)
    {
        _resultBox.Text = string.Empty;
        await using var session = await CopilotClientManager.Instance.CreateSessionAsync(new SessionConfig
        {
            Model = _splitBtn.ModelName,
            SystemMessage = new SystemMessageConfig { Mode = SystemMessageMode.Replace, Content = systemPrompt },
            AvailableTools = new List<string>(),
            OnPermissionRequest = PermissionHandler.ApproveAll,
            Streaming = true,
        });

        var sb = new System.Text.StringBuilder();
        var done = new TaskCompletionSource();
        session.On(evt =>
        {
            if (evt is AssistantMessageDeltaEvent delta && !string.IsNullOrEmpty(delta.Data?.DeltaContent))
            {
                sb.Append(delta.Data.DeltaContent);
                if (!IsDisposed && _resultBox.IsHandleCreated)
                {
                    _resultBox.BeginInvoke(() =>
                    {
                        // Normalize line endings: TextBox renders \n alone as a space.
                        _resultBox.Text = NormalizeLineEndings(sb.ToString());
                        _resultBox.SelectionStart = _resultBox.TextLength;
                        _resultBox.ScrollToCaret();
                    });
                }
            }
            else if (evt is SessionIdleEvent)
            {
                done.TrySetResult();
            }
        });

        await session.SendAsync(new MessageOptions { Prompt = userPrompt });
        await done.Task;
        return sb.ToString();
    }

    private string ExecuteCustomProvider(string systemPrompt, string userPrompt)
    {
        string apiKey = ConfigManager.GetCustomApiKey();
        string endpoint = ConfigManager.GetCustomEndpoint();
        string deploymentId = ConfigManager.GetCustomDeploymentId();
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException("API key or endpoint not configured.");

        var client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        var options = new ChatCompletionsOptions
        {
            DeploymentName = deploymentId,
            Messages =
            {
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            }
        };
        Response<ChatCompletions> response = client.GetChatCompletions(options);
        var text = response.Value.Choices[0].Message.Content ?? string.Empty;
        _resultBox.Text = NormalizeLineEndings(text);
        return text;
    }

    /// <summary>
    /// Converts bare \n line endings to \r\n so multi-line AI output renders
    /// correctly in WinForms TextBox (which only treats \r\n as a line break).
    /// </summary>
    private static string NormalizeLineEndings(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // First normalize to \n (in case the source mixes), then to \r\n.
        return s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
    }

    private void FlashStatus(string text)
    {
        var prev = string.Empty;
        _setStatusHint(text);
        var t = new System.Windows.Forms.Timer { Interval = 1500 };
        t.Tick += (_, _) => { t.Stop(); t.Dispose(); UpdateStatusModeChip(); };
        t.Start();
    }

    /// <summary>Custom dark renderer for the model context menu.</summary>
    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors()) { RoundedEdges = true; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? Theme.AccentInk : Theme.Text;
            base.OnRenderItemText(e);
        }
        private class DarkColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Theme.Accent;
            public override Color MenuItemBorder => Theme.Border;
            public override Color ToolStripDropDownBackground => Theme.Surface;
            public override Color ImageMarginGradientBegin => Theme.Surface;
            public override Color ImageMarginGradientMiddle => Theme.Surface;
            public override Color ImageMarginGradientEnd => Theme.Surface;
            public override Color MenuBorder => Theme.Border;
            public override Color MenuStripGradientBegin => Theme.Surface;
            public override Color MenuStripGradientEnd => Theme.Surface;
        }
    }
}
