using System.Drawing;
using System.Windows.Forms;
using AIPaste.UI.Controls;
using Azure;
using Azure.AI.OpenAI;
using GitHub.Copilot;

namespace AIPaste.UI.Panes;

public enum ProcessMode { Rewrite, Translate, Custom }

/// <summary>
/// The default pane — the light "Transform Studio": a SOURCE pane on the left,
/// a central SPINE (mode / model / transform) and a RESULT pane on the right,
/// with a footer hosting the Accept &amp; Copy action.
/// </summary>
public class ProcessPane : UserControl
{
    private readonly Action<string> _setStatusModel;
    private readonly Action<string> _setStatusModeChip;
    private readonly Action<string> _setStatusHint;
    private readonly Action<string> _setStatusTiming;
    private readonly Action _onRequestManageActions;
    private readonly Action _onRequestSettings;

    // Source pane
    private RichTextBox _originalBox = null!;
    private Label _charCount = null!;

    // Spine
    private SpineDropdownButton _rewriteBtn = null!;
    private SpineDropdownButton _translateBtn = null!;
    private SpineDropdownButton _customBtn = null!;
    private SpineDropdownButton _modelBtn = null!;
    private TransformButton _transformBtn = null!;

    // Result pane
    private RichTextBox _resultBox = null!;

    // State
    private string _originalText = string.Empty;
    private string _processedText = string.Empty;
    private ProcessMode _currentMode = ProcessMode.Rewrite;
    private string _currentTone = "Professional";
    private string _currentLanguage = "Hindi";
    private CustomAction? _currentAction;
    private string _currentModel = string.Empty;

    private const string ResultPlaceholder = "✨  Result will appear here";

    public string ProcessedText => _processedText;
    public event EventHandler<string>? AcceptedAndCopied;

    public ProcessPane(string originalText,
        Action<string> setStatusModel,
        Action<string> setStatusModeChip,
        Action<string> setStatusHint,
        Action<string> setStatusTiming,
        Action onRequestManageActions,
        Action onRequestSettings)
    {
        _originalText = originalText ?? string.Empty;
        _setStatusModel = setStatusModel;
        _setStatusModeChip = setStatusModeChip;
        _setStatusHint = setStatusHint;
        _setStatusTiming = setStatusTiming;
        _onRequestManageActions = onRequestManageActions;
        _onRequestSettings = onRequestSettings;

        BackColor = Theme.Surface;
        Padding = new Padding(0);

        _currentAction = ConfigManager.GetCustomActions().FirstOrDefault();
        _currentModel = ResolveInitialModel();

        BuildLayout();
        SetActiveMode(ProcessMode.Rewrite);
        UpdateOriginal(_originalText);
        PublishCurrentModel();
    }

    public void UpdateOriginal(string text)
    {
        var newText = text ?? string.Empty;
        bool changed = !string.Equals(_originalText, newText, StringComparison.Ordinal);
        _originalText = newText;
        _originalBox.Text = NormalizeLineEndings(_originalText);
        _charCount.Text = $"{_originalText.Length} chars";

        if (changed)
        {
            _processedText = string.Empty;
            _resultBox.ForeColor = Theme.TextMuted;
            _resultBox.Text = ResultPlaceholder;
            _setStatusTiming(string.Empty);
        }
    }

    /// <summary>Gives keyboard focus to the primary (Rewrite) mode button.</summary>
    public void FocusPrimary() => _rewriteBtn?.Focus();

    private static string ResolveInitialModel()
    {
        if (ConfigManager.GetProvider() == AIProvider.GitHubCopilot)
        {
            var preferred = ConfigManager.GetCopilotPreferredModel();
            return string.IsNullOrWhiteSpace(preferred) ? "gpt-4o" : preferred;
        }

        return ConfigManager.GetCustomDeploymentId();
    }

    private void PublishCurrentModel()
    {
        _setStatusModel(string.IsNullOrWhiteSpace(_currentModel) ? "(no model)" : _currentModel);
    }

    // ============================================================== Layout ===

    private void BuildLayout()
    {
        SuspendLayout();

        var stage = BuildStage();
        var footer = BuildFooter();

        // Fill first (back), then bottom (front) so Fill avoids the footer.
        Controls.Add(stage);
        Controls.Add(footer);

        ResumeLayout(true);
    }

    private TableLayoutPanel BuildStage()
    {
        var stage = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Surface,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        stage.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        stage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        stage.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        stage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        stage.Controls.Add(BuildSourcePane(), 0, 0);
        stage.Controls.Add(BuildSpine(), 1, 0);
        stage.Controls.Add(BuildResultPane(), 2, 0);
        return stage;
    }

    private Panel BuildSourcePane()
    {
        var pane = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Margin = new Padding(0) };

        _charCount = new Label
        {
            Text = "0 chars",
            ForeColor = Theme.TextMuted,
            Font = Theme.Small(),
            AutoSize = true,
            BackColor = Color.Transparent,
        };
        var header = BuildPaneHeader("SOURCE", _charCount);

        _originalBox = new RichTextBox
        {
            ReadOnly = true,
            WordWrap = true,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = new Font(Theme.FontFamilyFallback, 10.5f),
            Dock = DockStyle.Fill,
        };
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 4, 16, 16), BackColor = Theme.Surface };
        host.Controls.Add(_originalBox);

        pane.Controls.Add(host);
        pane.Controls.Add(header);
        return pane;
    }

    private SpinePanel BuildSpine()
    {
        var spine = new SpinePanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(14),
            Margin = new Padding(0),
        };

        _rewriteBtn = new SpineDropdownButton
        {
            Glyph = "✨",
            Title = "Rewrite",
            Subtitle = _currentTone,
            Active = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6),
        };
        _rewriteBtn.Clicked += OnRewriteClicked;

        _translateBtn = new SpineDropdownButton
        {
            Glyph = "🌐",
            Title = "Translate",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6),
        };
        _translateBtn.Clicked += OnTranslateClicked;

        _customBtn = new SpineDropdownButton
        {
            Glyph = "⚡",
            Title = "Custom",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6),
        };
        _customBtn.Clicked += OnCustomClicked;

        _modelBtn = new SpineDropdownButton
        {
            Glyph = "⚡",
            Title = string.IsNullOrEmpty(_currentModel) ? "(no model)" : _currentModel,
            ShowCaret = true,
            Active = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
        _modelBtn.Clicked += OnModelClicked;

        _transformBtn = new TransformButton
        {
            Verb = "Process",
            Dock = DockStyle.Bottom,
            Height = 104,
            Margin = new Padding(0),
        };
        _transformBtn.Clicked += async (_, _) => await ExecuteCurrentRequestAsync(false);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            BackColor = Theme.Surface,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;
        void AddRow(Control c, int height)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            grid.Controls.Add(c, 0, row);
            row++;
        }

        AddRow(MakeCaption("MODE"), 22);
        AddRow(_rewriteBtn, 60); // taller: hosts the tone subtitle line
        AddRow(_translateBtn, 50);
        AddRow(_customBtn, 50);
        AddRow(MakeDivider(), 15);
        AddRow(MakeCaption("MODEL"), 22);
        AddRow(_modelBtn, 46);
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // spacer pushes content up
        grid.RowCount = row + 1;

        // Fill first (back), then bottom (front) so the grid avoids the button.
        spine.Controls.Add(grid);
        spine.Controls.Add(_transformBtn);
        return spine;
    }

    private Panel BuildResultPane()
    {
        var pane = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Margin = new Padding(0) };

        var header = BuildPaneHeader("RESULT");

        _resultBox = new RichTextBox
        {
            ReadOnly = true,
            WordWrap = true,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextMuted,
            Font = new Font(Theme.FontFamilyFallback, 10.5f),
            Dock = DockStyle.Fill,
            Text = ResultPlaceholder,
        };
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 4, 16, 16), BackColor = Theme.Surface };
        host.Controls.Add(_resultBox);

        pane.Controls.Add(host);
        pane.Controls.Add(header);
        return pane;
    }

    private Panel BuildFooter()
    {
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Theme.Surface };
        var topBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border };

        var rowPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
            Padding = new Padding(14, 0, 14, 0),
            Margin = new Padding(0),
        };
        rowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rowPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var accept = MakeAccentBtn("✓  Accept & Copy", AcceptAndCopy);
        accept.Anchor = AnchorStyles.None;
        accept.Margin = new Padding(0);

        rowPanel.Controls.Add(accept, 1, 0);

        // Fill first (back), then top border (front).
        footer.Controls.Add(rowPanel);
        footer.Controls.Add(topBorder);
        return footer;
    }

    /// <summary>Builds a 34px pane header with a left caption and an optional right-aligned control.</summary>
    private static Panel BuildPaneHeader(string title, Control? rightControl = null)
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Theme.Surface };
        var label = new Label
        {
            Text = title,
            ForeColor = Theme.TextDim,
            Font = Theme.SmallBold(),
            AutoSize = true,
            BackColor = Color.Transparent,
        };
        header.Controls.Add(label);
        if (rightControl != null) header.Controls.Add(rightControl);

        void DoLayout()
        {
            if (label.IsDisposed) return;
            label.Location = new Point(16, (header.Height - label.Height) / 2);
            if (rightControl != null && !rightControl.IsDisposed)
                rightControl.Location = new Point(
                    header.ClientSize.Width - rightControl.Width - 14,
                    (header.Height - rightControl.Height) / 2);
        }
        header.SizeChanged += (_, _) => DoLayout();
        label.SizeChanged += (_, _) => DoLayout();
        if (rightControl != null) rightControl.SizeChanged += (_, _) => DoLayout();
        header.HandleCreated += (_, _) => DoLayout();
        return header;
    }

    private static Label MakeCaption(string text) => new()
    {
        Text = text,
        ForeColor = Theme.TextMuted,
        Font = Theme.SmallBold(),
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        BackColor = Color.Transparent,
        Margin = new Padding(2, 0, 0, 2),
    };

    private static Panel MakeDivider() => new()
    {
        Height = 1,
        BackColor = Theme.Border,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        Margin = new Padding(2, 0, 2, 0),
    };

    private Button MakeAccentBtn(string text, Action onClick)
    {
        var b = new Button
        {
            Text = text,
            UseMnemonic = false, // keep the literal "&" in "Accept & Copy"
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Accent,
            ForeColor = Theme.AccentInk,
            Font = Theme.BodyBold(),
            AutoSize = true,
            Padding = new Padding(14, 6, 14, 6),
            Cursor = Cursors.Hand,
            Margin = new Padding(0),
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Theme.Accent2;
        b.Click += (_, _) => onClick();
        return b;
    }

    // ============================================================ Behaviour ===

    private void SetActiveMode(ProcessMode m)
    {
        _currentMode = m;
        _rewriteBtn.Active = m == ProcessMode.Rewrite;
        _translateBtn.Active = m == ProcessMode.Translate;
        _customBtn.Active = m == ProcessMode.Custom;
        _transformBtn.Verb = m == ProcessMode.Translate ? "Translate" : "Process";
        RefreshModeLabels();
        UpdateStatusModeChip();
        _transformBtn.Invalidate();
    }

    /// <summary>
    /// Updates each mode button's label so only the ACTIVE mode shows its chosen value
    /// (tone / language / action); the inactive modes show their generic word. This is how
    /// you "stop translating": switch to Rewrite (or Custom) and the language label clears
    /// back to "Translate".
    /// </summary>
    private void RefreshModeLabels()
    {
        _rewriteBtn.Glyph = "✨";
        _rewriteBtn.Title = "Rewrite";
        _rewriteBtn.Subtitle = _currentTone;

        _translateBtn.Glyph = "🌐";
        _translateBtn.Title = _currentMode == ProcessMode.Translate ? _currentLanguage : "Translate";

        if (_currentMode == ProcessMode.Custom && _currentAction != null)
        {
            _customBtn.Glyph = "📝";
            _customBtn.Title = _currentAction.Name;
        }
        else
        {
            _customBtn.Glyph = "⚡";
            _customBtn.Title = "Custom";
        }

        _rewriteBtn.Invalidate();
        _translateBtn.Invalidate();
        _customBtn.Invalidate();
    }

    private static ContextMenuStrip NewMenu() => AIPaste.UI.Controls.ThemedMenu.Create();

    private void OnRewriteClicked(object? sender, EventArgs e)
    {
        SetActiveMode(ProcessMode.Rewrite);
        var menu = NewMenu();
        foreach (var tone in new[] { "Professional", "Casual", "Informative", "Enthusiastic" })
        {
            var captured = tone;
            var item = new ToolStripMenuItem(tone) { ForeColor = Theme.Text, Checked = tone == _currentTone };
            item.Click += (_, _) =>
            {
                _currentTone = captured;
                RefreshModeLabels();
                UpdateStatusModeChip();
            };
            menu.Items.Add(item);
        }
        menu.Show(_rewriteBtn.PointToScreen(new Point(0, _rewriteBtn.Height)));
    }

    private void OnTranslateClicked(object? sender, EventArgs e)
    {
        var menu = NewMenu();
        bool translating = _currentMode == ProcessMode.Translate;

        // Default value — the "off" entry, checked when nothing is being translated.
        // Selecting it stops translating right from this dropdown (no need to click Rewrite).
        var noneItem = new ToolStripMenuItem("Don't translate") { ForeColor = Theme.Text, Checked = !translating };
        noneItem.Click += (_, _) => SetActiveMode(ProcessMode.Rewrite);
        menu.Items.Add(noneItem);
        menu.Items.Add(new ToolStripSeparator());

        foreach (var lang in new[] { "Hindi", "Gujarati" })
        {
            var captured = lang;
            var item = new ToolStripMenuItem(lang) { ForeColor = Theme.Text, Checked = translating && lang == _currentLanguage };
            item.Click += (_, _) =>
            {
                _currentLanguage = captured;
                SetActiveMode(ProcessMode.Translate);
            };
            menu.Items.Add(item);
        }
        menu.Show(_translateBtn.PointToScreen(new Point(0, _translateBtn.Height)));
    }

    private void OnCustomClicked(object? sender, EventArgs e)
    {
        var menu = NewMenu();
        bool usingCustom = _currentMode == ProcessMode.Custom;

        // Default value — the "off" entry, checked when no custom action is running.
        // Selecting it stops using a custom action straight from this dropdown.
        var noneItem = new ToolStripMenuItem("No custom action") { ForeColor = Theme.Text, Checked = !usingCustom };
        noneItem.Click += (_, _) => SetActiveMode(ProcessMode.Rewrite);
        menu.Items.Add(noneItem);

        var actions = ConfigManager.GetCustomActions();
        if (actions.Count > 0) menu.Items.Add(new ToolStripSeparator());
        foreach (var a in actions)
        {
            var captured = a;
            var item = new ToolStripMenuItem(a.Name) { ForeColor = Theme.Text, Checked = usingCustom && a.Name == _currentAction?.Name };
            item.Click += (_, _) =>
            {
                _currentAction = captured;
                SetActiveMode(ProcessMode.Custom);
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());
        var manage = new ToolStripMenuItem("⚙  Manage actions…") { ForeColor = Theme.Text };
        manage.Click += (_, _) => _onRequestManageActions();
        menu.Items.Add(manage);
        menu.Show(_customBtn.PointToScreen(new Point(0, _customBtn.Height)));
    }

    private void OnModelClicked(object? sender, EventArgs e)
    {
        if (ConfigManager.GetProvider() != AIProvider.GitHubCopilot) return;
        _ = ShowModelPickerAsync();
    }

    private async Task ShowModelPickerAsync()
    {
        IReadOnlyList<ModelInfo>? models = null;
        try { models = await ConfigManager.GetCopilotModelsAsync(); } catch { }
        if (models == null || models.Count == 0) return;

        var menu = NewMenu();
        foreach (var m in models.OrderBy(m => m.Name))
        {
            var captured = m;
            var item = new ToolStripMenuItem(m.Id) { Tag = m.Id, ForeColor = Theme.Text, Checked = m.Id == _currentModel };
            item.Click += (_, _) =>
            {
                _currentModel = captured.Id;
                _modelBtn.Title = _currentModel;
                _setStatusModel(_currentModel);
                _modelBtn.Invalidate();
            };
            menu.Items.Add(item);
        }
        menu.Show(_modelBtn.PointToScreen(new Point(0, _modelBtn.Height)));
    }

    private void UpdateStatusModeChip()
    {
        switch (_currentMode)
        {
            case ProcessMode.Rewrite:
                _setStatusModeChip(_currentTone);
                _setStatusHint("Press Enter to process");
                break;
            case ProcessMode.Translate:
                _setStatusModeChip(_currentLanguage);
                _setStatusHint("Press Enter to translate");
                break;
            case ProcessMode.Custom:
                _setStatusModeChip(_currentAction?.Name ?? "");
                _setStatusHint("Press Enter to run");
                break;
        }
    }

    public void RefreshCustomActions()
    {
        var actions = ConfigManager.GetCustomActions();
        var cur = _currentAction;
        bool stillExists = cur != null && actions.Any(a => a.Name == cur.Name);
        if (stillExists) return;

        _currentAction = actions.FirstOrDefault();
        if (_currentMode == ProcessMode.Custom)
        {
            if (_currentAction != null)
            {
                _customBtn.Glyph = "📝";
                _customBtn.Title = _currentAction.Name;
            }
            else
            {
                _customBtn.Glyph = "⚡";
                _customBtn.Title = "Custom";
            }
            _customBtn.Invalidate();
            UpdateStatusModeChip();
        }
    }

    private void AcceptAndCopy()
    {
        if (!string.IsNullOrEmpty(_processedText))
        {
            Clipboard.SetText(_processedText);
            AcceptedAndCopied?.Invoke(this, _processedText);
        }
    }

    // ============================================================== Process ===

    public async Task ExecuteCurrentRequestAsync(bool isRetry)
    {
        if (string.IsNullOrEmpty(_originalText)) return;
        if (!ConfigManager.IsConfigComplete())
        {
            _onRequestSettings();
            return;
        }

        _resultBox.ForeColor = Theme.Text;
        _resultBox.Text = isRetry ? string.Empty : "Processing…";
        _setStatusTiming(string.Empty);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            string systemPrompt;
            string userPrompt;
            switch (_currentMode)
            {
                case ProcessMode.Rewrite:
                    systemPrompt = "You are a helpful assistant that rewrites text based on the specified tone. Only return the rewritten text, nothing else.";
                    userPrompt = $"Rewrite the following text in a {_currentTone} tone:\n\n\"{_originalText}\"";
                    break;
                case ProcessMode.Translate:
                    systemPrompt = "You are a helpful assistant that translates text. Only return the translated text, nothing else.";
                    userPrompt = $"Translate the following text to {_currentLanguage} in a {_currentTone} tone:\n\n\"{_originalText}\"";
                    break;
                default:
                    if (_currentAction == null)
                    {
                        SetResultText("No custom action selected.");
                        return;
                    }
                    systemPrompt = "You are a helpful assistant that processes text based on the given instructions. Only return the processed result, nothing else.";
                    userPrompt = _currentAction.Prompt.Contains("{text}")
                        ? _currentAction.Prompt.Replace("{text}", _originalText)
                        : $"{_currentAction.Prompt}\n\nText to process: \"{_originalText}\"";
                    break;
            }

            string result = ConfigManager.GetProvider() == AIProvider.GitHubCopilot
                ? await ExecuteCopilotAsync(systemPrompt, userPrompt)
                : ExecuteCustomProvider(systemPrompt, userPrompt);

            _processedText = result;
        }
        catch (Exception ex)
        {
            SetResultText($"Error: {ex.Message}");
            _processedText = string.Empty;
        }
        finally
        {
            sw.Stop();
            var elapsed = FormatElapsed(sw.Elapsed);
            _setStatusTiming(elapsed);
        }
    }

    private void SetResultText(string text)
    {
        _resultBox.ForeColor = Theme.Text;
        _resultBox.Text = text;
    }

    private static string FormatElapsed(TimeSpan t)
    {
        double secs = t.TotalSeconds;
        return secs < 1
            ? $"{t.TotalMilliseconds:0} ms"
            : $"{secs:0.0}s";
    }

    private async Task<string> ExecuteCopilotAsync(string systemPrompt, string userPrompt)
    {
        // Show a live placeholder until the first streamed token arrives — the CLI
        // runtime has a few seconds of time-to-first-token, so without this the result
        // pane would sit blank after the user clicks Process.
        _resultBox.ForeColor = Theme.TextDim;
        _resultBox.Text = "Streaming…";
        var sb = new System.Text.StringBuilder();

        await using var session = await CopilotClientManager.Instance.CreateSessionAsync(new SessionConfig
        {
            Model = _currentModel,
            SystemMessage = new SystemMessageConfig { Mode = SystemMessageMode.Replace, Content = systemPrompt },
            AvailableTools = new List<string>(),
            OnPermissionRequest = PermissionHandler.ApproveAll,
            Streaming = true,
            // One-shot text transform — disable the agentic machinery (skills, embeddings,
            // hooks, git, MCP, session store, infinite-session workspace, config/instruction
            // discovery) so each request is as close to a plain completion as the SDK allows.
            SkipEmbeddingRetrieval = true,
            SkipCustomInstructions = true,
            EnableConfigDiscovery = false,
            EnableOnDemandInstructionDiscovery = false,
            EnableSkills = false,
            EnableFileHooks = false,
            EnableHostGitOperations = false,
            EnableSessionStore = false,
            EnableSessionTelemetry = false,
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
        });

        var done = new TaskCompletionSource();
        session.On<SessionEvent>(evt =>
        {
            if (evt is AssistantMessageDeltaEvent delta && !string.IsNullOrEmpty(delta.Data?.DeltaContent))
            {
                sb.Append(delta.Data.DeltaContent);
                if (!IsDisposed && _resultBox.IsHandleCreated)
                {
                    _resultBox.BeginInvoke(() =>
                    {
                        // First real token replaces the "Streaming…" placeholder.
                        _resultBox.ForeColor = Theme.Text;
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

        // No content streamed (e.g. empty or blocked response): don't leave the placeholder.
        if (sb.Length == 0 && !IsDisposed && _resultBox.IsHandleCreated)
            _resultBox.BeginInvoke(() => { _resultBox.ForeColor = Theme.Text; _resultBox.Text = string.Empty; });

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
        SetResultText(NormalizeLineEndings(text));
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

    /// <summary>A spine panel that paints 1px left and right borders.</summary>
    private sealed class SpinePanel : Panel
    {
        public SpinePanel() { ResizeRedraw = true; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(Theme.Border, 1f);
            e.Graphics.DrawLine(pen, 0, 0, 0, Height - 1);
            e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height - 1);
        }
    }
}
