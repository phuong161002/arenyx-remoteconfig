#nullable enable
using Arenyx.RemoteConfig.SourceGen;

namespace Game.Config.Models
{
    /// <summary>
    /// Main gameplay configuration model.
    /// Annotated with source generator attributes — a binder will be auto-generated.
    /// </summary>
    [ConfigModel]
    public sealed record GameplayConfig
    {
        /// <summary>Feature flag: enable the new shop UI.</summary>
        [ConfigKey("ff_new_shop", DefaultBool = false)]
        public bool FfNewShop { get; init; }

        /// <summary>PvP turn timer in milliseconds (clamped 5000–60000).</summary>
        [ConfigKey("pvp_turn_time_ms", DefaultLong = 30000, MinLong = 500, MaxLong = 60000)]
        public long PvpTurnTimeMs { get; init; }

        /// <summary>Balance data parsed from JSON Remote Config value.</summary>
        [ConfigJson("balance_json_v1", DefaultJson = "{\"BaseHp\":100,\"BaseAtk\":15,\"CritMultiplier\":1.5}")]
        public BalanceConfig? Balance { get; init; }
    }
}
