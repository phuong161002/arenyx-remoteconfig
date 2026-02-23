#nullable enable

namespace Arenyx.RemoteConfig.Core.Tests.Fakes
{
    /// <summary>
    /// Fake module that registers FakeBinder (TestConfig) and FakeBinderB (TestUiConfig).
    /// </summary>
    public sealed class FakeModule : IRemoteConfigModule
    {
        public void Register(IRemoteConfigHostBuilder builder)
        {
            builder.Add(new FakeBinder());
            builder.Add(new FakeBinderB());
        }
    }

    /// <summary>
    /// Fake module that registers only FakeBinder (TestConfig).
    /// </summary>
    public sealed class FakeSingleModule : IRemoteConfigModule
    {
        public void Register(IRemoteConfigHostBuilder builder)
        {
            builder.Add(new FakeBinder());
        }
    }
}
