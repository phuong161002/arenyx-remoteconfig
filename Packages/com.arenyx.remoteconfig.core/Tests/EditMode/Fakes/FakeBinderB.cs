#nullable enable
using System.Collections.Generic;

namespace Arenyx.RemoteConfig.Core.Tests.Fakes
{
    /// <summary>
    /// A minimal second config model for multi-config Host tests.
    /// </summary>
    public sealed record TestUiConfig
    {
        public bool DarkMode { get; init; }
        public string Theme { get; init; } = "";
    }

    /// <summary>
    /// Second fake binder to test Host with multiple configs.
    /// </summary>
    public sealed class FakeBinderB : IConfigBinder<TestUiConfig>
    {
        public const string KeyDarkMode = "ui_dark_mode";
        public const string KeyTheme = "ui_theme";

        public IReadOnlyDictionary<string, object> Defaults { get; } = new Dictionary<string, object>
        {
            [KeyDarkMode] = false,
            [KeyTheme] = "light"
        };

        public TestUiConfig Build(IRemoteConfigBackend backend, IJsonCodec? json, IConfigDiagnostics? diag)
        {
            var darkVal = backend.GetValue(KeyDarkMode);
            var dark = darkVal.HasValue ? darkVal.Bool : (bool)Defaults[KeyDarkMode];
            if (!darkVal.HasValue) diag?.OnMissingKey(KeyDarkMode);

            var themeVal = backend.GetValue(KeyTheme);
            var theme = themeVal.HasValue ? themeVal.String : (string)Defaults[KeyTheme];
            if (!themeVal.HasValue) diag?.OnMissingKey(KeyTheme);

            return new TestUiConfig
            {
                DarkMode = dark,
                Theme = theme
            };
        }
    }
}
