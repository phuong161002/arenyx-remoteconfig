#nullable enable

namespace Arenyx.RemoteConfig.Core
{
    public interface IConfig<out T>
    {
        T Current { get; }

        long Version { get; }
    }
}
