#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arenyx.RemoteConfig.Core.Tests.Fakes;
using NUnit.Framework;

namespace Arenyx.RemoteConfig.Core.Tests
{
    [TestFixture]
    public sealed class RemoteConfigHostTests
    {
        private FakeRemoteConfigBackend _backend = null!;
        private FakeJsonCodec _json = null!;
        private FakeDiagnostics _diag = null!;

        private static readonly RemoteConfigSettings DefaultSettings = new(5000, 3600000);

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeRemoteConfigBackend();
            _json = new FakeJsonCodec();
            _diag = new FakeDiagnostics();
        }

        // ---------------------------------------------------------------
        // 1) Host init order — backend calls once, correct sequence
        // ---------------------------------------------------------------
        [Test]
        public async Task InitializeAsync_CallsBackendInCorrectOrder()
        {
            SeedAllValues();
            var host = CreateHost(new FakeSingleModule());
            await host.InitializeAsync(DefaultSettings, CancellationToken.None);

            var expected = new[]
            {
                nameof(IRemoteConfigBackend.EnsureInitializedAsync),
                nameof(IRemoteConfigBackend.SetDefaultsAsync),
                nameof(IRemoteConfigBackend.SetSettingsAsync),
                nameof(IRemoteConfigBackend.ActivateAsync),
                // RebuildAll happens here (not a backend call).
                // FetchAsync is NOT called — it's now explicit.
            };

            CollectionAssert.AreEqual(expected, _backend.CallLog);
        }

        // ---------------------------------------------------------------
        // 2) N configs do not call Activate N times
        // ---------------------------------------------------------------
        [Test]
        public async Task InitializeAsync_WithTwoConfigs_ActivateCalledOnce()
        {
            SeedAllValues();
            SeedUiValues();

            var host = CreateHost(new FakeModule()); // registers 2 binders
            await host.InitializeAsync(DefaultSettings,  CancellationToken.None);

            var activateCount = _backend.CallLog.Count(c => c == nameof(IRemoteConfigBackend.ActivateAsync));
            Assert.That(activateCount, Is.EqualTo(1), "ActivateAsync should be called exactly once regardless of config count");
        }

        // ---------------------------------------------------------------
        // 3) Global version sync — both configs share same version
        // ---------------------------------------------------------------
        [Test]
        public async Task GlobalVersion_IsSyncedAcrossConfigs()
        {
            SeedAllValues();
            SeedUiValues();

            var host = CreateHost(new FakeModule());
            await host.InitializeAsync(DefaultSettings,  CancellationToken.None);

            var configA = host.Get<TestConfig>();
            var configB = host.Get<TestUiConfig>();

            Assert.That(configA.Version, Is.EqualTo(configB.Version),
                "All configs must share the same global version after init");
            Assert.That(host.Version, Is.EqualTo(configA.Version));
        }

        [Test]
        public async Task GlobalVersion_IncreasesTogetherOnSafePoint()
        {
            SeedAllValues();
            SeedUiValues();

            var host = CreateHost(new FakeModule());
            await host.InitializeAsync(DefaultSettings,  CancellationToken.None);

            var versionBefore = host.Version;

            _backend.ActivateReturns = true;
            await host.ActivateAndRebuildAsync(CancellationToken.None);

            var configA = host.Get<TestConfig>();
            var configB = host.Get<TestUiConfig>();

            Assert.That(configA.Version, Is.GreaterThan(versionBefore));
            Assert.That(configA.Version, Is.EqualTo(configB.Version));
            Assert.That(host.Version, Is.EqualTo(configA.Version));
        }

        // ---------------------------------------------------------------
        // 4) Defaults merge + conflict
        // ---------------------------------------------------------------
        [Test]
        public void Binder_Defaults_ContainExpectedKeys()
        {
            var binder = new FakeBinder();
            var keys = binder.Defaults.Keys.ToList();

            Assert.That(keys, Has.Member(FakeBinder.KeyFeatureFlag));
            Assert.That(keys, Has.Member(FakeBinder.KeyTurnTimeMs));
            Assert.That(keys, Has.Member(FakeBinder.KeyMultiplier));
            Assert.That(keys, Has.Member(FakeBinder.KeyLabel));
            Assert.That(keys, Has.Member(FakeBinder.KeyBalanceJson));

            Assert.That(binder.Defaults[FakeBinder.KeyFeatureFlag], Is.TypeOf<bool>());
            Assert.That(binder.Defaults[FakeBinder.KeyTurnTimeMs], Is.TypeOf<long>());
            Assert.That(binder.Defaults[FakeBinder.KeyMultiplier], Is.TypeOf<double>());
            Assert.That(binder.Defaults[FakeBinder.KeyLabel], Is.TypeOf<string>());
            Assert.That(binder.Defaults[FakeBinder.KeyBalanceJson], Is.TypeOf<string>());
        }

        [Test]
        public void DefaultsMerge_ConflictingKeys_Throws()
        {
            // Create a binder with a conflicting default for an existing key.
            var conflicting = new ConflictingBinder();

            var host = new RemoteConfigHost(_backend, _json, _diag);
            host.AddModule(new FakeSingleModule());

            // Adding a module with a conflicting key should throw on init (during merge).
            host.AddModule(new ModuleOf(conflicting));

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await host.InitializeAsync(DefaultSettings,  CancellationToken.None));
        }

        // ---------------------------------------------------------------
        // 5) Rebuild-all — changed=false: no-op
        // ---------------------------------------------------------------
        [Test]
        public async Task ActivateAndRebuild_False_ReturnsFalseAndVersionUnchanged()
        {
            SeedAllValues();
            var host = CreateHost(new FakeSingleModule());
            await host.InitializeAsync(DefaultSettings,  CancellationToken.None);

            var configA = host.Get<TestConfig>();
            var versionBefore = configA.Version;
            var snapshotBefore = configA.Current;

            _backend.ActivateReturns = false;
            var changed = await host.ActivateAndRebuildAsync(CancellationToken.None);

            Assert.That(changed, Is.False);
            Assert.That(configA.Version, Is.EqualTo(versionBefore));
            Assert.That(configA.Current, Is.SameAs(snapshotBefore));
        }

        // ---------------------------------------------------------------
        // 5b) Rebuild-all — changed=true: version increments, snapshots replaced
        // ---------------------------------------------------------------
        [Test]
        public async Task ActivateAndRebuild_True_ReturnsTrueAndAllSnapshotsReplaced()
        {
            SeedAllValues();
            SeedUiValues();

            var host = CreateHost(new FakeModule());
            await host.InitializeAsync(DefaultSettings,  CancellationToken.None);

            var configA = host.Get<TestConfig>();
            var configB = host.Get<TestUiConfig>();
            var snapshotA = configA.Current;
            var snapshotB = configB.Current;
            var versionBefore = host.Version;

            _backend.ActivateReturns = true;
            var changed = await host.ActivateAndRebuildAsync(CancellationToken.None);

            Assert.That(changed, Is.True);
            Assert.That(host.Version, Is.GreaterThan(versionBefore));
            // Snapshots are records (reference types) — new instances after rebuild.
            Assert.That(configA.Current, Is.Not.SameAs(snapshotA));
            Assert.That(configB.Current, Is.Not.SameAs(snapshotB));
        }

        // ---------------------------------------------------------------
        // 6) Missing key fallback
        // ---------------------------------------------------------------
        [Test]
        public async Task Build_MissingKey_UsesDefaultAndReportsDiagnostic()
        {
            // Backend has NO values → every key is missing.
            var host = CreateHost(new FakeSingleModule());
            await host.InitializeAsync(DefaultSettings,  CancellationToken.None);

            var cfg = host.Get<TestConfig>().Current;

            Assert.That(cfg.FeatureFlag, Is.EqualTo(false));
            Assert.That(cfg.TurnTimeMs, Is.EqualTo(30000L));
            Assert.That(cfg.Multiplier, Is.EqualTo(1.0));
            Assert.That(cfg.Label, Is.EqualTo("default"));

            Assert.That(_diag.MissingKeys, Has.Member(FakeBinder.KeyFeatureFlag));
            Assert.That(_diag.MissingKeys, Has.Member(FakeBinder.KeyTurnTimeMs));
            Assert.That(_diag.MissingKeys, Has.Member(FakeBinder.KeyLabel));
        }

        // ---------------------------------------------------------------
        // 7) Clamp / Sanitize
        // ---------------------------------------------------------------
        [Test]
        public async Task Build_OutOfRangeLow_IsClamped()
        {
            _backend.SetValue(FakeBinder.KeyTurnTimeMs, RemoteConfigValue.LongValue(1000));
            SeedOtherValues();

            var host = CreateHost(new FakeSingleModule());
            await host.InitializeAsync(DefaultSettings,  CancellationToken.None);

            Assert.That(host.Get<TestConfig>().Current.TurnTimeMs, Is.EqualTo(FakeBinder.MinTurnTime));
            Assert.That(_diag.Sanitized.Any(s => s.Key == FakeBinder.KeyTurnTimeMs), Is.True);
        }

        [Test]
        public async Task Build_OutOfRangeHigh_IsClamped()
        {
            _backend.SetValue(FakeBinder.KeyTurnTimeMs, RemoteConfigValue.LongValue(999999));
            SeedOtherValues();

            var host = CreateHost(new FakeSingleModule());
            await host.InitializeAsync(DefaultSettings,  CancellationToken.None);

            Assert.That(host.Get<TestConfig>().Current.TurnTimeMs, Is.EqualTo(FakeBinder.MaxTurnTime));
            Assert.That(_diag.Sanitized.Any(s => s.Key == FakeBinder.KeyTurnTimeMs), Is.True);
        }

        // ---------------------------------------------------------------
        // 8) JSON parse success
        // ---------------------------------------------------------------
        [Test]
        public async Task Build_ValidJson_ParsesCorrectly()
        {
            SeedAllValues();
            _backend.SetValue(FakeBinder.KeyBalanceJson,
                RemoteConfigValue.StringValue("{\"Hp\":200,\"Atk\":25}"));

            var host = CreateHost(new FakeSingleModule());
            await host.InitializeAsync(DefaultSettings,  CancellationToken.None);

            var cfg = host.Get<TestConfig>().Current;
            Assert.That(cfg.Balance, Is.Not.Null);
            Assert.That(cfg.Balance!.Hp, Is.EqualTo(200));
            Assert.That(cfg.Balance!.Atk, Is.EqualTo(25));
            Assert.That(_diag.ParseErrors.Count, Is.EqualTo(0));
        }

        // ---------------------------------------------------------------
        // 9) JSON parse failure
        // ---------------------------------------------------------------
        [Test]
        public async Task Build_InvalidJson_FallsBackAndReportsError()
        {
            SeedAllValues();
            _backend.SetValue(FakeBinder.KeyBalanceJson, RemoteConfigValue.StringValue("NOT_JSON"));

            var host = CreateHost(new FakeSingleModule());
            await host.InitializeAsync(DefaultSettings,  CancellationToken.None);

            var cfg = host.Get<TestConfig>().Current;
            Assert.That(cfg.Balance, Is.Not.Null);
            Assert.That(cfg.Balance!.Hp, Is.EqualTo(100));
            Assert.That(cfg.Balance!.Atk, Is.EqualTo(10));

            Assert.That(_diag.ParseErrors.Count, Is.EqualTo(1));
            Assert.That(_diag.ParseErrors[0].Key, Is.EqualTo(FakeBinder.KeyBalanceJson));
        }

        // ---------------------------------------------------------------
        // 10) Get<T> throws for unregistered type
        // ---------------------------------------------------------------
        [Test]
        public async Task Get_UnregisteredType_Throws()
        {
            SeedAllValues();
            var host = CreateHost(new FakeSingleModule());
            await host.InitializeAsync(DefaultSettings,  CancellationToken.None);

            Assert.Throws<InvalidOperationException>(() => host.Get<TestUiConfig>());
        }

        // ---------------------------------------------------------------
        // 11) FetchAsync — Default vs Force cache expiration
        // ---------------------------------------------------------------
        [Test]
        public async Task FetchAsync_Default_UsesCacheInterval()
        {
            SeedAllValues();
            var host = CreateHost(new FakeSingleModule());
            await host.InitializeAsync(DefaultSettings, CancellationToken.None);

            _backend.CallLog.Clear();
            await host.FetchAsync(FetchMode.Default, CancellationToken.None);

            Assert.That(_backend.CallLog, Has.Exactly(1).EqualTo(nameof(IRemoteConfigBackend.FetchAsync)));
            Assert.That(_backend.LastFetchCacheExpiration,
                Is.EqualTo(TimeSpan.FromMilliseconds(DefaultSettings.MinimumFetchIntervalMs)));
        }

        [Test]
        public async Task FetchAsync_Force_UsesCacheExpirationZero()
        {
            SeedAllValues();
            var host = CreateHost(new FakeSingleModule());
            await host.InitializeAsync(DefaultSettings, CancellationToken.None);

            _backend.CallLog.Clear();
            await host.FetchAsync(FetchMode.Force, CancellationToken.None);

            Assert.That(_backend.LastFetchCacheExpiration, Is.EqualTo(TimeSpan.Zero));
        }

        // ---------------------------------------------------------------
        // 12) HasPendingUpdate lifecycle
        // ---------------------------------------------------------------
        [Test]
        public async Task HasPendingUpdate_FalseAfterInit()
        {
            SeedAllValues();
            var host = CreateHost(new FakeSingleModule());
            await host.InitializeAsync(DefaultSettings, CancellationToken.None);

            Assert.That(host.HasPendingUpdate, Is.False);
        }

        [Test]
        public async Task HasPendingUpdate_TrueAfterFetch()
        {
            SeedAllValues();
            var host = CreateHost(new FakeSingleModule());
            await host.InitializeAsync(DefaultSettings, CancellationToken.None);

            await host.FetchAsync(FetchMode.Default, CancellationToken.None);
            Assert.That(host.HasPendingUpdate, Is.True);
        }

        [Test]
        public async Task HasPendingUpdate_ClearedAfterActivate()
        {
            SeedAllValues();
            var host = CreateHost(new FakeSingleModule());
            await host.InitializeAsync(DefaultSettings, CancellationToken.None);

            await host.FetchAsync(FetchMode.Default, CancellationToken.None);
            Assert.That(host.HasPendingUpdate, Is.True);

            _backend.ActivateReturns = true;
            await host.ActivateAndRebuildAsync(CancellationToken.None);
            Assert.That(host.HasPendingUpdate, Is.False);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private RemoteConfigHost CreateHost(IRemoteConfigModule module)
        {
            var host = new RemoteConfigHost(_backend, _json, _diag);
            host.AddModule(module);
            return host;
        }

        private void SeedAllValues()
        {
            _backend.SetValue(FakeBinder.KeyFeatureFlag,  RemoteConfigValue.BoolValue(true));
            _backend.SetValue(FakeBinder.KeyTurnTimeMs,   RemoteConfigValue.LongValue(15000));
            _backend.SetValue(FakeBinder.KeyMultiplier,   RemoteConfigValue.DoubleValue(1.5));
            _backend.SetValue(FakeBinder.KeyLabel,        RemoteConfigValue.StringValue("live"));
            _backend.SetValue(FakeBinder.KeyBalanceJson,  RemoteConfigValue.StringValue("{\"Hp\":100,\"Atk\":10}"));
        }

        private void SeedUiValues()
        {
            _backend.SetValue(FakeBinderB.KeyDarkMode, RemoteConfigValue.BoolValue(true));
            _backend.SetValue(FakeBinderB.KeyTheme,    RemoteConfigValue.StringValue("dark"));
        }

        /// <summary>Seeds all values except TurnTimeMs (for clamp tests).</summary>
        private void SeedOtherValues()
        {
            _backend.SetValue(FakeBinder.KeyFeatureFlag, RemoteConfigValue.BoolValue(true));
            _backend.SetValue(FakeBinder.KeyMultiplier,  RemoteConfigValue.DoubleValue(1.5));
            _backend.SetValue(FakeBinder.KeyLabel,       RemoteConfigValue.StringValue("live"));
            _backend.SetValue(FakeBinder.KeyBalanceJson, RemoteConfigValue.StringValue("{\"Hp\":100,\"Atk\":10}"));
        }

        // ---- Test helpers for conflict detection ----

        /// <summary>Binder that defines the same key as FakeBinder but with a different default.</summary>
        private sealed class ConflictingBinder : IConfigBinder<TestConflictConfig>
        {
            public IReadOnlyDictionary<string, object> Defaults { get; } = new Dictionary<string, object>
            {
                // Same key as FakeBinder.KeyFeatureFlag, different default value.
                [FakeBinder.KeyFeatureFlag] = true // FakeBinder has false
            };

            public TestConflictConfig Build(IRemoteConfigBackend backend, IJsonCodec? json, IConfigDiagnostics? diag)
                => new();
        }

        private sealed record TestConflictConfig;

        /// <summary>Wraps a single binder into a module.</summary>
        private sealed class ModuleOf : IRemoteConfigModule
        {
            private readonly IConfigBinder<TestConflictConfig> _binder;
            public ModuleOf(IConfigBinder<TestConflictConfig> binder) => _binder = binder;
            public void Register(IRemoteConfigHostBuilder builder) => builder.Add(_binder);
        }
    }
}
