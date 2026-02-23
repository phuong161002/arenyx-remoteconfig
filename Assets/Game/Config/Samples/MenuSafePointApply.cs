#nullable enable
using System;
using System.Threading;
using Game.Config.Bootstrap;
using Game.Config.Models;
using UnityEngine;

namespace Game.Config.Samples
{
    /// <summary>
    /// Demonstrates applying fetched config at a safe point (menu / before matchmaking).
    /// A single <see cref="Arenyx.RemoteConfig.Core.IRemoteConfigHost.ActivateAndRebuildAsync"/> call
    /// rebuilds ALL registered configs in one pass.
    /// </summary>
    public sealed class MenuSafePointApply : MonoBehaviour
    {
        private async void OnEnable()
        {
            await TryApplyUpdatesAsync();
        }

        public async System.Threading.Tasks.Task TryApplyUpdatesAsync()
        {
            var host = ConfigBootstrapper.Host;
            if (host == null) return;

            try
            {
                // Skip the backend round-trip if we know there's nothing to apply.
                if (!host.HasPendingUpdate) return;

                var changed = await host.ActivateAndRebuildAsync(CancellationToken.None);

                if (changed)
                {
                    // Both configs were rebuilt in the same pass — read them together.
                    var gp = ConfigBootstrapper.Config?.Current;
                    var lo = ConfigBootstrapper.LiveOps?.Current;

                    Debug.Log(
                        $"[RemoteConfig] Configs updated to v{host.Version} | " +
                        $"FfNewShop={gp?.FfNewShop} TurnMs={gp?.PvpTurnTimeMs} | " +
                        $"EventActive={lo?.EventActive} XP={lo?.XpMultiplier}x " +
                        $"Lobby={lo?.LobbySize} Promo={lo?.Promo?.Title}");

                    // Example: integrate with game systems that care.
                    if (lo?.EventActive == true)
                    {
                        Debug.Log($"[RemoteConfig] Seasonal event active: '{lo.EventName}' — XP x{lo.XpMultiplier}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemoteConfig] Safe-point update failed: {ex}");
            }
        }
    }
}
