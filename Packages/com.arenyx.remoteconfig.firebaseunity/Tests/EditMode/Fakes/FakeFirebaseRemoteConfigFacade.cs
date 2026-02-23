#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firebase.RemoteConfig;

namespace Arenyx.RemoteConfig.FirebaseUnity.Tests
{
    /// <summary>
    /// Fake facade that records calls and returns configurable data,
    /// without requiring a real Firebase instance.
    /// </summary>
    internal sealed class FakeFirebaseRemoteConfigFacade : IFirebaseRemoteConfigFacade
    {
        public List<string> CallLog { get; } = new();
        public bool ActivateReturns { get; set; } = true;

        private readonly Dictionary<string, ConfigValue> _values = new();

        public void SetValue(string key, ConfigValue value) => _values[key] = value;

        public Task EnsureInitializedAsync(CancellationToken ct)
        {
            CallLog.Add(nameof(EnsureInitializedAsync));
            return Task.CompletedTask;
        }

        public Task SetDefaultsAsync(IDictionary<string, object> defaults)
        {
            CallLog.Add(nameof(SetDefaultsAsync));
            return Task.CompletedTask;
        }

        public Task SetConfigSettingsAsync(ConfigSettings settings)
        {
            CallLog.Add(nameof(SetConfigSettingsAsync));
            return Task.CompletedTask;
        }

        public Task FetchAsync(TimeSpan cacheExpiration)
        {
            CallLog.Add(nameof(FetchAsync));
            return Task.CompletedTask;
        }

        public Task<bool> ActivateAsync()
        {
            CallLog.Add(nameof(ActivateAsync));
            return Task.FromResult(ActivateReturns);
        }

        public ConfigValue GetValue(string key)
        {
            CallLog.Add(nameof(GetValue));
            if (_values.TryGetValue(key, out var val))
                return val;

            return new ConfigValue();
        }
    }
}
