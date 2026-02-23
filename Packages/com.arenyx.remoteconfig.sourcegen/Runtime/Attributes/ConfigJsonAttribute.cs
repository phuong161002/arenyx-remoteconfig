#nullable enable
using System;

namespace Arenyx.RemoteConfig.SourceGen
{
    /// <summary>
    /// Annotates a property on a <see cref="ConfigModelAttribute"/>-marked class
    /// to bind a JSON-typed Remote Config value stored as a string.
    /// The binder will parse the string once via <c>IJsonCodec</c> during <c>Build()</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class ConfigJsonAttribute : Attribute
    {
        /// <summary>Remote Config key name.</summary>
        public string Key { get; }

        /// <summary>Default JSON string used when the key is missing or parse fails.</summary>
        public string DefaultJson { get; set; } = "{}";

        /// <summary>If true, a missing key triggers <c>OnMissingKey</c>.</summary>
        public bool Required { get; set; }

        public ConfigJsonAttribute(string key)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
        }
    }
}
