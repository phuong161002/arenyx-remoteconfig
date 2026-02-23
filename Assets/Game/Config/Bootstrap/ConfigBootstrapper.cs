#nullable enable
using System;
using System.Threading;
using Arenyx.RemoteConfig.Core;
using Arenyx.RemoteConfig.FirebaseUnity;
using Arenyx.RemoteConfig.Generated;
using Game.Config.Models;
using UnityEngine;
using RemoteConfigSettings = Arenyx.RemoteConfig.Core.RemoteConfigSettings;
using Newtonsoft.Json;

namespace Game.Config.Bootstrap
{
    /// <summary>
    /// Initializes the Remote Config host at game boot.
    /// Attach this to a GameObject in the boot scene.
    /// </summary>
    public sealed class ConfigBootstrapper : MonoBehaviour
    {
        /// <summary>
        /// The unified host. Expose for safe-point calls elsewhere.
        /// </summary>
        public static IRemoteConfigHost? Host { get; private set; }

        /// <summary>Gameplay config (new-shop flag, turn timer, balance data).</summary>
        public static IConfig<GameplayConfig>? Config { get; private set; }

        /// <summary>Live ops config (seasonal events, XP multiplier, promo offer).</summary>
        public static IConfig<LiveOpsConfig>? LiveOps { get; private set; }

        [SerializeField] private int fetchTimeoutMs = 10000;
        [SerializeField] private long minimumFetchIntervalMs = 3600000; // 1 hour

        private async void Start()
        {
            try
            {
                var backend = new FirebaseRemoteConfigBackend();
                var json = new UnityJsonCodec();
                var diag = new UnityConfigDiagnostics();

                var host = new RemoteConfigHost(backend, json, diag);
                host.AddModule(new GeneratedRemoteConfigModule());

#if UNITY_EDITOR
                var interval = 0;
#else
                var interval = minFetchIntervalMs;
#endif
                var settings = new RemoteConfigSettings(
                    fetchTimeoutMs,
                    interval);

                await host.InitializeAsync(settings, CancellationToken.None);

                Host = host;
                Config = host.Get<GameplayConfig>();
                LiveOps = host.Get<LiveOpsConfig>();

                var gp = Config.Current;
                var lo = LiveOps.Current;
                Debug.Log(
                    $"[RemoteConfig] Initialized v{host.Version} | " +
                    $"FfNewShop={gp.FfNewShop} TurnMs={gp.PvpTurnTimeMs} Balance.BaseAtk={gp.Balance?.BaseAtk} | " +
                    $"EventActive={lo.EventActive} Event={lo.EventName} " +
                    $"XP={lo.XpMultiplier}x Lobby={lo.LobbySize} " +
                    $"Promo={lo.Promo?.Title}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemoteConfig] Init failed: {ex}");
            }
        }
    }

    /// <summary>
    /// Simple IJsonCodec using Unity's JsonUtility.
    /// </summary>
    internal sealed class UnityJsonCodec : IJsonCodec
    {
        public bool TryDeserialize<T>(string json, out T? value)
        {
            try
            {
                value = JsonConvert.DeserializeObject<T>(json);
                return value != null;
            }
            catch
            {
                value = default;
                return false;
            }
        }
    }

    /// <summary>
    /// Diagnostics implementation that logs to Unity console.
    /// </summary>
    internal sealed class UnityConfigDiagnostics : IConfigDiagnostics
    {
        public void OnMissingKey(string key) =>
            Debug.LogWarning($"[RemoteConfig] Missing key: {key}");

        public void OnParseError(string key, string? raw, string error) =>
            Debug.LogError($"[RemoteConfig] Parse error for '{key}': {error} (raw: {raw})");

        public void OnSanitized(string key, string? raw, string reason) =>
            Debug.LogWarning($"[RemoteConfig] Sanitized '{key}': {reason} (raw: {raw})");
    }
}