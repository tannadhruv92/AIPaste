using System;
using System.Drawing;
using System.Windows.Forms;

namespace AIPaste
{
    public partial class CustomActionsForm : Form
    {
        private ListBox? actionsListBox;
        private TextBox? nameTextBox;
        private TextBox? promptTextBox;
        private Button? addButton;
        private Button? updateButton;
        private Button? deleteButton;
        private string? selectedActionId;

        public CustomActionsForm()
        {
            InitializeComponents();
            LoadCustomActions();
        }

        private void InitializeComponents()
        {
            this.Text = "Manage Custom Actions";
            this.Size = new Size(600, 500);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Actions list
            Label actionsLabel = new Label
            {
                Text = "Custom Actions:",
                Location = new Point(20, 20),
                Size = new Size(100, 20)
            };

            actionsListBox = new ListBox
            {
                Location = new Point(20, 50),
                Size = new Size(250, 350)
            };
            actionsListBox.SelectedIndexChanged += ActionsListBox_SelectedIndexChanged;

            // Action name
            Label nameLabel = new Label
            {
                Text = "Name:",
                Location = new Point(300, 50),
                Size = new Size(80, 20)
            };

            nameTextBox = new TextBox
            {
                Location = new Point(300, 75),
                Size = new Size(250, 20)
            };

            // Prompt
            Label promptLabel = new Label
            {
                Text = "Prompt:",
                Location = new Point(300, 110),
                Size = new Size(80, 20)
            };

            promptTextBox = new TextBox
            {
                Location = new Point(300, 135),
                Size = new Size(250, 150),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };

            // Help text
            Label helpLabel = new Label
            {
                Text = "Create custom prompts that can be selected from the clipboard popup menu.",
                Location = new Point(300, 300),
                Size = new Size(250, 40),
                ForeColor = Color.Gray
            };

            // Buttons
            addButton = new Button
            {
                Text = "Add",
                Location = new Point(300, 350),
                Size = new Size(80, 30)
            };
            addButton.Click += AddButton_Click;

            updateButton = new Button
            {
                Text = "Update",
                Location = new Point(390, 350),
                Size = new Size(80, 30),
                Enabled = false
            };
            updateButton.Click += UpdateButton_Click;

            deleteButton = new Button
            {
                Text = "Delete",
                Location = new Point(480, 350),
                Size = new Size(80, 30),
                Enabled = false
            };
            deleteButton.Click += DeleteButton_Click;

            Button closeButton = new Button
            {
                Text = "Close",
                Location = new Point(470, 420),
                Size = new Size(80, 30)
            };
            closeButton.Click += CloseButton_Click;

            // Add controls
            this.Controls.Add(actionsLabel);
            this.Controls.Add(actionsListBox);
            this.Controls.Add(nameLabel);
            this.Controls.Add(nameTextBox);
            this.Controls.Add(promptLabel);
            this.Controls.Add(promptTextBox);
            this.Controls.Add(helpLabel);
            this.Controls.Add(addButton);
            this.Controls.Add(updateButton);
            this.Controls.Add(deleteButton);
            this.Controls.Add(closeButton);
        }

        private void LoadCustomActions()
        {
            if (actionsListBox == null) return;
            
            actionsListBox.Items.Clear();
            
            var actions = ConfigManager.GetCustomActions();
            foreach (var action in actions)
            {
                actionsListBox.Items.Add(new CustomActionItem(action));
            }
        }

        private void ClearForm()
        {
            if (nameTextBox != null)
            {
                nameTextBox.Text = string.Empty;
            }
            
            if (promptTextBox != null)
            {
                promptTextBox.Text = string.Empty;
            }
            
            selectedActionId = null;
            
            if (updateButton != null)
            {
                updateButton.Enabled = false;
            }
            
            if (deleteButton != null)
            {
                deleteButton.Enabled = false;
            }
        }

        private void AddButton_Click(object? sender, EventArgs e)
        {
            if (nameTextBox == null || promptTextBox == null) return;
            
            if (string.IsNullOrWhiteSpace(nameTextBox.Text) || string.IsNullOrWhiteSpace(promptTextBox.Text))
            {
                MessageBox.Show("Please enter a name and prompt for the custom action.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var newAction = new CustomAction
            {
                Name = nameTextBox.Text,
                Prompt = promptTextBox.Text
            };

            if (ConfigManager.SaveCustomAction(newAction))
            {
                LoadCustomActions();
                ClearForm();
                MessageBox.Show("Custom action added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to add custom action.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateButton_Click(object? sender, EventArgs e)
        {
            if (nameTextBox == null || promptTextBox == null) return;
            
            if (string.IsNullOrWhiteSpace(selectedActionId) || 
                string.IsNullOrWhiteSpace(nameTextBox.Text) || 
                string.IsNullOrWhiteSpace(promptTextBox.Text))
            {
                MessageBox.Show("Please select an action and enter a name and prompt.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var updateAction = new CustomAction
            {
                Id = selectedActionId,
                Name = nameTextBox.Text,
                Prompt = promptTextBox.Text
            };

            if (ConfigManager.SaveCustomAction(updateAction))
            {
                LoadCustomActions();
                MessageBox.Show("Custom action updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to update custom action.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(selectedActionId))
            {
                MessageBox.Show("Please select an action to delete.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var result = MessageBox.Show("Are you sure you want to delete this custom action?", 
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                if (ConfigManager.DeleteCustomAction(selectedActionId))
                {
                    LoadCustomActions();
                    ClearForm();
                    MessageBox.Show("Custom action deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Failed to delete custom action.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void CloseButton_Click(object? sender, EventArgs e)
        {
            this.Close();
        }

        private void ActionsListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (actionsListBox?.SelectedItem is CustomActionItem selectedItem)
            {
                selectedActionId = selectedItem.Action.Id;
                
                if (nameTextBox != null)
                {
                    nameTextBox.Text = selectedItem.Action.Name;
                }
                
                if (promptTextBox != null)
                {
                    promptTextBox.Text = selectedItem.Action.Prompt;
                }
                
                if (updateButton != null)
                {
                    updateButton.Enabled = true;
                }
                
                if (deleteButton != null)
                {
                    deleteButton.Enabled = true;
                }
            }
        }
    }

    // Helper class for ListBox items
    public class CustomActionItem
    {
        public CustomAction Action { get; }

        public CustomActionItem(CustomAction action)
        {
            Action = action;
        }

        public override string ToString()
        {
            return Action.Name;
        }
    }
}