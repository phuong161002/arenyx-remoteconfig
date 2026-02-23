#nullable enable

namespace Arenyx.RemoteConfig.Core
{
    /// <summary>
    /// Builder interface used by <see cref="IRemoteConfigModule"/> to register binders.
    /// </summary>
    public interface IRemoteConfigHostBuilder
    {
        /// <summary>
        /// Adds a binder for config model <typeparamref name="T"/>.
        /// The host will create and manage a <see cref="ConfigStore{T}"/> for this binder.
        /// </summary>
        void Add<T>(IConfigBinder<T> binder) where T : class;
    }
}
