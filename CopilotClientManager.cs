using System.Threading;
using GitHub.Copilot;

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
        private readonly SemaphoreSlim _clientInitLock = new SemaphoreSlim(1, 1);
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
            if (_client != null && _isStarted)
            {
                _lastUsed = DateTime.Now;
                return _client;
            }

            await _clientInitLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_client == null || !_isStarted)
                {
                    await InitializeClientAsync().ConfigureAwait(false);
                }

                _lastUsed = DateTime.Now;
                return _client!;
            }
            finally
            {
                _clientInitLock.Release();
            }
        }
        
        /// <summary>
        /// Pre-warms the client connection without creating a session.
        /// Call at app startup to eliminate cold-start latency on first use.
        /// </summary>
        public Task WarmUpAsync() => Task.Run(async () =>
        {
            try { await GetClientAsync().ConfigureAwait(false); } catch { }
        });
        
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
                    var client = await GetClientAsync().ConfigureAwait(false);
                    var session = await client.CreateSessionAsync(config).ConfigureAwait(false);
                    _warmSession = session;
                    _warmSessionModel = model;
                    _warmSessionSystemPrompt = systemPrompt;
                }
                catch { /* pre-warm failed silently - will create on demand */ }
            });
        }
        
        private async Task InitializeClientAsync()
        {
            if (_client != null && !_isStarted)
            {
                try { await _client.DisposeAsync().ConfigureAwait(false); } catch { }
                _client = null;
            }

            // Default CopilotClient() uses the SDK's bundled Copilot runtime — a matched
            // pair with this SDK version. We deliberately do NOT use a system-installed
            // CLI so the client and runtime protocol versions can never drift apart.
            var client = await Task.Run(async () =>
            {
                var newClient = new CopilotClient();
                try
                {
                    await newClient.StartAsync().ConfigureAwait(false);
                    return newClient;
                }
                catch
                {
                    try { await newClient.DisposeAsync().ConfigureAwait(false); } catch { }
                    throw;
                }
            }).ConfigureAwait(false);

            _client = client;
            _isStarted = true;
        }
        
        private async Task DisposeClientAsync()
        {
            await _clientInitLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_client != null)
                {
                    try
                    {
                        await _client.DisposeAsync().ConfigureAwait(false);
                    }
                    catch { }
                    _client = null;
                    _isStarted = false;
                }
            }
            finally
            {
                _clientInitLock.Release();
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
            
            var client = await GetClientAsync().ConfigureAwait(false);
            return await client.CreateSessionAsync(config).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Lists available models using the managed client.
        /// </summary>
        public async Task<IList<ModelInfo>> ListModelsAsync()
        {
            var client = await GetClientAsync().ConfigureAwait(false);
            return await client.ListModelsAsync().ConfigureAwait(false);
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
