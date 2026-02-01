using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GitHub.Copilot.SDK;
using Newtonsoft.Json;

namespace AIPaste
{
    public enum AIProvider
    {
        NotConfigured = 0,
        GitHubCopilot = 1,
        Custom = 2
    }

    public static class ConfigManager
    {
        private const string ConfigFileName = "config.json";
        private static readonly byte[] entropy = Encoding.Unicode.GetBytes("AIPaste_Secret_Entropy");
        private static string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
        
        private static AppConfig? _config;
        
        public static AppConfig GetConfig()
        {
            if (_config == null)
            {
                LoadConfig();
            }
            
            return _config ?? new AppConfig();
        }
        
        public static bool IsProviderConfigured()
        {
            var config = GetConfig();
            return config.Provider != AIProvider.NotConfigured;
        }
        
        public static bool IsConfigComplete()
        {
            var config = GetConfig();
            
            if (config.Provider == AIProvider.NotConfigured)
                return false;
                
            if (config.Provider == AIProvider.GitHubCopilot)
                return true; // CLI auth is handled externally
                
            if (config.Provider == AIProvider.Custom)
            {
                return !string.IsNullOrEmpty(GetCustomApiKey()) && 
                       !string.IsNullOrEmpty(config.CustomProvider.Endpoint) && 
                       !string.IsNullOrEmpty(config.CustomProvider.DeploymentId);
            }
            
            return false;
        }
        
        public static AIProvider GetProvider()
        {
            return GetConfig().Provider;
        }
        
        public static bool SetProvider(AIProvider provider)
        {
            try
            {
                var config = GetConfig();
                config.Provider = provider;
                SaveConfig();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        // Custom Provider methods
        public static string GetCustomApiKey()
        {
            var config = GetConfig();
            
            if (string.IsNullOrEmpty(config.CustomProvider.EncryptedApiKey))
            {
                return string.Empty;
            }

            try
            {
                byte[] encryptedData = Convert.FromBase64String(config.CustomProvider.EncryptedApiKey);
                byte[] decryptedData = ProtectedData.Unprotect(encryptedData, entropy, DataProtectionScope.CurrentUser);
                return Encoding.Unicode.GetString(decryptedData);
            }
            catch
            {
                return string.Empty;
            }
        }
        
        public static string GetCustomEndpoint()
        {
            return GetConfig().CustomProvider.Endpoint;
        }
        
        public static string GetCustomDeploymentId()
        {
            return GetConfig().CustomProvider.DeploymentId;
        }
        
        public static bool SetCustomApiKey(string apiKey)
        {
            try
            {
                var config = GetConfig();
                
                byte[] data = Encoding.Unicode.GetBytes(apiKey);
                byte[] encrypted = ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
                config.CustomProvider.EncryptedApiKey = Convert.ToBase64String(encrypted);
                
                SaveConfig();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public static bool SetCustomEndpoint(string endpoint)
        {
            try
            {
                var config = GetConfig();
                config.CustomProvider.Endpoint = endpoint;
                SaveConfig();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public static bool SetCustomDeploymentId(string deploymentId)
        {
            try
            {
                var config = GetConfig();
                config.CustomProvider.DeploymentId = deploymentId;
                SaveConfig();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        // GitHub Copilot methods
        public static string GetCopilotPreferredModel()
        {
            return GetConfig().GitHubCopilot.PreferredModel;
        }
        
        public static bool SetCopilotPreferredModel(string model)
        {
            try
            {
                var config = GetConfig();
                config.GitHubCopilot.PreferredModel = model;
                SaveConfig();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        // Custom Actions
        public static List<CustomAction> GetCustomActions()
        {
            return GetConfig().CustomActions;
        }
        
        public static bool SaveCustomAction(CustomAction action)
        {
            try
            {
                var config = GetConfig();
                
                var existingAction = config.CustomActions.FirstOrDefault(a => a.Id == action.Id);
                if (existingAction != null)
                {
                    int index = config.CustomActions.IndexOf(existingAction);
                    config.CustomActions[index] = action;
                }
                else
                {
                    if (string.IsNullOrEmpty(action.Id))
                    {
                        action.Id = Guid.NewGuid().ToString();
                    }
                    config.CustomActions.Add(action);
                }
                
                SaveConfig();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public static bool DeleteCustomAction(string actionId)
        {
            try
            {
                var config = GetConfig();
                var action = config.CustomActions.FirstOrDefault(a => a.Id == actionId);
                if (action != null)
                {
                    config.CustomActions.Remove(action);
                    SaveConfig();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private static void LoadConfig()
        {
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    _config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                    return;
                }
                catch
                {
                    // If loading fails, create a new config
                }
            }
            
            _config = new AppConfig();
        }
        
        private static void SaveConfig()
        {
            string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, json);
        }
        
        public static void ReloadConfig()
        {
            _config = null;
            LoadConfig();
        }
        
        // Centralized Copilot SDK methods
        private static List<ModelInfo>? _cachedModels;
        private static DateTime _modelsCacheTime = DateTime.MinValue;
        private static readonly TimeSpan ModelsCacheDuration = TimeSpan.FromMinutes(5);
        
        public static async Task<List<ModelInfo>?> GetCopilotModelsAsync(bool forceRefresh = false)
        {
            // Return cached models if still valid
            if (!forceRefresh && _cachedModels != null && DateTime.Now - _modelsCacheTime < ModelsCacheDuration)
            {
                return _cachedModels;
            }
            
            try
            {
                // Use singleton client manager
                var models = await CopilotClientManager.Instance.ListModelsAsync();
                
                if (models != null && models.Count > 0)
                {
                    _cachedModels = models.ToList();
                    _modelsCacheTime = DateTime.Now;
                    return _cachedModels;
                }
                
                // No models returned - clear cache
                _cachedModels = null;
                return null;
            }
            catch
            {
                // Auth failed - clear cache and reset client
                if (forceRefresh)
                {
                    _cachedModels = null;
                    await CopilotClientManager.Instance.ResetAsync();
                }
                throw;
            }
        }
        
        public static async Task<(bool IsAuthenticated, string Message)> CheckCopilotAuthAsync()
        {
            try
            {
                var models = await GetCopilotModelsAsync(forceRefresh: true);
                
                if (models != null && models.Count > 0)
                {
                    return (true, "Authenticated!");
                }
                
                return (false, "No models available. Please login first.\nRun 'copilot' and type /login");
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message.ToLower();
                if (errorMsg.Contains("not found") || errorMsg.Contains("cannot find") || errorMsg.Contains("not recognized"))
                {
                    return (false, "Copilot CLI not found.\nInstall: winget install GitHub.Copilot");
                }
                else if (errorMsg.Contains("auth") || errorMsg.Contains("login") || errorMsg.Contains("unauthorized"))
                {
                    return (false, "Not authenticated.\nRun 'copilot' and type /login");
                }
                
                return (false, $"Error: {ex.Message}");
            }
        }
    }
    
    public class AppConfig
    {
        public AIProvider Provider { get; set; } = AIProvider.NotConfigured;
        public GitHubCopilotConfig GitHubCopilot { get; set; } = new GitHubCopilotConfig();
        public CustomProviderConfig CustomProvider { get; set; } = new CustomProviderConfig();
        public List<CustomAction> CustomActions { get; set; } = new List<CustomAction>();
    }
    
    public class GitHubCopilotConfig
    {
        public string PreferredModel { get; set; } = "gpt-4o";
    }
    
    public class CustomProviderConfig
    {
        public string EncryptedApiKey { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string DeploymentId { get; set; } = string.Empty;
    }
    
    public class CustomAction
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
    }
}