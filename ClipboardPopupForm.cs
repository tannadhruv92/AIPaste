using Azure;
using Azure.AI.OpenAI;
using GitHub.Copilot.SDK;
using System.Drawing;
using System.Windows.Forms;

namespace AIPaste
{
    public partial class ClipboardPopupForm : Form
    {
        private string originalText;
        private string? processedText;
        private TextBox? originalTextBox;
        private TextBox? resultTextBox;
        private Button? acceptButton;
        private Button? retryButton;
        private Button? cancelButton;
        private ComboBox? toneSelector;
        private ComboBox? translationSelector;
        private ComboBox? customActionSelector;
        private ComboBox? modelSelector;
        private Label? modelLabel;
        private Label? loadingLabel;

        public ClipboardPopupForm(string clipboardText)
        {
            originalText = clipboardText;
            InitializeComponents();
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            
            this.Shown += async (s, e) => {
                var processButton = this.Controls.OfType<Button>().FirstOrDefault(b => b.Text == "Process");
                processButton?.Focus();
                
                // Load models after form is shown (ensures handle is created)
                if (ConfigManager.GetProvider() == AIProvider.GitHubCopilot)
                {
                    await LoadModelsAsync();
                    PreWarmDefaultSession();
                }
            };
        }

        private void InitializeComponents()
        {
            this.Text = "AIPaste";
            this.Size = new Size(600, 530);
            this.BackColor = Color.WhiteSmoke;

            // Original text box
            originalTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(20, 20),
                Size = new Size(560, 100),
                Text = originalText,
                Font = new Font("Segoe UI", 11)
            };
            
            // Custom Actions selector
            Label customActionLabel = new Label
            {
                Text = "Custom Action:",
                Location = new Point(20, 130),
                Size = new Size(100, 20)
            };

            customActionSelector = new ComboBox
            {
                Location = new Point(120, 130),
                Size = new Size(180, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            LoadCustomActions();
            customActionSelector.SelectedIndexChanged += CustomActionSelector_SelectedIndexChanged;

            // Tone selector
            Label toneLabel = new Label
            {
                Text = "Tone:",
                Location = new Point(20, 160),
                Size = new Size(50, 20)
            };

            toneSelector = new ComboBox
            {
                Location = new Point(120, 160),
                Size = new Size(180, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            toneSelector.Items.AddRange(new[] { "Professional", "Informative", "Casual", "Enthusiastic" });
            toneSelector.SelectedIndex = 0;

            // Translation selector
            Label translationLabel = new Label
            {
                Text = "Translate:",
                Location = new Point(320, 160),
                Size = new Size(70, 20)
            };

            translationSelector = new ComboBox
            {
                Location = new Point(400, 160),
                Size = new Size(180, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            translationSelector.Items.AddRange(new[] { "None", "Gujarati", "Hindi" });
            translationSelector.SelectedIndex = 0;

            // Model selector (visible only for GitHub Copilot)
            modelLabel = new Label
            {
                Text = "Model:",
                Location = new Point(20, 190),
                Size = new Size(50, 20)
            };

            modelSelector = new ComboBox
            {
                Location = new Point(120, 190),
                Size = new Size(250, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            
            // Show/hide model selector based on provider
            bool isGitHubCopilot = ConfigManager.GetProvider() == AIProvider.GitHubCopilot;
            modelLabel.Visible = isGitHubCopilot;
            modelSelector.Visible = isGitHubCopilot;
            
            // Set loading placeholder for GitHub Copilot (actual load happens in Shown event)
            if (isGitHubCopilot)
            {
                modelSelector.Items.Add("Loading models...");
                modelSelector.SelectedIndex = 0;
            }

            // Process button
            Button processButton = new Button
            {
                Text = "Process",
                Location = new Point(490, 130),
                Size = new Size(90, 25)
            };
            processButton.Click += ProcessButton_Click;

            // Loading label
            loadingLabel = new Label
            {
                Text = "Processing...",
                Location = new Point(20, 220),
                Size = new Size(200, 20),
                ForeColor = Color.Gray,
                Visible = false
            };

            // Result text box
            resultTextBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(20, 245),
                Size = new Size(560, 195),
                ReadOnly = true,
                Font = new Font("Segoe UI", 11)
            };

            // Buttons
            acceptButton = new Button
            {
                Text = "Accept",
                Location = new Point(310, 450),
                Size = new Size(80, 30),
                Enabled = false
            };
            acceptButton.Click += AcceptButton_Click;

            retryButton = new Button
            {
                Text = "Retry",
                Location = new Point(400, 450),
                Size = new Size(80, 30)
            };
            retryButton.Click += RetryButton_Click;

            cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(490, 450),
                Size = new Size(80, 30)
            };
            cancelButton.Click += CancelButton_Click;

            // Add controls
            this.Controls.Add(originalTextBox);
            this.Controls.Add(customActionLabel);
            this.Controls.Add(customActionSelector);
            this.Controls.Add(toneLabel);
            this.Controls.Add(toneSelector);
            this.Controls.Add(translationLabel);
            this.Controls.Add(translationSelector);
            this.Controls.Add(modelLabel);
            this.Controls.Add(modelSelector);
            this.Controls.Add(processButton);
            this.Controls.Add(loadingLabel);
            this.Controls.Add(resultTextBox);
            this.Controls.Add(acceptButton);
            this.Controls.Add(retryButton);
            this.Controls.Add(cancelButton);

            this.KeyPreview = true;
            this.KeyDown += ClipboardPopupForm_KeyDown;

            AddFormStyle();
        }
        
        private void LoadCustomActions()
        {
            if (customActionSelector == null) return;
            
            customActionSelector.Items.Clear();
            customActionSelector.Items.Add("None");
            
            var actions = ConfigManager.GetCustomActions();
            foreach (var action in actions)
            {
                customActionSelector.Items.Add(new CustomActionItem(action));
            }
            
            if (customActionSelector.Items.Count > 0)
            {
                customActionSelector.SelectedIndex = 0;
            }
        }
        
        private async Task LoadModelsAsync()
        {
            try
            {
                var models = await ConfigManager.GetCopilotModelsAsync();
                
                if (models != null && models.Count > 0 && modelSelector != null)
                {
                    modelSelector.Items.Clear();
                    foreach (var model in models.OrderBy(m => m.Name))
                    {
                        modelSelector.Items.Add(model.Id);
                    }
                    
                    // Select preferred model or first available
                    string preferredModel = ConfigManager.GetCopilotPreferredModel();
                    int modelIndex = modelSelector.Items.IndexOf(preferredModel);
                    modelSelector.SelectedIndex = modelIndex >= 0 ? modelIndex : 0;
                }
            }
            catch
            {
                if (modelSelector != null)
                {
                    modelSelector.Items.Clear();
                    modelSelector.Items.Add("Error loading models");
                    modelSelector.SelectedIndex = 0;
                }
            }
        }
        
        private void CustomActionSelector_SelectedIndexChanged(object? sender, EventArgs e)
        {
            bool useCustomAction = customActionSelector?.SelectedIndex > 0;
            
            if (toneSelector != null)
                toneSelector.Enabled = !useCustomAction;
            
            if (translationSelector != null)
                translationSelector.Enabled = !useCustomAction;
        }

        private void AddFormStyle()
        {
            Panel borderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            this.Controls.Add(borderPanel);
            borderPanel.SendToBack();
        }

        private async void ProcessButton_Click(object? sender, EventArgs e)
        {
            if (loadingLabel != null)
            {
                loadingLabel.Visible = true;
                loadingLabel.Refresh();
            }

            bool useCustomAction = customActionSelector?.SelectedIndex > 0;
            CustomAction? selectedCustomAction = null;
            
            if (useCustomAction && customActionSelector?.SelectedItem is CustomActionItem customActionItem)
            {
                selectedCustomAction = customActionItem.Action;
            }
            else if (!useCustomAction && (toneSelector?.SelectedItem == null || translationSelector?.SelectedItem == null))
            {
                MessageBox.Show("Please select a tone and translation option", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (loadingLabel != null) loadingLabel.Visible = false;
                return;
            }

            if (!ConfigManager.IsConfigComplete())
            {
                var result = MessageBox.Show(
                    "AI provider configuration is incomplete. Would you like to configure it now?", 
                    "Configuration Required", 
                    MessageBoxButtons.YesNo, 
                    MessageBoxIcon.Information);
                
                if (result == DialogResult.Yes)
                {
                    var configForm = new ConfigurationForm();
                    configForm.ShowDialog();
                    
                    if (!ConfigManager.IsConfigComplete())
                    {
                        if (resultTextBox != null)
                            resultTextBox.Text = "Processing canceled. Please complete the configuration.";
                        if (loadingLabel != null) loadingLabel.Visible = false;
                        return;
                    }
                }
                else
                {
                    if (resultTextBox != null)
                        resultTextBox.Text = "Processing canceled. Please complete the configuration.";
                    if (loadingLabel != null) loadingLabel.Visible = false;
                    return;
                }
            }

            string selectedTone = toneSelector?.SelectedItem?.ToString() ?? "Professional";
            string selectedTranslation = translationSelector?.SelectedItem?.ToString() ?? "None";
            string selectedModel = modelSelector?.SelectedItem?.ToString() ?? "gpt-4o";
            
            // Note: Model selection here is per-request only, not saved to config
            // User can change default model from Configuration popup
            
            try
            {
                AIProvider provider = ConfigManager.GetProvider();
                
                if (useCustomAction && selectedCustomAction != null)
                {
                    if (provider == AIProvider.GitHubCopilot)
                    {
                        processedText = await ProcessCustomActionWithCopilotAsync(originalText, selectedCustomAction, selectedModel);
                    }
                    else
                    {
                        processedText = ProcessCustomActionWithCustomProvider(originalText, selectedCustomAction);
                    }
                }
                else
                {
                    if (provider == AIProvider.GitHubCopilot)
                    {
                        processedText = await ProcessWithCopilotAsync(originalText, selectedTone, selectedTranslation, selectedModel);
                    }
                    else
                    {
                        processedText = ProcessWithCustomProvider(originalText, selectedTone, selectedTranslation);
                    }
                }
                
                if (resultTextBox != null)
                    resultTextBox.Text = processedText;
                    
                if (acceptButton != null)
                {
                    acceptButton.Enabled = true;
                    acceptButton.Focus();
                }
            }
            catch (Exception ex)
            {
                if (resultTextBox != null)
                    resultTextBox.Text = $"Error processing text: {ex.Message}";
                    
                if (acceptButton != null)
                    acceptButton.Enabled = false;
            }
            finally
            {
                if (loadingLabel != null) loadingLabel.Visible = false;
            }
        }

        #region GitHub Copilot SDK Methods

        /// <summary>
        /// Pre-creates a session with default settings (rewrite mode) while the user picks options.
        /// </summary>
        private void PreWarmDefaultSession()
        {
            string model = modelSelector?.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(model) || model == "Loading models..." || model == "Error loading models")
                return;
            
            CopilotClientManager.Instance.PreWarmSession(new SessionConfig
            {
                Model = model,
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

        private async Task<string> ProcessWithCopilotAsync(string text, string tone, string translation, string model)
        {
            string systemPrompt = "You are a helpful assistant that rewrites text based on the specified tone and translation requirements. Only return the rewritten text, nothing else.";
            string userPrompt = $"Rewrite the following text";
            
            if (tone != "None")
                userPrompt += $" in a {tone} tone";
            
            if (translation != "None")
                userPrompt += $" and translate it to {translation}";
            
            userPrompt += $":\n\n\"{text}\"";
            
            return await ExecuteCopilotRequestAsync(systemPrompt, userPrompt, model);
        }

        private async Task<string> ProcessCustomActionWithCopilotAsync(string text, CustomAction customAction, string model)
        {
            string userPrompt = customAction.Prompt.Replace("{text}", text);
            if (!customAction.Prompt.Contains("{text}"))
            {
                userPrompt += $"\n\nText to process: \"{text}\"";
            }
            
            string systemPrompt = "You are a helpful assistant that processes text based on the given instructions. Only return the processed result, nothing else.";
            
            return await ExecuteCopilotRequestAsync(systemPrompt, userPrompt, model);
        }

        private async Task<string> ExecuteCopilotRequestAsync(string systemPrompt, string userPrompt, string model)
        {
            // Clear result area for streaming output
            if (resultTextBox != null && !resultTextBox.IsDisposed)
                resultTextBox.Text = string.Empty;
            
            await using var session = await CopilotClientManager.Instance.CreateSessionAsync(new SessionConfig
            {
                Model = model,
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Replace,
                    Content = systemPrompt
                },
                AvailableTools = new List<string>(),
                OnPermissionRequest = PermissionHandler.ApproveAll,
                Streaming = true
            });
            
            // Stream response chunks into the result textbox as they arrive
            var result = new System.Text.StringBuilder();
            var done = new TaskCompletionSource();
            
            session.On(evt =>
            {
                if (evt is AssistantMessageDeltaEvent delta && !string.IsNullOrEmpty(delta.Data?.DeltaContent))
                {
                    result.Append(delta.Data.DeltaContent);
                    if (resultTextBox != null && !resultTextBox.IsDisposed)
                    {
                        resultTextBox.Invoke(() =>
                        {
                            resultTextBox.Text = result.ToString();
                            resultTextBox.SelectionStart = resultTextBox.Text.Length;
                            resultTextBox.Refresh();
                            if (loadingLabel != null) loadingLabel.Visible = false;
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
            
            return result.ToString();
        }

        #endregion

        #region Custom Provider Methods

        private string ProcessWithCustomProvider(string text, string tone, string translation)
        {
            string apiKey = ConfigManager.GetCustomApiKey();
            string endpoint = ConfigManager.GetCustomEndpoint();
            
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint))
            {
                throw new InvalidOperationException("API key or endpoint not configured. Please set them in the configuration.");
            }

            OpenAIClient client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            
            string systemPrompt = "You are a helpful assistant that rewrites text based on the specified tone and translation requirements.";
            string userPrompt = $"Rewrite the following text";
            
            if (tone != "None")
                userPrompt += $" in a {tone} tone";
            
            if (translation != "None")
                userPrompt += $" and translate it to {translation}";
            
            userPrompt += $":\n\n\"{text}\"";
            
            string deploymentId = ConfigManager.GetCustomDeploymentId();
            
            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                DeploymentName = deploymentId,
                Messages =
                {
                    new ChatMessage(ChatRole.System, systemPrompt),
                    new ChatMessage(ChatRole.User, userPrompt)
                }
            };
            
            Response<ChatCompletions> response = client.GetChatCompletions(chatCompletionsOptions);
            return response.Value.Choices[0].Message.Content;
        }

        private string ProcessCustomActionWithCustomProvider(string text, CustomAction customAction)
        {
            string apiKey = ConfigManager.GetCustomApiKey();
            string endpoint = ConfigManager.GetCustomEndpoint();
            
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint))
            {
                throw new InvalidOperationException("API key or endpoint not configured. Please set them in the configuration.");
            }

            OpenAIClient client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            
            string userPrompt = customAction.Prompt.Replace("{text}", text);
            if (!customAction.Prompt.Contains("{text}"))
            {
                userPrompt += $"\n\nText to process: \"{text}\"";
            }
            
            string systemPrompt = "You are a helpful assistant that processes text based on the given instructions.";
            string deploymentId = ConfigManager.GetCustomDeploymentId();
            
            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                DeploymentName = deploymentId,
                Messages =
                {
                    new ChatMessage(ChatRole.System, systemPrompt),
                    new ChatMessage(ChatRole.User, userPrompt)
                }
            };
            
            Response<ChatCompletions> response = client.GetChatCompletions(chatCompletionsOptions);
            return response.Value.Choices[0].Message.Content;
        }

        #endregion

        private void AcceptButton_Click(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(processedText))
            {
                Clipboard.SetText(processedText);
            }
            this.Close();
        }

        private void RetryButton_Click(object? sender, EventArgs e)
        {
            if (resultTextBox != null)
                resultTextBox.Text = string.Empty;
            
            if (acceptButton != null)
                acceptButton.Enabled = false;
            
            processedText = null;
            
            bool useCustomAction = customActionSelector?.SelectedIndex > 0;
            if (useCustomAction)
                customActionSelector?.Focus();
            else
                toneSelector?.Focus();
            
            var processButton = this.Controls.OfType<Button>().FirstOrDefault(b => b.Text == "Process");
            if (processButton != null)
                this.AcceptButton = processButton;
        }

        private void CancelButton_Click(object? sender, EventArgs e)
        {
            this.Close();
        }

        private void ClipboardPopupForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Down || keyData == Keys.Tab)
            {
                SelectNextControl(ActiveControl, true, true, true, true);
                return true;
            }
            else if (keyData == (Keys.Shift | Keys.Tab) || keyData == Keys.Up)
            {
                SelectNextControl(ActiveControl, false, true, true, true);
                return true;
            }
            
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}