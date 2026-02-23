#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;

namespace Arenyx.RemoteConfig.Core
{
    /// <summary>
    /// Pure snapshot holder for a single config model.
    /// Does NOT own lifecycle — <see cref="RemoteConfigHost"/> calls
    /// <see cref="Rebuild"/> after activation.
    /// </summary>
    public sealed class ConfigStore<T> : IConfig<T>, IConfigStoreInternal where T : class
    {
        private readonly IConfigBinder<T> _binder;

        private T _current;
        private long _version;

        public ConfigStore(IConfigBinder<T> binder)
        {
            _binder = binder ?? throw new ArgumentNullException(nameof(binder));

            // Build an initial snapshot from pure defaults (no backend values).
            // This ensures Current is never null even before Host.InitializeAsync.
            _current = _binder.Build(new NullBackend(), null, null);
            _version = 0;
        }

        /// <summary>
        /// Returns the current immutable snapshot.
        /// Thread-safe, allocation-free, no JSON parsing.
        /// </summary>
        public T Current => Volatile.Read(ref _current);

        /// <summary>
        /// Global version set by the Host.
        /// All configs share the same version after each rebuild pass.
        /// </summary>
        public long Version => Volatile.Read(ref _version);

        // ---- IConfigStoreInternal ----

        IReadOnlyDictionary<string, object> IConfigStoreInternal.Defaults => _binder.Defaults;

        Type IConfigStoreInternal.ModelType => typeof(T);

        void IConfigStoreInternal.Rebuild(IRemoteConfigBackend backend, IJsonCodec? json, IConfigDiagnostics? diag, long globalVersion)
        {
            var snapshot = _binder.Build(backend, json, diag);
            Volatile.Write(ref _current, snapshot);
            Volatile.Write(ref _version, globalVersion);
        }

        /// <summary>
        /// Minimal backend that always returns Empty values.
        /// Used only at construction time to build a default snapshot.
        /// </summary>
        private sealed class NullBackend : IRemoteConfigBackend
        {
            public System.Threading.Tasks.Task EnsureInitializedAsync(CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task SetDefaultsAsync(IReadOnlyDictionary<string, object> defaults, CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task SetSettingsAsync(RemoteConfigSettings settings, CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task FetchAsync(TimeSpan cacheExpiration, CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task<bool> ActivateAsync(CancellationToken ct) => System.Threading.Tasks.Task.FromResult(false);
            public RemoteConfigValue GetValue(string key) => RemoteConfigValue.Empty;
        }
    }
}