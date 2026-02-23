#nullable enable

namespace Game.Config.Models
{
    /// <summary>
    /// Typed JSON config for game balance data.
    /// Deserialized once during binder Build() — callers never parse JSON.
    /// </summary>
    public sealed record BalanceConfig
    {
        public int BaseHp { get; init; }
        public int BaseAtk { get; init; }
        public double CritMultiplier { get; init; }
    }
}