using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using GitHub.Copilot.SDK;

namespace AIPaste
{
    public partial class ConfigurationForm : Form
    {
        private ComboBox? providerComboBox;
        
        // GitHub Copilot controls
        private Panel? copilotPanel;
        private Label? authStatusLabel;
        private Button? checkAuthButton;
        private Label? modelLabel;
        private ComboBox? modelComboBox;
        
        // Custom Provider controls
        private Panel? customPanel;
        private TextBox? apiKeyTextBox;
        private TextBox? endpointTextBox;
        private TextBox? deploymentIdTextBox;

        public ConfigurationForm()
        {
            InitializeComponents();
            LoadSettings();
            
            // Auto-check auth when form loads if GitHub Copilot is selected
            this.Shown += async (s, e) => await AutoCheckAuthAsync();
        }
        
        private async Task AutoCheckAuthAsync()
        {
            if (providerComboBox?.SelectedIndex == 0) // GitHub Copilot
            {
                await CheckAndLoadModelsAsync();
            }
        }

        private void InitializeComponents()
        {
            this.Text = "Configure AIPaste";
            this.Size = new Size(500, 350);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Provider Selection
            Label providerLabel = new Label
            {
                Text = "AI Provider:",
                Location = new Point(20, 20),
                Size = new Size(80, 20)
            };

            providerComboBox = new ComboBox
            {
                Location = new Point(120, 20),
                Size = new Size(340, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            providerComboBox.Items.Add("GitHub Copilot");
            providerComboBox.Items.Add("Azure OpenAI");
            providerComboBox.SelectedIndexChanged += ProviderComboBox_SelectedIndexChanged;

            // GitHub Copilot Panel
            copilotPanel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(440, 210),
                Visible = false
            };
            
            Label copilotInfoLabel = new Label
            {
                Text = "GitHub Copilot requires CLI authentication.\nStart Copilot CLI and use /login command:",
                Location = new Point(0, 0),
                Size = new Size(440, 40)
            };
            
            TextBox cliCommandTextBox = new TextBox
            {
                Text = "copilot",
                Location = new Point(0, 45),
                Size = new Size(340, 25),
                ReadOnly = true,
                BackColor = Color.FromArgb(240, 240, 240),
                Font = new Font("Consolas", 10)
            };
            
            Button copyCommandButton = new Button
            {
                Text = "Copy",
                Location = new Point(350, 45),
                Size = new Size(60, 25)
            };
            copyCommandButton.Click += (s, e) => {
                Clipboard.SetText("copilot");
                MessageBox.Show("Command copied! Run 'copilot' then type /login", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            
            authStatusLabel = new Label
            {
                Text = "Status: Not checked",
                Location = new Point(0, 80),
                Size = new Size(440, 35),
                ForeColor = Color.Gray
            };
            
            checkAuthButton = new Button
            {
                Text = "Check Auth Status",
                Location = new Point(0, 115),
                Size = new Size(130, 30)
            };
            checkAuthButton.Click += CheckAuthButton_Click;
            
            // Model selector (hidden until auth succeeds)
            modelLabel = new Label
            {
                Text = "Preferred Model:",
                Location = new Point(0, 155),
                Size = new Size(100, 20),
                Visible = false
            };
            
            modelComboBox = new ComboBox
            {
                Location = new Point(110, 152),
                Size = new Size(250, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };
            
            Label modelNoteLabel = new Label
            {
                Name = "modelNoteLabel",
                Text = "This will be your default. You can change it per-request in the popup.",
                Location = new Point(0, 180),
                Size = new Size(440, 20),
                ForeColor = Color.Gray,
                Font = new Font(this.Font, FontStyle.Italic),
                Visible = false
            };
            
            copilotPanel.Controls.Add(copilotInfoLabel);
            copilotPanel.Controls.Add(cliCommandTextBox);
            copilotPanel.Controls.Add(copyCommandButton);
            copilotPanel.Controls.Add(authStatusLabel);
            copilotPanel.Controls.Add(checkAuthButton);
            copilotPanel.Controls.Add(modelLabel);
            copilotPanel.Controls.Add(modelComboBox);
            copilotPanel.Controls.Add(modelNoteLabel);

            // Custom Provider Panel
            customPanel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(440, 180),
                Visible = false
            };
            
            Label apiKeyLabel = new Label
            {
                Text = "API Key:",
                Location = new Point(0, 0),
                Size = new Size(100, 20)
            };

            apiKeyTextBox = new TextBox
            {
                Location = new Point(0, 25),
                Size = new Size(440, 25),
                PasswordChar = '•'
            };

            Label endpointLabel = new Label
            {
                Text = "Endpoint URL:",
                Location = new Point(0, 55),
                Size = new Size(100, 20)
            };

            endpointTextBox = new TextBox
            {
                Location = new Point(0, 80),
                Size = new Size(440, 25)
            };

            Label deploymentIdLabel = new Label
            {
                Text = "Deployment ID / Model:",
                Location = new Point(0, 110),
                Size = new Size(150, 20)
            };

            deploymentIdTextBox = new TextBox
            {
                Location = new Point(0, 135),
                Size = new Size(440, 25)
            };
            
            Label customNoteLabel = new Label
            {
                Text = "Enter your Azure OpenAI resource details to connect.",
                Location = new Point(0, 165),
                Size = new Size(440, 20),
                ForeColor = Color.Gray
            };
            
            customPanel.Controls.Add(apiKeyLabel);
            customPanel.Controls.Add(apiKeyTextBox);
            customPanel.Controls.Add(endpointLabel);
            customPanel.Controls.Add(endpointTextBox);
            customPanel.Controls.Add(deploymentIdLabel);
            customPanel.Controls.Add(deploymentIdTextBox);
            customPanel.Controls.Add(customNoteLabel);

            // Buttons
            Button saveButton = new Button
            {
                Text = "Save",
                Location = new Point(290, 270),
                Size = new Size(90, 30)
            };
            saveButton.Click += SaveButton_Click;

            Button cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(390, 270),
                Size = new Size(90, 30)
            };
            cancelButton.Click += CancelButton_Click;

            // Add controls
            this.Controls.Add(providerLabel);
            this.Controls.Add(providerComboBox);
            this.Controls.Add(copilotPanel);
            this.Controls.Add(customPanel);
            this.Controls.Add(saveButton);
            this.Controls.Add(cancelButton);
        }

        private void ProviderComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (providerComboBox == null || copilotPanel == null || customPanel == null)
                return;
                
            bool isGitHubCopilot = providerComboBox.SelectedIndex == 0;
            copilotPanel.Visible = isGitHubCopilot;
            customPanel.Visible = !isGitHubCopilot;
            
            // Auto-check auth when switching to GitHub Copilot
            if (isGitHubCopilot)
            {
                _ = CheckAndLoadModelsAsync();
            }
        }

        private async void CheckAuthButton_Click(object? sender, EventArgs e)
        {
            if (checkAuthButton != null)
                checkAuthButton.Enabled = false;
            
            await CheckAndLoadModelsAsync();
            
            if (checkAuthButton != null)
                checkAuthButton.Enabled = true;
        }
        
        private async Task CheckAndLoadModelsAsync()
        {
            if (authStatusLabel == null)
                return;
                
            authStatusLabel.Text = "Checking authentication...";
            authStatusLabel.ForeColor = Color.Gray;
            
            // Hide model selector while checking
            if (modelLabel != null) modelLabel.Visible = false;
            if (modelComboBox != null) modelComboBox.Visible = false;
            var modelNoteLabel = copilotPanel?.Controls["modelNoteLabel"];
            if (modelNoteLabel != null) modelNoteLabel.Visible = false;
            
            try
            {
                var (isAuthenticated, message) = await ConfigManager.CheckCopilotAuthAsync();
                
                authStatusLabel.Text = isAuthenticated ? $"✓ {message}" : $"✗ {message}";
                authStatusLabel.ForeColor = isAuthenticated ? Color.Green : Color.Red;
                
                // Show model selector if authenticated
                if (isAuthenticated)
                {
                    await LoadModelsAsync();
                }
            }
            catch
            {
                authStatusLabel.Text = "✗ Error checking authentication";
                authStatusLabel.ForeColor = Color.Red;
            }
        }
        
        private async Task LoadModelsAsync()
        {
            var models = await ConfigManager.GetCopilotModelsAsync();
            
            if (models != null && models.Count > 0 && modelComboBox != null && modelLabel != null)
            {
                modelComboBox.Items.Clear();
                foreach (var model in models.OrderBy(m => m.Name))
                {
                    modelComboBox.Items.Add(model.Id);
                }
                
                // Select current preferred model
                string preferredModel = ConfigManager.GetCopilotPreferredModel();
                int modelIndex = modelComboBox.Items.IndexOf(preferredModel);
                modelComboBox.SelectedIndex = modelIndex >= 0 ? modelIndex : 0;
                
                // Show model selector
                modelLabel.Visible = true;
                modelComboBox.Visible = true;
                var modelNoteLabel = copilotPanel?.Controls["modelNoteLabel"];
                if (modelNoteLabel != null) modelNoteLabel.Visible = true;
            }
        }

        private void LoadSettings()
        {
            var provider = ConfigManager.GetProvider();
            
            if (providerComboBox != null)
            {
                if (provider == AIProvider.GitHubCopilot)
                {
                    providerComboBox.SelectedIndex = 0;
                }
                else if (provider == AIProvider.Custom)
                {
                    providerComboBox.SelectedIndex = 1;
                }
                else
                {
                    // Not configured - default to GitHub Copilot
                    providerComboBox.SelectedIndex = 0;
                }
            }
            
            // Load Custom provider settings
            if (!string.IsNullOrEmpty(ConfigManager.GetCustomApiKey()) && apiKeyTextBox != null)
            {
                apiKeyTextBox.Text = "••••••••••••••••••••••••••";
            }

            if (endpointTextBox != null)
            {
                endpointTextBox.Text = ConfigManager.GetCustomEndpoint();
            }
            
            if (deploymentIdTextBox != null)
            {
                deploymentIdTextBox.Text = ConfigManager.GetCustomDeploymentId();
            }
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            if (providerComboBox == null)
                return;
                
            AIProvider selectedProvider = providerComboBox.SelectedIndex == 0 
                ? AIProvider.GitHubCopilot 
                : AIProvider.Custom;
            
            // Validate based on selected provider
            if (selectedProvider == AIProvider.Custom)
            {
                if (string.IsNullOrWhiteSpace(endpointTextBox?.Text))
                {
                    MessageBox.Show("Please enter the API endpoint.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(deploymentIdTextBox?.Text))
                {
                    MessageBox.Show("Please enter the deployment ID / model name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // Only validate API key if it's a new entry (not masked)
                bool isNewApiKey = apiKeyTextBox?.Text != "••••••••••••••••••••••••••" && !string.IsNullOrWhiteSpace(apiKeyTextBox?.Text);
                bool hasExistingApiKey = !string.IsNullOrEmpty(ConfigManager.GetCustomApiKey());
                
                if (!isNewApiKey && !hasExistingApiKey)
                {
                    MessageBox.Show("Please enter the API key.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            
            // Save provider selection
            ConfigManager.SetProvider(selectedProvider);
            
            // Save Custom provider settings if applicable
            if (selectedProvider == AIProvider.Custom)
            {
                if (apiKeyTextBox != null && apiKeyTextBox.Text != "••••••••••••••••••••••••••" && !string.IsNullOrEmpty(apiKeyTextBox.Text))
                {
                    ConfigManager.SetCustomApiKey(apiKeyTextBox.Text);
                }

                if (endpointTextBox != null && !string.IsNullOrEmpty(endpointTextBox.Text))
                {
                    ConfigManager.SetCustomEndpoint(endpointTextBox.Text);
                }

                if (deploymentIdTextBox != null && !string.IsNullOrEmpty(deploymentIdTextBox.Text))
                {
                    ConfigManager.SetCustomDeploymentId(deploymentIdTextBox.Text);
                }
            }
            
            // Save GitHub Copilot preferred model if applicable
            if (selectedProvider == AIProvider.GitHubCopilot && modelComboBox != null && modelComboBox.SelectedItem != null)
            {
                ConfigManager.SetCopilotPreferredModel(modelComboBox.SelectedItem.ToString() ?? "gpt-4o");
            }

            MessageBox.Show("Settings saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void CancelButton_Click(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}