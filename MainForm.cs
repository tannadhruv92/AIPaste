using System;
using System.Drawing;
using System.Windows.Forms;

namespace AIPaste;

public partial class MainForm : Form
{
    private NotifyIcon? notifyIcon;
    private ContextMenuStrip? contextMenu;
    private AIPaste.UI.AppShellForm? appWindow;
    private bool openPopupOnStart;

    private const int WM_SHOWME = 0x8001;

    public MainForm(bool openPopupOnStart = false)
    {
        InitializeComponent();
        this.openPopupOnStart = openPopupOnStart;
        
        InitializeSystemTray();
        
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        
        this.Load += Form1_Load;
    }

    private void Form1_Load(object? sender, EventArgs e)
    {
        this.Hide();

        // Check if provider is configured on startup
        if (!ConfigManager.IsProviderConfigured())
        {
            ShowConfigurationRequired();
            return;
        }

        if (openPopupOnStart)
        {
            OpenClipboardPopup();
        }

        QueueCopilotWarmup();
    }

    private void QueueCopilotWarmup()
    {
        if (ConfigManager.GetProvider() != AIProvider.GitHubCopilot)
            return;

        BeginInvoke((Action)(() => _ = CopilotClientManager.Instance.WarmUpAsync()));
    }
    
    private void ShowConfigurationRequired()
    {
        MessageBox.Show(
            "Welcome to AIPaste!\n\nPlease configure your AI provider to get started.",
            "Configuration Required",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        
        // Open the shell directly. The user can switch to Settings via the rail.
        ShowAppWindow(string.Empty);
    }

    private void InitializeSystemTray()
    {
        contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Open AIPaste", null, OnOpenClipboardPopup);
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exit", null, OnExit);

        notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            ContextMenuStrip = contextMenu,
            Text = "AIPaste",
            Visible = true
        };

        notifyIcon.DoubleClick += (s, e) => OpenClipboardPopup();
    }

    /// <summary>
    /// Loads the app icon embedded in AIPaste.exe (set via &lt;ApplicationIcon&gt; in csproj),
    /// falling back to the default application icon if extraction fails.
    /// </summary>
    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            // Preferred: load from the embedded managed resource (works in single-file).
            var stream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("AIPaste.app.ico");
            if (stream != null)
            {
                using (stream)
                    return new System.Drawing.Icon(stream);
            }
        }
        catch { }
        try
        {
            var exePath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(exePath))
                exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                // For .NET 9, GetEntryAssembly().Location returns the .dll, not the .exe.
                if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    exePath = System.IO.Path.ChangeExtension(exePath, ".exe");
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon != null) return icon;
            }
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    private void OnOpenClipboardPopup(object? sender, EventArgs e)
    {
        OpenClipboardPopup();
    }
    
    public void OpenClipboardPopupPublic()
    {
        OpenClipboardPopup();
    }

    private void OpenClipboardPopup()
    {
        // Check if provider is configured before opening popup
        if (!ConfigManager.IsProviderConfigured())
        {
            ShowConfigurationRequired();
            return;
        }
        
        try
        {
            ShowAppWindow(ReadClipboardText() ?? string.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening clipboard popup: {ex.Message}\n\n{ex.GetType().FullName}\n{ex.StackTrace}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowAppWindow(string clipboardText)
    {
        if (appWindow == null || appWindow.IsDisposed)
        {
            appWindow = new AIPaste.UI.AppShellForm(clipboardText);
            appWindow.FormClosed += (_, _) => appWindow = null;
        }
        else if (!string.IsNullOrEmpty(clipboardText))
        {
            appWindow.UpdateClipboardText(clipboardText);
        }

        if (appWindow.WindowState == FormWindowState.Minimized)
        {
            appWindow.WindowState = FormWindowState.Normal;
        }

        appWindow.Show();
        appWindow.Activate();
        appWindow.RefreshFromClipboard();
    }

    private static string? ReadClipboardText()
    {
        if (!Clipboard.ContainsText())
            return null;

        var text = Clipboard.GetText();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private void OnExit(object? sender, EventArgs e)
    {
        if (notifyIcon != null)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        }
        
        Application.Exit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_SHOWME)
        {
            OpenClipboardPopup();
            return;
        }
        
        base.WndProc(ref m);
    }
}
