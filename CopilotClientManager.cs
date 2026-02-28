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
        
        // Pre-warmed session support
        private CopilotSession? _warmSession;
        private string? _warmSessionModel;
        private string? _warmSessionSystemPrompt;
        private Task? _warmSessionTask;
        
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
        
        /// <summary>
        /// Pre-warms the client connection without creating a session.
        /// Call at app startup to eliminate cold-start latency on first use.
        /// </summary>
        public async Task WarmUpAsync()
        {
            try { await GetClientAsync(); } catch { }
        }
        
        /// <summary>
        /// Pre-creates a session in the background for faster first use.
        /// The session is consumed by CreateSessionAsync if the config matches.
        /// </summary>
        public void PreWarmSession(SessionConfig config)
        {
            var model = config.Model;
            var systemPrompt = config.SystemMessage?.Content;
            
            _warmSessionTask = Task.Run(async () =>
            {
                try
                {
                    var client = await GetClientAsync();
                    var session = await client.CreateSessionAsync(config);
                    _warmSession = session;
                    _warmSessionModel = model;
                    _warmSessionSystemPrompt = systemPrompt;
                }
                catch { /* pre-warm failed silently - will create on demand */ }
            });
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
        
        private async Task DisposePreWarmedSessionAsync()
        {
            if (_warmSessionTask != null)
            {
                try { await _warmSessionTask; } catch { }
                _warmSessionTask = null;
            }
            if (_warmSession != null)
            {
                try { await _warmSession.DisposeAsync(); } catch { }
                _warmSession = null;
                _warmSessionModel = null;
                _warmSessionSystemPrompt = null;
            }
        }
        
        /// <summary>
        /// Creates a new session using the managed client.
        /// Reuses a pre-warmed session if available and config matches (model + system prompt).
        /// </summary>
        public async Task<CopilotSession> CreateSessionAsync(SessionConfig config)
        {
            // Check for pre-warmed session
            if (_warmSessionTask != null)
            {
                try { await _warmSessionTask; } catch { }
                _warmSessionTask = null;
                
                if (_warmSession != null &&
                    _warmSessionModel == config.Model &&
                    _warmSessionSystemPrompt == config.SystemMessage?.Content)
                {
                    var session = _warmSession;
                    _warmSession = null;
                    _warmSessionModel = null;
                    _warmSessionSystemPrompt = null;
                    return session;
                }
                
                // Dispose mismatched pre-warmed session
                if (_warmSession != null)
                {
                    try { await _warmSession.DisposeAsync(); } catch { }
                    _warmSession = null;
                    _warmSessionModel = null;
                    _warmSessionSystemPrompt = null;
                }
            }
            
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
            await DisposePreWarmedSessionAsync();
            await DisposeClientAsync();
        }
        
        /// <summary>
        /// Resets the client (useful after logout/login).
        /// </summary>
        public async Task ResetAsync()
        {
            await DisposePreWarmedSessionAsync();
            await DisposeClientAsync();
        }
    }
}
