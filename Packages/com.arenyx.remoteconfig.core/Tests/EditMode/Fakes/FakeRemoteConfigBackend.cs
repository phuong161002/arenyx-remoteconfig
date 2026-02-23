#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Arenyx.RemoteConfig.Core.Tests.Fakes
{
    public sealed class FakeRemoteConfigBackend : IRemoteConfigBackend
    {
        private readonly Dictionary<string, RemoteConfigValue> _values = new();

        /// <summary>Records every method call name in order.</summary>
        public List<string> CallLog { get; } = new();

        /// <summary>Controls what ActivateAsync returns.</summary>
        public bool ActivateReturns { get; set; } = true;

        /// <summary>Set or override a value for a given key.</summary>
        public void SetValue(string key, RemoteConfigValue value) => _values[key] = value;

        public Task EnsureInitializedAsync(CancellationToken ct)
        {
            CallLog.Add(nameof(EnsureInitializedAsync));
            return Task.CompletedTask;
        }

        public Task SetDefaultsAsync(IReadOnlyDictionary<string, object> defaults, CancellationToken ct)
        {
            CallLog.Add(nameof(SetDefaultsAsync));
            return Task.CompletedTask;
        }

        public Task SetSettingsAsync(RemoteConfigSettings settings, CancellationToken ct)
        {
            CallLog.Add(nameof(SetSettingsAsync));
            return Task.CompletedTask;
        }

        /// <summary>The cacheExpiration passed to the most recent FetchAsync call.</summary>
        public TimeSpan LastFetchCacheExpiration { get; private set; }

        public Task FetchAsync(TimeSpan cacheExpiration, CancellationToken ct)
        {
            CallLog.Add(nameof(FetchAsync));
            LastFetchCacheExpiration = cacheExpiration;
            return Task.CompletedTask;
        }

        public Task<bool> ActivateAsync(CancellationToken ct)
        {
            CallLog.Add(nameof(ActivateAsync));
            return Task.FromResult(ActivateReturns);
        }

        public RemoteConfigValue GetValue(string key)
        {
            if (_values.TryGetValue(key, out var val))
                return val;

            return RemoteConfigValue.Empty;
        }
    }
}
