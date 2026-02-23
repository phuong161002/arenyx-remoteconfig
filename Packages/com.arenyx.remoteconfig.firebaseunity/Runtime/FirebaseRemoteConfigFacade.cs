#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firebase;
using Firebase.RemoteConfig;

namespace Arenyx.RemoteConfig.FirebaseUnity
{
    /// <summary>
    /// Production facade that wraps <see cref="FirebaseRemoteConfig.DefaultInstance"/>.
    /// </summary>
    internal sealed class FirebaseRemoteConfigFacade : IFirebaseRemoteConfigFacade
    {
        private FirebaseRemoteConfig? _instance;

        public async Task EnsureInitializedAsync(CancellationToken ct)
        {
            var app = await FirebaseApp.CheckAndFixDependenciesAsync().ConfigureAwait(false);
            if (app != DependencyStatus.Available)
                throw new InvalidOperationException($"Firebase dependencies not available: {app}");

            _instance = FirebaseRemoteConfig.DefaultInstance;
            await _instance.EnsureInitializedAsync();
        }

        public Task SetDefaultsAsync(IDictionary<string, object> defaults)
        {
            EnsureInstance();
            return _instance!.SetDefaultsAsync(defaults);
        }

        public Task SetConfigSettingsAsync(ConfigSettings settings)
        {
            EnsureInstance();
            return _instance!.SetConfigSettingsAsync(settings);
        }

        public Task FetchAsync(TimeSpan cacheExpiration)
        {
            EnsureInstance();
            return _instance!.FetchAsync(cacheExpiration);
        }

        public Task<bool> ActivateAsync()
        {
            EnsureInstance();
            return _instance!.ActivateAsync();
        }

        public ConfigValue GetValue(string key)
        {
            EnsureInstance();
            return _instance!.GetValue(key);
        }

        private void EnsureInstance()
        {
            if (_instance == null)
                throw new InvalidOperationException(
                    "Firebase Remote Config is not initialized. Call EnsureInitializedAsync first.");
        }
    }
}