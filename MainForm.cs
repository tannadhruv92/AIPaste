using System;
using System.Drawing;
using System.Windows.Forms;

namespace AIPaste;

public partial class MainForm : Form
{
    private NotifyIcon? notifyIcon;
    private ContextMenuStrip? contextMenu;
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
        
        // Pre-warm Copilot token exchange in background to cut first-request latency.
        if (ConfigManager.IsProviderConfigured() && ConfigManager.GetProvider() == AIProvider.GitHubCopilot
            && AIPaste.Copilot.CopilotAuth.IsSignedIn)
        {
            _ = AIPaste.Copilot.CopilotAuth.GetCopilotTokenAsync();
        }
        
        // Check if provider is configured on startup
        if (!ConfigManager.IsProviderConfigured())
        {
            ShowConfigurationRequired();
        }
        else if (openPopupOnStart)
        {
            OpenClipboardPopup();
        }
    }
    
    private void ShowConfigurationRequired()
    {
        MessageBox.Show(
            "Welcome to AIPaste!\n\nPlease configure your AI provider to get started.",
            "Configuration Required",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        
        // Open the new shell directly. The user can switch to Settings via the rail.
        var popup = new AIPaste.UI.AppShellForm(string.Empty);
        popup.Show();
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
            string clipboardText = string.Empty;
            if (Clipboard.ContainsText())
            {
                clipboardText = Clipboard.GetText();
            }

            if (!string.IsNullOrEmpty(clipboardText))
            {
                var popup = new AIPaste.UI.AppShellForm(clipboardText);
                popup.Show();
            }
            else
            {
                MessageBox.Show("No text found in clipboard.", "AIPaste", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening clipboard popup: {ex.Message}\n\n{ex.GetType().FullName}\n{ex.StackTrace}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
