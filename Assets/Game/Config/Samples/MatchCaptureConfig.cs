#nullable enable
using Game.Config.Models;
using UnityEngine;

namespace Game.Config.Samples
{
    /// <summary>
    /// Demonstrates capturing the config snapshot at match start.
    /// The captured snapshot is used for the entire match — no mid-match drift.
    /// </summary>
    public sealed class MatchCaptureConfig : MonoBehaviour
    {
        /// <summary>Captured snapshot for the current match.</summary>
        public GameplayConfig? MatchConfig { get; private set; }

        /// <summary>Call at match start to freeze the config for the match duration.</summary>
        public void CaptureForMatch()
        {
            var config = Bootstrap.ConfigBootstrapper.Config;
            if (config == null)
            {
                Debug.LogError("[RemoteConfig] Config store not initialized!");
                return;
            }

            MatchConfig = config.Current;
            Debug.Log($"[RemoteConfig] Captured config for match: TurnTime={MatchConfig.PvpTurnTimeMs}ms, NewShop={MatchConfig.FfNewShop}");
        }

        /// <summary>Example: use the captured snapshot during the match.</summary>
        public long GetTurnTimeMs()
        {
            return MatchConfig?.PvpTurnTimeMs ?? 30000;
        }
    }
}
