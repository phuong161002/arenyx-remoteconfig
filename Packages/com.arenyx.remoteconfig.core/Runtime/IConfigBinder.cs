#nullable enable
using System.Collections.Generic;

namespace Arenyx.RemoteConfig.Core
{
    public interface IConfigBinder<T>
    {
        IReadOnlyDictionary<string, object> Defaults { get; }

        T Build(IRemoteConfigBackend backend, IJsonCodec? json, IConfigDiagnostics? diag);
    }
}
