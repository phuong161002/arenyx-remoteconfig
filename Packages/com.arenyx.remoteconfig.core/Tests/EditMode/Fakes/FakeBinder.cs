#nullable enable
using System.Collections.Generic;

namespace Arenyx.RemoteConfig.Core.Tests.Fakes
{
    /// <summary>
    /// A test model used by FakeBinder.
    /// Immutable snapshot with primitive + JSON-typed fields.
    /// </summary>
    public sealed record TestConfig
    {
        public bool FeatureFlag { get; init; }
        public long TurnTimeMs { get; init; }
        public double Multiplier { get; init; }
        public string Label { get; init; } = "";
        public TestBalanceConfig? Balance { get; init; }
    }

    public sealed record TestBalanceConfig
    {
        public int Hp { get; init; }
        public int Atk { get; init; }
    }

    /// <summary>
    /// Fake binder that reads known keys from the backend and applies clamp + JSON logic
    /// so we can test ConfigStore lifecycle end-to-end.
    /// </summary>
    public sealed class FakeBinder : IConfigBinder<TestConfig>
    {
        public const string KeyFeatureFlag = "ff_test";
        public const string KeyTurnTimeMs = "turn_time_ms";
        public const string KeyMultiplier = "multiplier";
        public const string KeyLabel = "label";
        public const string KeyBalanceJson = "balance_json";

        public const long MinTurnTime = 5000;
        public const long MaxTurnTime = 60000;

        private static readonly TestBalanceConfig DefaultBalance = new() { Hp = 100, Atk = 10 };

        public IReadOnlyDictionary<string, object> Defaults { get; } = new Dictionary<string, object>
        {
            [KeyFeatureFlag] = false,
            [KeyTurnTimeMs] = 30000L,
            [KeyMultiplier] = 1.0,
            [KeyLabel] = "default",
            [KeyBalanceJson] = "{\"Hp\":100,\"Atk\":10}"
        };

        public TestConfig Build(IRemoteConfigBackend backend, IJsonCodec? json, IConfigDiagnostics? diag)
        {
            // --- Feature flag ---
            var ffVal = backend.GetValue(KeyFeatureFlag);
            var ff = ffVal.HasValue ? ffVal.Bool : (bool)Defaults[KeyFeatureFlag];
            if (!ffVal.HasValue) diag?.OnMissingKey(KeyFeatureFlag);

            // --- Turn time (clamped) ---
            var ttVal = backend.GetValue(KeyTurnTimeMs);
            long tt;
            if (ttVal.HasValue)
            {
                tt = ttVal.Long;
                if (tt < MinTurnTime)
                {
                    diag?.OnSanitized(KeyTurnTimeMs, tt.ToString(), $"Clamped to min {MinTurnTime}");
                    tt = MinTurnTime;
                }
                else if (tt > MaxTurnTime)
                {
                    diag?.OnSanitized(KeyTurnTimeMs, tt.ToString(), $"Clamped to max {MaxTurnTime}");
                    tt = MaxTurnTime;
                }
            }
            else
            {
                tt = (long)Defaults[KeyTurnTimeMs];
                diag?.OnMissingKey(KeyTurnTimeMs);
            }

            // --- Multiplier ---
            var mulVal = backend.GetValue(KeyMultiplier);
            var mul = mulVal.HasValue ? mulVal.Double : (double)Defaults[KeyMultiplier];
            if (!mulVal.HasValue) diag?.OnMissingKey(KeyMultiplier);

            // --- Label ---
            var lblVal = backend.GetValue(KeyLabel);
            var lbl = lblVal.HasValue ? lblVal.String : (string)Defaults[KeyLabel];
            if (!lblVal.HasValue) diag?.OnMissingKey(KeyLabel);

            // --- Balance JSON ---
            var balVal = backend.GetValue(KeyBalanceJson);
            TestBalanceConfig? balance;
            if (balVal.HasValue && json != null)
            {
                if (!json.TryDeserialize<TestBalanceConfig>(balVal.String, out var parsed))
                {
                    diag?.OnParseError(KeyBalanceJson, balVal.String, "Failed to deserialize TestBalanceConfig");
                    balance = DefaultBalance;
                }
                else
                {
                    balance = parsed ?? DefaultBalance;
                }
            }
            else
            {
                if (!balVal.HasValue) diag?.OnMissingKey(KeyBalanceJson);
                balance = DefaultBalance;
            }

            return new TestConfig
            {
                FeatureFlag = ff,
                TurnTimeMs = tt,
                Multiplier = mul,
                Label = lbl,
                Balance = balance
            };
        }
    }
}
