#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Arenyx.RemoteConfig.Core
{
    /// <summary>
    /// Unified lifecycle host for all Remote Config stores.
    /// Calls backend lifecycle methods exactly once, regardless of how many configs are registered.
    /// </summary>
    public sealed class RemoteConfigHost : IRemoteConfigHost, IRemoteConfigHostBuilder
    {
        private readonly IRemoteConfigBackend _backend;
        private readonly IJsonCodec? _json;
        private readonly IConfigDiagnostics? _diag;

        private readonly List<IConfigStoreInternal> _stores = new();
        private readonly Dictionary<Type, object> _storesByType = new();

        private RemoteConfigSettings _settings;
        private long _globalVersion;
        private bool _initialized;
        private volatile bool _hasPendingUpdate;

        public RemoteConfigHost(
            IRemoteConfigBackend backend,
            IJsonCodec? json = null,
            IConfigDiagnostics? diag = null)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _json = json;
            _diag = diag;
        }

        /// <summary>Global version shared by all configs.</summary>
        public long Version => Volatile.Read(ref _globalVersion);

        /// <summary>
        /// True when a fetch has completed but <see cref="ActivateAndRebuildAsync"/>
        /// has not yet applied it. UI can show an update indicator.
        /// </summary>
        public bool HasPendingUpdate => _hasPendingUpdate;

        // ---- Module registration ----

        /// <summary>
        /// Registers all binders from a module.
        /// Must be called before <see cref="InitializeAsync"/>.
        /// </summary>
        public void AddModule(IRemoteConfigModule module)
        {
            if (_initialized)
                throw new InvalidOperationException("Cannot add modules after initialization.");

            module.Register(this);
        }

        // ---- IRemoteConfigHostBuilder ----

        void IRemoteConfigHostBuilder.Add<T>(IConfigBinder<T> binder)
        {
            if (_initialized)
                throw new InvalidOperationException("Cannot add binders after initialization.");

            var modelType = typeof(T);
            if (_storesByType.ContainsKey(modelType))
                throw new InvalidOperationException($"A binder for '{modelType.Name}' is already registered.");

            var store = new ConfigStore<T>(binder);
            _stores.Add(store);
            _storesByType[modelType] = store;
        }

        // ---- IRemoteConfigHost ----

        /// <summary>
        /// Retrieve a specific config by model type.
        /// </summary>
        public IConfig<T> Get<T>() where T : class
        {
            if (_storesByType.TryGetValue(typeof(T), out var store))
                return (IConfig<T>)store;

            throw new InvalidOperationException(
                $"No config registered for type '{typeof(T).Name}'. " +
                "Did you forget to call AddModule()?");
        }

        /// <summary>
        /// Full initialization lifecycle (called once at boot):
        /// 1) EnsureInit
        /// 2) Merge defaults from all stores → SetDefaults (once)
        /// 3) SetSettings (once)
        /// 4) Activate (once — apply cached values from previous session)
        /// 5) RebuildAll
        /// Does NOT fetch — call <see cref="FetchAsync"/> explicitly.
        /// </summary>
        public async Task InitializeAsync(
            RemoteConfigSettings settings,
            CancellationToken ct)
        {
            if (_initialized)
                throw new InvalidOperationException("Host is already initialized.");

            if (_stores.Count == 0)
                throw new InvalidOperationException("No config modules registered. Call AddModule() first.");

            _settings = settings;

            // 1. Ensure backend is ready.
            await _backend.EnsureInitializedAsync(ct).ConfigureAwait(false);

            // 2. Merge defaults from all stores and push once.
            var merged = MergeDefaults();
            await _backend.SetDefaultsAsync(merged, ct).ConfigureAwait(false);

            // 3. Apply settings once.
            await _backend.SetSettingsAsync(settings, ct).ConfigureAwait(false);

            // 4. Activate cached values from previous session (once).
            await _backend.ActivateAsync(ct).ConfigureAwait(false);

            // 5. Rebuild all snapshots.
            RebuildAll();

            _initialized = true;
        }

        /// <summary>
        /// Fetch new values from the backend.
        /// <see cref="FetchMode.Default"/> uses the configured MinimumFetchInterval;
        /// <see cref="FetchMode.Force"/> bypasses throttle (dev builds).
        /// Sets <see cref="HasPendingUpdate"/> to true on completion.
        /// </summary>
        public async Task FetchAsync(FetchMode mode, CancellationToken ct)
        {
            var cacheExpiration = mode == FetchMode.Force
                ? TimeSpan.Zero
                : TimeSpan.FromMilliseconds(_settings.MinimumFetchIntervalMs);

            await _backend.FetchAsync(cacheExpiration, ct).ConfigureAwait(false);
            _hasPendingUpdate = true;
        }

        /// <summary>
        /// Call at safe points (menu, before matchmaking) to apply fetched values.
        /// Calls ActivateAsync once. If changed, rebuilds ALL configs in one pass,
        /// increments Version, and clears <see cref="HasPendingUpdate"/>.
        /// Returns true if a rebuild occurred.
        /// </summary>
        public async Task<bool> ActivateAndRebuildAsync(CancellationToken ct)
        {
            var changed = await _backend.ActivateAsync(ct).ConfigureAwait(false);
            if (changed)
            {
                RebuildAll();
            }

            _hasPendingUpdate = false;
            return changed;
        }

        // ---- Private helpers ----

        private void RebuildAll()
        {
            var version = Interlocked.Increment(ref _globalVersion);

            foreach (var store in _stores)
            {
                store.Rebuild(_backend, _json, _diag, version);
            }
        }

        /// <summary>
        /// Merges defaults from all registered stores.
        /// Throws if the same key has conflicting values across binders.
        /// </summary>
        private Dictionary<string, object> MergeDefaults()
        {
            var merged = new Dictionary<string, object>();

            foreach (var store in _stores)
            {
                foreach (var kvp in store.Defaults)
                {
                    if (merged.TryGetValue(kvp.Key, out var existing))
                    {
                        // Same key — check for conflict.
                        if (!Equals(existing, kvp.Value))
                        {
                            throw new InvalidOperationException(
                                $"Default conflict for key '{kvp.Key}': " +
                                $"'{existing}' ({existing?.GetType().Name}) vs " +
                                $"'{kvp.Value}' ({kvp.Value?.GetType().Name}). " +
                                $"Two binders define the same key with different defaults.");
                        }
                        // Same value — OK, skip.
                    }
                    else
                    {
                        merged[kvp.Key] = kvp.Value;
                    }
                }
            }

            return merged;
        }
    }
}
