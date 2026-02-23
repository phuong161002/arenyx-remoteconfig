#nullable enable

namespace Game.Config.Models
{
    /// <summary>
    /// Typed JSON sub-config for a live promotional offer.
    /// Deserialized once per rebuild — callers read typed properties directly.
    /// </summary>
    public sealed record PromoConfig
    {
        public string Title { get; init; } = "";
        public int DiscountPercent { get; init; } = 0;
        public bool IsActive { get; init; } = false;
    }
}
