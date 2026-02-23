#nullable enable
using System;

namespace Arenyx.RemoteConfig.SourceGen
{
    /// <summary>
    /// Annotates a property on a <see cref="ConfigModelAttribute"/>-marked class
    /// to bind a primitive Remote Config key (bool, long, double, or string).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class ConfigKeyAttribute : Attribute
    {
        /// <summary>Remote Config key name.</summary>
        public string Key { get; }

        // ---- Default values (set the one matching the property type) ----
        public bool DefaultBool { get; set; }
        public long DefaultLong { get; set; }
        public double DefaultDouble { get; set; }
        public string DefaultString { get; set; } = "";

        // ---- Clamp ranges (optional) ----
        public long MinLong { get; set; } = long.MinValue;
        public long MaxLong { get; set; } = long.MaxValue;
        public double MinDouble { get; set; } = double.MinValue;
        public double MaxDouble { get; set; } = double.MaxValue;

        // ---- Diagnostics hints ----
        /// <summary>If true, a missing key triggers <c>OnMissingKey</c>.</summary>
        public bool Required { get; set; }

        /// <summary>Reason string used when a value is clamped/sanitized.</summary>
        public string? SanitizeReason { get; set; }

        public ConfigKeyAttribute(string key)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
        }
    }
}
