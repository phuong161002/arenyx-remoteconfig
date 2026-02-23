#nullable enable
using Arenyx.RemoteConfig.SourceGen;

namespace Game.Config.Models
{
    /// <summary>
    /// Live operations configuration model.
    /// Controls seasonal events, matchmaking tuning, and promo offers.
    /// Annotated with source generator attributes — a binder will be auto-generated.
    /// </summary>
    [ConfigModel]
    public sealed record LiveOpsConfig
    {
        /// <summary>Whether a seasonal event is currently running.</summary>
        [ConfigKey("liveops_event_active", DefaultBool = false)]
        public bool EventActive { get; init; }

        /// <summary>Display name of the active seasonal event (empty = no event).</summary>
        [ConfigKey("liveops_event_name", DefaultString = "")]
        public string EventName { get; init; } = "";

        /// <summary>XP multiplier applied during events (clamped 1.0–5.0).</summary>
        [ConfigKey("liveops_xp_multiplier", DefaultDouble = 1.0, MinDouble = 1.0, MaxDouble = 5.0)]
        public double XpMultiplier { get; init; }

        /// <summary>Maximum players per matchmaking lobby during events (clamped 2–20).</summary>
        [ConfigKey("liveops_lobby_size", DefaultLong = 8, MinLong = 2, MaxLong = 20)]
        public long LobbySize { get; init; }

        /// <summary>Active promotional offer, parsed from JSON.</summary>
        [ConfigJson("liveops_promo_json",
            DefaultJson = "{\"Title\":\"default\",\"DiscountPercent\":0,\"IsActive\":false}")]
        public PromoConfig? Promo { get; init; }
    }
}
