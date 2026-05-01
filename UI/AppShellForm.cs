using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AIPaste.UI.Controls;
using AIPaste.UI.Panes;

namespace AIPaste.UI;

/// <summary>
/// Main popup window — app shell with activity rail, top bar,
/// pane content area, and bottom status bar.
/// </summary>
public class AppShellForm : Form
{
    // === Win32: enable Windows 11 dark mode title bar ===
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // Win10 20H1+ / Win11
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19; // older Win10
    private const int DWMWA_CAPTION_COLOR = 35;          // Win11 build 22000+
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_TEXT_COLOR = 36;

    // === Win32: borderless drag-by-grabbing-top-bar ===
    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;

    private static int ColorToCOLORREF(Color c) => (c.B << 16) | (c.G << 8) | c.R;

    private void ApplyDarkTitleBar()
    {
        if (!IsHandleCreated) return;
        try
        {
            int useDark = 1;
            if (DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int)) != 0)
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));

            int caption = ColorToCOLORREF(Theme.Surface);
            DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            int border = ColorToCOLORREF(Theme.Border);
            DwmSetWindowAttribute(Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
            int textColor = ColorToCOLORREF(Theme.Text);
            DwmSetWindowAttribute(Handle, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
        }
        catch { /* best-effort — older Windows just stays light */ }
    }
    // =====================================================

    private ActivityRail _rail = null!;
    private Panel _topBar = null!;
    private Label _appTitle = null!;
    private Label _appSubtitle = null!;
    private Panel _contentHost = null!;
    private StatusBar _statusBar = null!;

    private ProcessPane? _processPane;
    private SettingsPane? _settingsPane;
    private CustomActionsPane? _customPane;

    private string _clipboardText;

    public AppShellForm(string clipboardText)
    {
        _clipboardText = clipboardText ?? string.Empty;

        Text = "AIPaste";
        Size = new Size(960, 640);
        MinimumSize = new Size(820, 520);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Body();
        Icon = LoadAppIcon();
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;

        BuildLayout();
        ShowPane(AppPane.Process);
        KeyDown += OnGlobalKeyDown;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyDarkTitleBar();
    }

    private void BuildLayout()
    {
        // Bottom status bar
        _statusBar = new StatusBar { Dock = DockStyle.Bottom };
        Controls.Add(_statusBar);

        // Activity rail
        _rail = new ActivityRail { Dock = DockStyle.Left };
        _rail.PaneSelected += (_, p) => ShowPane(p);
        Controls.Add(_rail);

        // Top bar
        _topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = Theme.TopBarHeight,
            BackColor = Theme.Surface,
        };
        _appTitle = new Label
        {
            Text = "AIPaste",
            ForeColor = Theme.Text,
            Font = Theme.BodyBold(),
            AutoSize = true,
            Location = new Point(16, 8),
            BackColor = Color.Transparent,
        };
        _appSubtitle = new Label
        {
            Text = $"{_clipboardText.Length} chars from clipboard · ready to process",
            ForeColor = Theme.TextMuted,
            Font = Theme.Small(),
            AutoSize = true,
            Location = new Point(16, 26),
            BackColor = Color.Transparent,
        };

        var pinBtn = MakeTopIconBtn("📌", "Pin on top");
        pinBtn.Visible = false; // hidden — popup is always TopMost
        var closeBtn = MakeTopIconBtn("✕", "Close (Esc)", danger: true);
        closeBtn.Click += (_, _) => Close();

        _topBar.SizeChanged += (_, _) =>
        {
            closeBtn.Location = new Point(_topBar.Width - 40, 9);
        };

        _topBar.Controls.Add(_appTitle);
        _topBar.Controls.Add(_appSubtitle);
        _topBar.Controls.Add(pinBtn);
        _topBar.Controls.Add(closeBtn);

        // Allow dragging the borderless window by grabbing the top bar.
        WireDrag(_topBar);
        WireDrag(_appTitle);
        WireDrag(_appSubtitle);

        var topDiv = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border };

        Controls.Add(topDiv);
        Controls.Add(_topBar);

        // Content host
        _contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
        };
        Controls.Add(_contentHost);

        Controls.SetChildIndex(_contentHost, 0);
        Controls.SetChildIndex(_topBar, 1);
        Controls.SetChildIndex(topDiv, 2);
        Controls.SetChildIndex(_rail, 3);
        Controls.SetChildIndex(_statusBar, 4);

        // Initial status
        _statusBar.Authenticated = ConfigManager.IsConfigComplete();
        _statusBar.CustomActionCount = ConfigManager.GetCustomActions().Count;
        _statusBar.Hint = "Press Enter to process";
    }

    private Button MakeTopIconBtn(string glyph, string tooltip, bool danger = false)
    {
        var b = new Button
        {
            Text = glyph,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextDim,
            Font = new Font(Theme.FontFamilyFallback, 11f),
            Size = new Size(30, 30),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = danger ? Color.FromArgb(40, Theme.Danger) : Theme.Surface2;
        var tt = new ToolTip();
        tt.SetToolTip(b, tooltip);
        return b;
    }

    private void ShowPane(AppPane pane)
    {
        _contentHost.SuspendLayout();
        _contentHost.Controls.Clear();
        switch (pane)
        {
            case AppPane.Process:
                if (_processPane == null)
                {
                    _processPane = new ProcessPane(
                        _clipboardText,
                        m => { _statusBar.ModelText = m; },
                        c => { _statusBar.ModeChip = c; },
                        h => { _statusBar.Hint = h; },
                        () => ShowPane(AppPane.CustomActions),
                        () => ShowPane(AppPane.Settings))
                    { Dock = DockStyle.Fill };
                    _processPane.AcceptedAndCopied += (_, _) => Close();
                }
                _contentHost.Controls.Add(_processPane);
                _appTitle.Text = "AIPaste";
                _appSubtitle.Text = $"{_clipboardText.Length} chars from clipboard · ready to process";
                _statusBar.CustomActionCount = ConfigManager.GetCustomActions().Count;
                break;

            case AppPane.CustomActions:
                if (_customPane == null)
                {
                    _customPane = new CustomActionsPane { Dock = DockStyle.Fill };
                    _customPane.ActionsChanged += (_, _) =>
                    {
                        _statusBar.CustomActionCount = ConfigManager.GetCustomActions().Count;
                        _processPane?.RefreshCustomActions();
                    };
                }
                else
                {
                    _customPane.ReloadList();
                }
                _contentHost.Controls.Add(_customPane);
                _appTitle.Text = "AIPaste";
                _appSubtitle.Text = "Custom Actions · saved AI prompts";
                break;

            case AppPane.Settings:
                _settingsPane = new SettingsPane(() => ShowPane(AppPane.Process)) { Dock = DockStyle.Fill };
                _settingsPane.Saved += (_, _) =>
                {
                    _statusBar.Authenticated = ConfigManager.IsConfigComplete();
                    // Force the process pane to reload models on next show
                    _processPane?.Dispose();
                    _processPane = null;
                };
                _contentHost.Controls.Add(_settingsPane);
                _appTitle.Text = "AIPaste";
                _appSubtitle.Text = "Settings · provider & default model";
                break;
        }
        _contentHost.ResumeLayout();
        _rail.Selected = pane;
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        if (e.Control && e.KeyCode == Keys.D1) { ShowPane(AppPane.Process); e.Handled = true; }
        else if (e.Control && e.KeyCode == Keys.D2) { ShowPane(AppPane.CustomActions); e.Handled = true; }
        else if (e.Control && e.KeyCode == Keys.Oemcomma) { ShowPane(AppPane.Settings); e.Handled = true; }
    }

    /// <summary>
    /// Loads the app icon embedded in AIPaste.exe.
    /// </summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            var exePath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(exePath))
                exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    exePath = System.IO.Path.ChangeExtension(exePath, ".exe");
                var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon != null) return icon;
            }
        }
        catch { }
        return SystemIcons.Application;
    }

    /// <summary>
    /// Wires mouse-down on a control to drag the borderless form, like a normal title bar.
    /// </summary>
    private void WireDrag(Control c)
    {
        c.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        };
    }
}
