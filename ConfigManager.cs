using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AIPaste.Copilot;
using Newtonsoft.Json;

namespace AIPaste
{
    public enum AIProvider
    {
        NotConfigured = 0,
        GitHubCopilot = 1,
        Custom = 2
    }

    public enum ThemeMode { System = 0, Light = 1, Dark = 2 }

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
                return AIPaste.Copilot.CopilotAuth.IsSignedIn;
                
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

        // Theme
        public static ThemeMode GetThemeMode() => GetConfig().Theme;

        public static bool SetThemeMode(ThemeMode mode)
        {
            try
            {
                var config = GetConfig();
                config.Theme = mode;
                SaveConfig();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Long-lived GitHub OAuth token (ghu_) — DPAPI-encrypted at rest.
        public static string GetCopilotOAuthToken()
        {
            var enc = GetConfig().GitHubCopilot.EncryptedOAuthToken;
            if (string.IsNullOrEmpty(enc)) return string.Empty;
            try
            {
                byte[] data = Convert.FromBase64String(enc);
                byte[] dec = ProtectedData.Unprotect(data, entropy, DataProtectionScope.CurrentUser);
                return Encoding.Unicode.GetString(dec);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool SetCopilotOAuthToken(string token)
        {
            try
            {
                byte[] data = Encoding.Unicode.GetBytes(token);
                byte[] enc = ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
                GetConfig().GitHubCopilot.EncryptedOAuthToken = Convert.ToBase64String(enc);
                SaveConfig();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ClearCopilotOAuthToken()
        {
            try
            {
                GetConfig().GitHubCopilot.EncryptedOAuthToken = string.Empty;
                SaveConfig();
            }
            catch { }
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
        private static List<CopilotModel>? _cachedModels;
        private static DateTime _modelsCacheTime = DateTime.MinValue;
        private static readonly TimeSpan ModelsCacheDuration = TimeSpan.FromMinutes(5);
        
        public static async Task<List<CopilotModel>?> GetCopilotModelsAsync(bool forceRefresh = false)
        {
            // Return cached models if still valid
            if (!forceRefresh && _cachedModels != null && DateTime.Now - _modelsCacheTime < ModelsCacheDuration)
            {
                return _cachedModels;
            }

            var models = await CopilotApiClient.ListModelsAsync();
            if (models.Count > 0)
            {
                _cachedModels = models;
                _modelsCacheTime = DateTime.Now;
                return _cachedModels;
            }

            _cachedModels = null;
            return null;
        }

        public static async Task<(bool IsAuthenticated, string Message)> CheckCopilotAuthAsync()
        {
            if (!CopilotAuth.IsSignedIn)
                return (false, "Not signed in.");

            try
            {
                var models = await GetCopilotModelsAsync(forceRefresh: true);
                if (models != null && models.Count > 0)
                    return (true, "Authenticated");

                return (false, "Signed in, but no models are available for this account.");
            }
            catch (NotSignedInException)
            {
                return (false, "Sign-in expired. Please sign in again.");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        }
    }
    
    public class AppConfig
    {
        public AIProvider Provider { get; set; } = AIProvider.NotConfigured;
        public ThemeMode Theme { get; set; } = ThemeMode.System;
        public GitHubCopilotConfig GitHubCopilot { get; set; } = new GitHubCopilotConfig();
        public CustomProviderConfig CustomProvider { get; set; } = new CustomProviderConfig();
        public List<CustomAction> CustomActions { get; set; } = new List<CustomAction>();
    }
    
    public class GitHubCopilotConfig
    {
        public string PreferredModel { get; set; } = "gpt-4o";

        /// <summary>
        /// Long-lived GitHub OAuth token (ghu_), DPAPI-encrypted. Lets the user
        /// stay signed in across restarts without re-running the device flow.
        /// </summary>
        public string EncryptedOAuthToken { get; set; } = string.Empty;
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