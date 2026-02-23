#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arenyx.RemoteConfig.Core;
using Firebase.RemoteConfig;

namespace Arenyx.RemoteConfig.FirebaseUnity
{
    /// <summary>
    /// <see cref="IRemoteConfigBackend"/> implementation that delegates to Firebase
    /// Remote Config via <see cref="IFirebaseRemoteConfigFacade"/>.
    /// </summary>
    public sealed class FirebaseRemoteConfigBackend : IRemoteConfigBackend
    {
        private readonly IFirebaseRemoteConfigFacade _facade;

        public FirebaseRemoteConfigBackend()
            : this(new FirebaseRemoteConfigFacade()) { }

        public FirebaseRemoteConfigBackend(IFirebaseRemoteConfigFacade facade)
        {
            _facade = facade ?? throw new ArgumentNullException(nameof(facade));
        }

        public Task EnsureInitializedAsync(CancellationToken ct)
            => _facade.EnsureInitializedAsync(ct);

        public Task SetDefaultsAsync(IReadOnlyDictionary<string, object> defaults, CancellationToken ct)
        {
            // Firebase SDK expects IDictionary<string, object>.
            var dict = defaults.ToDictionary(kv => kv.Key, kv => kv.Value);
            return _facade.SetDefaultsAsync(dict);
        }

        public Task SetSettingsAsync(RemoteConfigSettings settings, CancellationToken ct)
        {
            var configSettings = new ConfigSettings
            {
                FetchTimeoutInMilliseconds = (ulong)settings.FetchTimeoutMs,
                MinimumFetchIntervalInMilliseconds = (ulong)settings.MinimumFetchIntervalMs
            };
            return _facade.SetConfigSettingsAsync(configSettings);
        }

        public Task FetchAsync(TimeSpan cacheExpiration, CancellationToken ct)
            => _facade.FetchAsync(cacheExpiration);

        public async Task<bool> ActivateAsync(CancellationToken ct)
            => await _facade.ActivateAsync().ConfigureAwait(false);

        public RemoteConfigValue GetValue(string key)
        {
            var cv = _facade.GetValue(key);
            return MapValue(cv);
        }

        /// <summary>
        /// Maps Firebase <see cref="ConfigValue"/> to Core <see cref="RemoteConfigValue"/>.
        /// <para>
        /// Firebase's typed accessors (BooleanValue, LongValue, DoubleValue) all parse
        /// from the raw string and can throw FormatException / OverflowException, so each
        /// is wrapped in a try-catch.
        /// </para>
        /// <para>
        /// HasValue is false when <c>Source == ValueSource.DefaultValue</c> — that means
        /// the key was not set remotely and Firebase is returning the default we pushed via
        /// SetDefaultsAsync, so the binder should use its own compiled-in default instead.
        /// </para>
        /// </summary>
        private static RemoteConfigValue MapValue(ConfigValue cv)
        {
            // TODO: Optimize this from try catch all case to specific parse
            // If the source is our pushed default, treat as "missing" so the binder
            // falls back to its own compiled-in default and reports diagnostics.
            var hasValue = cv.Source != ValueSource.DefaultValue;

            // StringValue is just Encoding.UTF8.GetString(Data), never throws.
            var str = cv.StringValue;

            // BooleanValue uses regex — can throw FormatException for non-boolean strings.
            bool boolVal = false;
            try { boolVal = cv.BooleanValue; } catch { /* non-boolean string, leave false */ }

            // LongValue uses Convert.ToInt64 — can throw FormatException / OverflowException.
            long longVal = 0;
            try { longVal = cv.LongValue; } catch { /* non-numeric string, leave 0 */ }

            // DoubleValue uses Convert.ToDouble — can throw FormatException.
            double doubleVal = 0;
            try { doubleVal = cv.DoubleValue; } catch { /* non-numeric string, leave 0 */ }

            return RemoteConfigValue.Create(hasValue, boolVal, longVal, doubleVal, str);
        }
    }
}
