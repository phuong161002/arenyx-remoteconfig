#nullable enable

namespace Arenyx.RemoteConfig.Core
{
    /// <summary>
    /// A module that registers config binders with <see cref="IRemoteConfigHostBuilder"/>.
    /// Typically source-generated as <c>GeneratedRemoteConfigModule</c>.
    /// </summary>
    public interface IRemoteConfigModule
    {
        void Register(IRemoteConfigHostBuilder builder);
    }
}
