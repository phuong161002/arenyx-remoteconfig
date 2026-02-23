#nullable enable
namespace Arenyx.RemoteConfig.Core
{
    public struct RemoteConfigValue
    {
        public bool HasValue { get; private set; }
        public bool Bool { get; private set; }
        public long Long { get; private set; }
        public double Double { get; private set; }
        public string String { get; private set; }

        public static RemoteConfigValue Empty { get; } = new();

        /// <summary>Creates a value with all fields populated (used by backend adapters).</summary>
        public static RemoteConfigValue Create(bool hasValue, bool boolVal, long longVal, double doubleVal, string stringVal)
            => new() { HasValue = hasValue, Bool = boolVal, Long = longVal, Double = doubleVal, String = stringVal };

        public static RemoteConfigValue BoolValue(bool value) => new() { HasValue = true, Bool = value };
        public static RemoteConfigValue LongValue(long value) => new() { HasValue = true, Long = value };
        public static RemoteConfigValue DoubleValue(double value) => new() { HasValue = true, Double = value };
        public static RemoteConfigValue StringValue(string value) => new() { HasValue = true, String = value };
    }
}