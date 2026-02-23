#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Arenyx.RemoteConfig.Core.Tests.Fakes
{
    /// <summary>
    /// Minimal JSON codec for use in EditMode tests.
    /// Avoids System.Text.Json (not available in Unity) and UnityEngine.JsonUtility
    /// (Core tests assembly has noEngineReferences = true).
    ///
    /// Handles flat objects with primitive fields (bool, int, long, double, string).
    /// Sufficient for TestBalanceConfig and similar simple test models.
    /// </summary>
    public sealed class FakeJsonCodec : IJsonCodec
    {
        /// <summary>When true, TryDeserialize always reports failure.</summary>
        public bool ForceFailure { get; set; }

        public bool TryDeserialize<T>(string json, out T? value)
        {
            value = default;

            if (ForceFailure)
                return false;

            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                var parsed = ParseFlatObject(json);
                if (parsed == null)
                    return false;

                // Create instance via parameterless constructor or record positional ctor.
                var instance = Activator.CreateInstance<T>();

                foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanWrite) continue;

                    if (!parsed.TryGetValue(prop.Name, out var rawVal))
                        continue;

                    var converted = ConvertValue(rawVal, prop.PropertyType);
                    if (converted != null)
                        prop.SetValue(instance, converted);
                }

                value = instance;
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        // ---- Internals ----

        /// <summary>
        /// Extracts key/value pairs from a flat JSON object string.
        /// e.g. {"Hp":100,"Atk":10} → {"Hp":"100","Atk":"10"}
        /// </summary>
        private static Dictionary<string, string>? ParseFlatObject(string json)
        {
            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                return null;

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Match "Key": value  where value is a number, bool, or quoted string.
            var pattern = new Regex(
                @"""(?<key>[^""]+)""\s*:\s*(?<val>""[^""]*""|true|false|-?\d+(?:\.\d+)?)",
                RegexOptions.IgnoreCase);

            foreach (Match m in pattern.Matches(json))
            {
                var key = m.Groups["key"].Value;
                var val = m.Groups["val"].Value;
                result[key] = val;
            }

            return result;
        }

        private static object? ConvertValue(string raw, Type targetType)
        {
            // Strip surrounding quotes for strings.
            if (raw.StartsWith("\"") && raw.EndsWith("\""))
                raw = raw.Substring(1, raw.Length - 2);

            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlying == typeof(bool))
                return bool.TryParse(raw, out var b) ? b : (object?)null;
            if (underlying == typeof(int))
                return int.TryParse(raw, out var i) ? i : (object?)null;
            if (underlying == typeof(long))
                return long.TryParse(raw, out var l) ? l : (object?)null;
            if (underlying == typeof(float))
                return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : (object?)null;
            if (underlying == typeof(double))
                return double.TryParse(raw, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (object?)null;
            if (underlying == typeof(string))
                return raw;

            return null;
        }
    }
}
