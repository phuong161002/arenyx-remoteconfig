#nullable enable

namespace Arenyx.RemoteConfig.Core
{
    public interface IConfigDiagnostics
    {
        void OnMissingKey(string key);

        void OnParseError(string key, string? raw, string error);

        void OnSanitized(string key, string? raw, string reason);
    }
}
