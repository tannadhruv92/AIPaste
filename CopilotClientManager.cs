using GitHub.Copilot.SDK;

namespace AIPaste
{
    /// <summary>
    /// Singleton manager for CopilotClient to avoid creating new instances on every request.
    /// </summary>
    public sealed class CopilotClientManager : IAsyncDisposable
    {
        private static CopilotClientManager? _instance;
        private static readonly object _lock = new object();
        
        private CopilotClient? _client;
        private bool _isStarted;
        private DateTime _lastUsed;
        private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(10);
        
        private CopilotClientManager() { }
        
        public static CopilotClientManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new CopilotClientManager();
                    }
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// Gets a connected CopilotClient, reusing existing if available and healthy.
        /// Uses State property instead of pinging for faster checks.
        /// </summary>
        public async Task<CopilotClient> GetClientAsync()
        {
            // Check if we need a new client
            if (_client == null || !_isStarted)
            {
                await InitializeClientAsync();
            }
            else
            {
                // Use State property for instant health check (no network call)
                var state = _client.State;
                if (state != ConnectionState.Connected)
                {
                    await DisposeClientAsync();
                    await InitializeClientAsync();
                }
            }
            
            _lastUsed = DateTime.Now;
            return _client!;
        }
        
        private async Task InitializeClientAsync()
        {
            _client = new CopilotClient();
            await _client.StartAsync();
            _isStarted = true;
        }
        
        private async Task DisposeClientAsync()
        {
            if (_client != null)
            {
                try
                {
                    await _client.DisposeAsync();
                }
                catch { }
                _client = null;
                _isStarted = false;
            }
        }
        
        /// <summary>
        /// Creates a new session using the managed client.
        /// Note: Sessions should be disposed after use, but the client is reused.
        /// </summary>
        public async Task<CopilotSession> CreateSessionAsync(SessionConfig config)
        {
            var client = await GetClientAsync();
            return await client.CreateSessionAsync(config);
        }
        
        /// <summary>
        /// Lists available models using the managed client.
        /// </summary>
        public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync()
        {
            var client = await GetClientAsync();
            return await client.ListModelsAsync();
        }
        
        public async ValueTask DisposeAsync()
        {
            await DisposeClientAsync();
        }
        
        /// <summary>
        /// Resets the client (useful after logout/login).
        /// </summary>
        public async Task ResetAsync()
        {
            await DisposeClientAsync();
        }
    }
}
