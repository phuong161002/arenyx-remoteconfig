#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Arenyx.RemoteConfig.Core
{
    /// <summary>
    /// Controls the cache behavior for <see cref="IRemoteConfigHost.FetchAsync"/>.
    /// </summary>
    public enum FetchMode
    {
        /// <summary>Rely on <see cref="RemoteConfigSettings.MinimumFetchIntervalMs"/> throttling.</summary>
        Default,

        /// <summary>Dev-only: use <c>cacheExpiration = TimeSpan.Zero</c> to bypass throttle.</summary>
        Force
    }

    /// <summary>
    /// The unified entry point for Remote Config lifecycle.
    /// Manages all config stores, calls backend lifecycle methods exactly once,
    /// and provides typed access to individual configs.
    /// </summary>
    public interface IRemoteConfigHost
    {
        /// <summary>Global version shared by all configs. Incremented on each rebuild.</summary>
        long Version { get; }

        /// <summary>
        /// True when a fetch has completed but <see cref="ActivateAndRebuildAsync"/>
        /// has not yet been called. UI can use this to display an update indicator.
        /// </summary>
        bool HasPendingUpdate { get; }

        /// <summary>
        /// Retrieve a specific config by model type.
        /// Returns <see cref="IConfig{T}"/> for reading the current snapshot.
        /// </summary>
        IConfig<T> Get<T>() where T : class;

        /// <summary>
        /// Full initialization lifecycle (called once at boot):
        /// EnsureInit → merge defaults → SetDefaults → SetSettings → Activate → RebuildAll.
        /// Does NOT fetch — call <see cref="FetchAsync"/> explicitly after init.
        /// </summary>
        Task InitializeAsync(RemoteConfigSettings settings, CancellationToken ct);

        /// <summary>
        /// Fetch new values from the backend.
        /// <see cref="FetchMode.Default"/> respects the minimum fetch interval;
        /// <see cref="FetchMode.Force"/> always goes to the network.
        /// Sets <see cref="HasPendingUpdate"/> to true on success.
        /// </summary>
        Task FetchAsync(FetchMode mode, CancellationToken ct);

        /// <summary>
        /// Call at safe points (menu, before matchmaking) to apply fetched values.
        /// If activated values changed, rebuilds all config snapshots, increments Version,
        /// and clears <see cref="HasPendingUpdate"/>.
        /// Returns true if a rebuild occurred.
        /// </summary>
        Task<bool> ActivateAndRebuildAsync(CancellationToken ct);
    }
}
