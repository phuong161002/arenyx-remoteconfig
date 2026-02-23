#nullable enable
using System.Collections.Generic;

namespace Arenyx.RemoteConfig.Core.Tests.Fakes
{
    /// <summary>
    /// Records all diagnostic callbacks for test assertions.
    /// </summary>
    public sealed class FakeDiagnostics : IConfigDiagnostics
    {
        public List<string> MissingKeys { get; } = new();
        public List<(string Key, string? Raw, string Error)> ParseErrors { get; } = new();
        public List<(string Key, string? Raw, string Reason)> Sanitized { get; } = new();

        public void OnMissingKey(string key) => MissingKeys.Add(key);

        public void OnParseError(string key, string? raw, string error) =>
            ParseErrors.Add((key, raw, error));

        public void OnSanitized(string key, string? raw, string reason) =>
            Sanitized.Add((key, raw, reason));
    }
}
