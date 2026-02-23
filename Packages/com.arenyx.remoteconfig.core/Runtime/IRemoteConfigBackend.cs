#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Arenyx.RemoteConfig.Core
{
    public interface IRemoteConfigBackend
    {
        Task EnsureInitializedAsync(CancellationToken ct);

        Task SetDefaultsAsync(IReadOnlyDictionary<string, object> defaults, CancellationToken ct);

        Task SetSettingsAsync(RemoteConfigSettings settings, CancellationToken ct);

        Task FetchAsync(TimeSpan cacheExpiration, CancellationToken ct);

        /// <summary>Return true if activated values changed and a rebuild is needed.</summary>
        Task<bool> ActivateAsync(CancellationToken ct);

        RemoteConfigValue GetValue(string key);
    }

    public readonly struct RemoteConfigSettings
    {
        public int FetchTimeoutMs { get; }
        public long MinimumFetchIntervalMs { get; }

        public RemoteConfigSettings(int fetchTimeoutMs, long minimumFetchIntervalMs)
        {
            FetchTimeoutMs = fetchTimeoutMs;
            MinimumFetchIntervalMs = minimumFetchIntervalMs;
        }
    }
}