#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firebase.RemoteConfig;

namespace Arenyx.RemoteConfig.FirebaseUnity
{
    /// <summary>
    /// Internal facade over Firebase Remote Config to allow testability
    /// without depending on <see cref="FirebaseRemoteConfig.DefaultInstance"/>.
    /// </summary>
    public interface IFirebaseRemoteConfigFacade
    {
        Task EnsureInitializedAsync(CancellationToken ct);

        Task SetDefaultsAsync(IDictionary<string, object> defaults);

        Task SetConfigSettingsAsync(ConfigSettings settings);

        Task FetchAsync(TimeSpan cacheExpiration);

        Task<bool> ActivateAsync();

        ConfigValue GetValue(string key);
    }
}
