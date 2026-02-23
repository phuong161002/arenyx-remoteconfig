#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Arenyx.RemoteConfig.Core;
using NUnit.Framework;

namespace Arenyx.RemoteConfig.FirebaseUnity.Tests
{
    [TestFixture]
    public sealed class FirebaseRemoteConfigBackendTests
    {
        private FakeFirebaseRemoteConfigFacade _facade = null!;
        private FirebaseRemoteConfigBackend _backend = null!;

        [SetUp]
        public void SetUp()
        {
            _facade = new FakeFirebaseRemoteConfigFacade();
            _backend = new FirebaseRemoteConfigBackend(_facade);
        }

        // ---------------------------------------------------------------
        // 1) Correct facade method calls per Core contract
        // ---------------------------------------------------------------
        [Test]
        public async Task EnsureInitializedAsync_DelegatesToFacade()
        {
            await _backend.EnsureInitializedAsync(CancellationToken.None);
            Assert.That(_facade.CallLog, Contains.Item(nameof(_facade.EnsureInitializedAsync)));
        }

        [Test]
        public async Task SetDefaultsAsync_DelegatesToFacade()
        {
            var defaults = new Dictionary<string, object> { ["key"] = "value" };
            await _backend.SetDefaultsAsync(defaults, CancellationToken.None);
            Assert.That(_facade.CallLog, Contains.Item(nameof(_facade.SetDefaultsAsync)));
        }

        [Test]
        public async Task SetSettingsAsync_DelegatesToFacade()
        {
            var settings = new RemoteConfigSettings(5000, 3600000);
            await _backend.SetSettingsAsync(settings, CancellationToken.None);
            Assert.That(_facade.CallLog, Contains.Item(nameof(_facade.SetConfigSettingsAsync)));
        }

        [Test]
        public async Task FetchAsync_DelegatesToFacade()
        {
            await _backend.FetchAsync(TimeSpan.FromHours(1), CancellationToken.None);
            Assert.That(_facade.CallLog, Contains.Item(nameof(_facade.FetchAsync)));
        }

        [Test]
        public async Task ActivateAsync_DelegatesToFacade_ReturnsTrue()
        {
            _facade.ActivateReturns = true;
            var result = await _backend.ActivateAsync(CancellationToken.None);
            Assert.That(result, Is.True);
            Assert.That(_facade.CallLog, Contains.Item(nameof(_facade.ActivateAsync)));
        }

        [Test]
        public async Task ActivateAsync_DelegatesToFacade_ReturnsFalse()
        {
            _facade.ActivateReturns = false;
            var result = await _backend.ActivateAsync(CancellationToken.None);
            Assert.That(result, Is.False);
        }

        // ---------------------------------------------------------------
        // 2) GetValue mapping: ConfigValue → RemoteConfigValue
        // ---------------------------------------------------------------
        [Test]
        public void GetValue_MapsFromFacade_HasValueTrue()
        {
            // Since Firebase doesn't reliably expose "missing", HasValue is always true.
            var result = _backend.GetValue("any_key");

            Assert.That(result.HasValue, Is.True);
            Assert.That(_facade.CallLog, Contains.Item(nameof(_facade.GetValue)));
        }
    }
}