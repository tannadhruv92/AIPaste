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
        
        var configForm = new ConfigurationForm();
        configForm.ShowDialog();
    }

    private void InitializeSystemTray()
    {
        contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Open Clipboard Popup", null, OnOpenClipboardPopup);
        contextMenu.Items.Add("Configure Provider", null, OnConfigureProvider);
        contextMenu.Items.Add("Manage Custom Actions", null, OnManageCustomActions);
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exit", null, OnExit);

        notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = contextMenu,
            Text = "AIPaste",
            Visible = true
        };

        notifyIcon.DoubleClick += (s, e) => OpenClipboardPopup();
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
                var popup = new ClipboardPopupForm(clipboardText);
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
            MessageBox.Show($"Error opening clipboard popup: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnConfigureProvider(object? sender, EventArgs e)
    {
        var configForm = new ConfigurationForm();
        configForm.ShowDialog();
    }

    private void OnManageCustomActions(object? sender, EventArgs e)
    {
        var customActionsForm = new CustomActionsForm();
        customActionsForm.ShowDialog();
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
