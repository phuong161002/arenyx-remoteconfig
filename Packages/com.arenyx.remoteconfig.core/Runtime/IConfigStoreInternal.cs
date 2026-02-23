#nullable enable
using System;
using System.Collections.Generic;

namespace Arenyx.RemoteConfig.Core
{
    /// <summary>
    /// Internal interface that <see cref="RemoteConfigHost"/> uses to manage
    /// all config stores uniformly regardless of their model type.
    /// </summary>
    internal interface IConfigStoreInternal
    {
        /// <summary>Defaults dictionary from the binder (for merge before SetDefaultsAsync).</summary>
        IReadOnlyDictionary<string, object> Defaults { get; }

        /// <summary>The model type this store binds to.</summary>
        Type ModelType { get; }

        /// <summary>
        /// Rebuild the snapshot from current backend values.
        /// Called by Host after activation.
        /// </summary>
        void Rebuild(IRemoteConfigBackend backend, IJsonCodec? json, IConfigDiagnostics? diag, long globalVersion);
    }
}
