#nullable enable

namespace Arenyx.RemoteConfig.Core
{
    public interface IJsonCodec
    {
        bool TryDeserialize<T>(string json, out T? value);
    }
}
