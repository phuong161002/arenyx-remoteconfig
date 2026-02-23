# Arenyx Remote Config SDK for Unity

[![Unity 6000.0+](https://img.shields.io/badge/Unity-6000.0%2B-black?logo=unity)](https://unity.com)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A **strongly-typed, backend-agnostic** Remote Config SDK for Unity games.  
Define your configs as plain C# records, annotate properties with attributes, and the source generator produces type-safe binders **and** a registration module — zero boilerplate, zero runtime JSON parsing on reads.

```csharp
// 1. Define your config models
[ConfigModel]
public sealed record GameplayConfig
{
    [ConfigKey("ff_new_shop", DefaultBool = false)]
    public bool FfNewShop { get; init; }

    [ConfigKey("pvp_turn_time_ms", DefaultLong = 30000, MinLong = 5000, MaxLong = 60000)]
    public long PvpTurnTimeMs { get; init; }

    [ConfigJson("balance_json_v1", DefaultJson = "{\"BaseHp\":100,\"BaseAtk\":10}")]
    public BalanceConfig? Balance { get; init; }
}

[ConfigModel]
public sealed record LiveOpsConfig
{
    [ConfigKey("liveops_event_active", DefaultBool = false)]
    public bool EventActive { get; init; }

    [ConfigKey("liveops_xp_multiplier", DefaultDouble = 1.0, MinDouble = 1.0, MaxDouble = 5.0)]
    public double XpMultiplier { get; init; }
}

// 2. Read config anywhere — immutable, allocation-free, no JSON parsing
var gp = host.Get<GameplayConfig>().Current;
var lo = host.Get<LiveOpsConfig>().Current;
if (gp.FfNewShop) ShowNewShop();
if (lo.EventActive) ApplyXpBoost(lo.XpMultiplier);
```

---

## ✨ Features

- **Unified Host** — `RemoteConfigHost` manages all config stores. Backend lifecycle (`Init → SetDefaults → Activate → Fetch`) is called **exactly once**, regardless of how many config models you register.
- **Typed snapshots** — `host.Get<T>().Current` returns an immutable record. No dictionaries, no casts, no magic strings at call sites.
- **Source-generated binders + module** — Roslyn generates `IConfigBinder<T>` for each `[ConfigModel]` and a `GeneratedRemoteConfigModule` that auto-registers all binders. No manual wiring.
- **Global version sync** — All configs share one version number. After a safe-point rebuild, `configA.Version == configB.Version` is guaranteed.
- **Backend-agnostic Core** — `com.arenyx.remoteconfig.core` has zero Unity/Firebase dependencies. Swap backends freely.
- **Firebase adapter included** — `com.arenyx.remoteconfig.firebaseunity` wraps Firebase Remote Config with a testable facade pattern.
- **Safe-point policy** — Apply fetched values only at safe points (menu, lobby). One call rebuilds **all** configs atomically.
- **Thread-safe reads** — `ConfigStore.Current` uses `Volatile.Read` for atomic, allocation-free access.
- **Defaults merge with conflict detection** — If two models define the same key with different defaults, the Host throws at init so you catch mistakes early.
- **Diagnostics built-in** — Missing keys, out-of-range clamping, and JSON parse errors are reported via `IConfigDiagnostics`.
- **Fully testable** — Fakes included for every interface. No Firebase instance needed in tests.

---

## 📦 Packages

| Package | Purpose | Dependencies |
|---------|---------|-------------|
| `com.arenyx.remoteconfig.core` | Backend-agnostic runtime: Host, interfaces, `ConfigStore<T>`, diagnostics | None |
| `com.arenyx.remoteconfig.firebaseunity` | Firebase Remote Config adapter | Core, Firebase SDK |
| `com.arenyx.remoteconfig.sourcegen` | Attributes + Roslyn source generator | Core |

### Architecture

```
┌──────────────────────────────────────────────────────────┐
│                        Game Code                         │
│  GameplayConfig  LiveOpsConfig  ConfigBootstrapper        │
│           ▼ reads IConfig<T>.Current                     │
├──────────────────────────────────────────────────────────┤
│               Source Generator (compile-time)             │
│  [ConfigModel] → GameplayConfigBinder.g.cs               │
│  [ConfigModel] → LiveOpsConfigBinder.g.cs                │
│               → GeneratedRemoteConfigModule.g.cs         │
├──────────────────────────────────────────────────────────┤
│                      Core Runtime                         │
│  RemoteConfigHost  IRemoteConfigHost  IConfig<T>         │
│  ConfigStore<T>    IConfigBinder<T>   IRemoteConfigModule │
│  IRemoteConfigBackend  IJsonCodec  IConfigDiagnostics    │
├──────────────────────────────────────────────────────────┤
│                 FirebaseUnity Adapter                     │
│  FirebaseRemoteConfigBackend ← IRemoteConfigBackend      │
│  FirebaseRemoteConfigFacade  (wraps Firebase SDK)        │
└──────────────────────────────────────────────────────────┘
```

---

## 🚀 Getting Started

### Prerequisites

- **Unity 6000.0+** (Unity 6)
- **Firebase Unity SDK** installed and configured (`google-services.json` / `GoogleService-Info.plist`)

### Installation

#### Option A — Git URL (recommended)

In Unity, go to **Window → Package Manager → "+" → Add package from git URL** and add the following URLs **in order**:

```
https://github.com/phuong161002/Arenyx.RemoteConfig.git?path=Packages/com.arenyx.remoteconfig.core
```
```
https://github.com/phuong161002/Arenyx.RemoteConfig.git?path=Packages/com.arenyx.remoteconfig.sourcegen
```
```
https://github.com/phuong161002/Arenyx.RemoteConfig.git?path=Packages/com.arenyx.remoteconfig.firebaseunity
```

Or edit `Packages/manifest.json` directly:

```json
{
  "dependencies": {
    "com.arenyx.remoteconfig.core": "https://github.com/phuong161002/Arenyx.RemoteConfig.git?path=Packages/com.arenyx.remoteconfig.core#v1.0.0",
    "com.arenyx.remoteconfig.sourcegen": "https://github.com/phuong161002/Arenyx.RemoteConfig.git?path=Packages/com.arenyx.remoteconfig.sourcegen#v1.0.0",
    "com.arenyx.remoteconfig.firebaseunity": "https://github.com/phuong161002/Arenyx.RemoteConfig.git?path=Packages/com.arenyx.remoteconfig.firebaseunity#v1.0.0"
  }
}
```

> **Pin a version** by appending `#v1.0.0` (tag) or `#abc1234` (commit hash) to the URL.
>
> **Install order matters** — `core` must be added before `sourcegen` and `firebaseunity` since they depend on it. Unity does not auto-resolve transitive git dependencies.
>
> **Private repos** require SSH keys or a Git credential helper configured on your machine.

#### Option B — Embedded packages

Clone or copy the package folders into your project's `Packages/` directory:

```
Packages/
  com.arenyx.remoteconfig.core/
  com.arenyx.remoteconfig.sourcegen/
  com.arenyx.remoteconfig.firebaseunity/
```

Unity will auto-detect the embedded UPM packages. No `manifest.json` edits needed.

### 1. Define Config Models

Create as many `[ConfigModel]` records as you need. Each model groups related config keys:

```csharp
using Arenyx.RemoteConfig.SourceGen;

[ConfigModel]
public sealed record GameplayConfig
{
    [ConfigKey("ff_new_shop", DefaultBool = false)]
    public bool FfNewShop { get; init; }

    [ConfigKey("pvp_turn_time_ms", DefaultLong = 30000, MinLong = 5000, MaxLong = 60000)]
    public long PvpTurnTimeMs { get; init; }

    [ConfigJson("balance_json_v1", DefaultJson = "{\"BaseHp\":100,\"BaseAtk\":10}")]
    public BalanceConfig? Balance { get; init; }
}

[ConfigModel]
public sealed record LiveOpsConfig
{
    [ConfigKey("liveops_event_active", DefaultBool = false)]
    public bool EventActive { get; init; }

    [ConfigKey("liveops_event_name", DefaultString = "")]
    public string EventName { get; init; } = "";

    [ConfigKey("liveops_xp_multiplier", DefaultDouble = 1.0, MinDouble = 1.0, MaxDouble = 5.0)]
    public double XpMultiplier { get; init; }

    [ConfigKey("liveops_lobby_size", DefaultLong = 8, MinLong = 2, MaxLong = 20)]
    public long LobbySize { get; init; }

    [ConfigJson("liveops_promo_json",
        DefaultJson = "{\"Title\":\"\",\"DiscountPercent\":0,\"IsActive\":false}")]
    public PromoConfig? Promo { get; init; }
}
```

The source generator automatically produces:
- `GameplayConfigBinder.g.cs` — binder for GameplayConfig
- `LiveOpsConfigBinder.g.cs` — binder for LiveOpsConfig
- `GeneratedRemoteConfigModule.g.cs` — registers both binders

### 2. Bootstrap at Startup

```csharp
using Arenyx.RemoteConfig.Core;
using Arenyx.RemoteConfig.FirebaseUnity;
using Arenyx.RemoteConfig.Generated;

public class ConfigBootstrapper : MonoBehaviour
{
    public static IRemoteConfigHost? Host { get; private set; }
    public static IConfig<GameplayConfig>? Config { get; private set; }
    public static IConfig<LiveOpsConfig>? LiveOps { get; private set; }

    private async void Start()
    {
        var backend = new FirebaseRemoteConfigBackend();
        var json    = new UnityJsonCodec();        // IJsonCodec → JsonUtility
        var diag    = new UnityConfigDiagnostics(); // IConfigDiagnostics → Debug.Log

        // Create the host and register all generated binders in one call.
        var host = new RemoteConfigHost(backend, json, diag);
        host.AddModule(new GeneratedRemoteConfigModule());

        var settings = new RemoteConfigSettings(
            fetchTimeoutMs: 10000,
            minimumFetchIntervalMs: 3600000);

        // One call initializes ALL configs:
        // EnsureInit → SetDefaults(merged) → SetSettings → Activate → RebuildAll
        await host.InitializeAsync(settings, CancellationToken.None);

        // Kick off a background fetch (explicit — init does NOT fetch).
        _ = host.FetchAsync(FetchMode.Default, CancellationToken.None);

        Host    = host;
        Config  = host.Get<GameplayConfig>();
        LiveOps = host.Get<LiveOpsConfig>();
    }
}
```

> **Key point:** No matter how many `[ConfigModel]` types you add, backend lifecycle methods are called **exactly once**. Defaults from all models are merged and pushed in a single `SetDefaultsAsync` call.

### 3. Read Config (Hot Path)

```csharp
// Allocation-free, no JSON parsing, thread-safe
var gp = ConfigBootstrapper.Config!.Current;
var lo = ConfigBootstrapper.LiveOps!.Current;
```

### 4. Fetch & Apply Updates at Safe Points

```csharp
// At boot (after init) or periodically — fetches new values from backend.
// FetchMode.Default respects MinimumFetchInterval; FetchMode.Force bypasses it.
await host.FetchAsync(FetchMode.Default, ct);

// Check if there are pending updates (useful for UI indicators).
if (host.HasPendingUpdate)
{
    // Menu screen or lobby — NOT during a match.
    // One call activates and rebuilds ALL registered configs.
    var changed = await host.ActivateAndRebuildAsync(ct);
    if (changed) Debug.Log($"Configs updated to v{host.Version}");
}
```

### 5. Capture for Match

```csharp
// Freeze config at match start — no mid-match drift
var matchGp = ConfigBootstrapper.Config!.Current;
var matchLo = ConfigBootstrapper.LiveOps!.Current;
// Use matchGp / matchLo for the entire match duration
```

---

## 🏷️ Attributes Reference

### `[ConfigModel]`

Marks a class or record for binder + module generation.

```csharp
[ConfigModel]
public sealed record MyConfig { ... }
```

### `[ConfigKey(key)]`

Maps a property to a Remote Config key with a primitive value.

| Parameter | Type | Description |
|-----------|------|-------------|
| `key` | `string` | Remote Config key name |
| `DefaultBool` | `bool` | Default for `bool` properties |
| `DefaultLong` | `long` | Default for `long` properties |
| `DefaultDouble` | `double` | Default for `double` properties |
| `DefaultString` | `string` | Default for `string` properties |
| `MinLong` / `MaxLong` | `long` | Clamp range for `long` values |
| `MinDouble` / `MaxDouble` | `double` | Clamp range for `double` values |
| `Required` | `bool` | Report to diagnostics if key is missing |
| `SanitizeReason` | `string?` | Custom reason string for clamp diagnostics |

### `[ConfigJson(key)]`

Maps a property to a Remote Config key containing a JSON string, parsed once during `Build()`.

| Parameter | Type | Description |
|-----------|------|-------------|
| `key` | `string` | Remote Config key name |
| `DefaultJson` | `string` | Fallback JSON if key is missing or parse fails |
| `Required` | `bool` | Report to diagnostics if key is missing |

---

## 🔄 Lifecycle

```
Game Start
    │
    ▼
host.InitializeAsync()                     ← called ONCE for all configs
    ├── 1. EnsureInitializedAsync()        ← wait for Firebase
    ├── 2. SetDefaultsAsync(merged)        ← merged defaults from all binders
    ├── 3. SetSettingsAsync()              ← fetch timeout, cache interval
    ├── 4. ActivateAsync()                 ← apply cached values from last session
    └── 5. RebuildAll()                    ← build ALL typed snapshots, version++
    │
    ▼
host.Get<T>().Current is ready             (all configs share version 1)
    │
    ▼
host.FetchAsync(mode)                      ← explicit, fire-and-forget or awaited
    └── sets HasPendingUpdate = true
    │
    ▼
[Menu / Lobby — Safe Point]
    │
    ├── if (host.HasPendingUpdate) ...
    │     host.ActivateAndRebuildAsync()   ← applies fetched values
    │       ├── returns true?  → RebuildAll() + global version++
    │       └── returns false? → no-op (version unchanged)
    │     clears HasPendingUpdate
    │
    ▼
[Match Start]
    │
    ├── var match = config.Current;         ← capture snapshot
    │   (use match for entire match — no rebuild mid-match)
    │
    ▼
[Match End → Return to Menu → repeat]
```

---

## 🧪 Testing

### Core Tests (Unity EditMode)

Run via **Window → General → Test Runner → EditMode → Run All**.

| Test | Validates |
|------|-----------|
| `InitializeAsync_CallsBackendInCorrectOrder` | 4-step backend call sequence (no fetch) |
| `InitializeAsync_WithTwoConfigs_ActivateCalledOnce` | N configs, Activate called once |
| `GlobalVersion_IsSyncedAcrossConfigs` | `configA.Version == configB.Version` after init |
| `GlobalVersion_IncreasesTogetherOnSafePoint` | Both versions increment together |
| `Binder_Defaults_ContainExpectedKeys` | Defaults dict has correct keys + types |
| `DefaultsMerge_ConflictingKeys_Throws` | Same key, different default → throws |
| `Build_MissingKey_UsesDefaultAndReportsDiagnostic` | Fallback + `OnMissingKey` |
| `Build_OutOfRangeLow_IsClamped` | Low clamp + `OnSanitized` |
| `Build_OutOfRangeHigh_IsClamped` | High clamp + `OnSanitized` |
| `ActivateAndRebuild_False_ReturnsFalseAndVersionUnchanged` | Returns false, no-op |
| `ActivateAndRebuild_True_ReturnsTrueAndAllSnapshotsReplaced` | Returns true, all rebuilt |
| `Build_ValidJson_ParsesCorrectly` | JSON → typed object |
| `Build_InvalidJson_FallsBackAndReportsError` | Fallback + `OnParseError` |
| `Get_UnregisteredType_Throws` | `Get<T>()` for unregistered type throws |
| `FetchAsync_Default_UsesCacheInterval` | Default mode uses MinimumFetchInterval |
| `FetchAsync_Force_UsesCacheExpirationZero` | Force mode bypasses throttle |
| `HasPendingUpdate_FalseAfterInit` | Not set until explicit fetch |
| `HasPendingUpdate_TrueAfterFetch` | Set after FetchAsync completes |
| `HasPendingUpdate_ClearedAfterActivate` | Cleared by ActivateAndRebuildAsync |

### Firebase Adapter Tests (Unity EditMode)

Tests use `FakeFirebaseRemoteConfigFacade` — no real Firebase instance needed.

### Source Generator Tests (dotnet test)

```bash
cd Packages/com.arenyx.remoteconfig.sourcegen/Tests~
dotnet test
```

Validates generated code contains defaults, clamp logic, JSON parse calls, module registration, and compiles cleanly.

---

## 🔧 Building the Source Generator

The generator is a standalone .NET project targeting `netstandard2.0`:

```bash
cd Packages/com.arenyx.remoteconfig.sourcegen/Generator~
dotnet build -c Release
```

Copy the output DLL to the package runtime folder:

```bash
cp bin/Release/netstandard2.0/Arenyx.RemoteConfig.SourceGen.Generator.dll ../Runtime/
```

The `.meta` file with the `RoslynAnalyzer` label is already configured. Unity will pick up the generator on the next domain reload.

> **Important:** The generator targets Roslyn 4.3.0 for Unity 6000.0 compatibility. Do not upgrade `Microsoft.CodeAnalysis.CSharp` beyond what your Unity version supports.

---

## 📁 Project Structure

```
Packages/
├── com.arenyx.remoteconfig.core/
│   ├── Runtime/
│   │   ├── IRemoteConfigBackend.cs       # Backend abstraction + settings
│   │   ├── RemoteConfigValue.cs          # Value type with factory methods
│   │   ├── IConfigBinder.cs              # Generic binder interface
│   │   ├── IConfig.cs                    # Covariant read-only accessor
│   │   ├── IJsonCodec.cs                 # JSON deserialization abstraction
│   │   ├── IConfigDiagnostics.cs         # Diagnostics callbacks
│   │   ├── IRemoteConfigHost.cs          # Unified host interface
│   │   ├── IRemoteConfigHostBuilder.cs   # Builder for module registration
│   │   ├── IRemoteConfigModule.cs        # Module registration interface
│   │   ├── RemoteConfigHost.cs           # Host implementation (lifecycle engine)
│   │   ├── ConfigStore.cs                # Pure snapshot holder
│   │   └── IConfigStoreInternal.cs       # Internal store management interface
│   └── Tests/EditMode/
│       ├── RemoteConfigHostTests.cs
│       └── Fakes/
│           ├── FakeBinder.cs             # Test binder (TestConfig)
│           ├── FakeBinderB.cs            # Second test binder (TestUiConfig)
│           ├── FakeModule.cs             # Test modules
│           ├── FakeRemoteConfigBackend.cs
│           ├── FakeDiagnostics.cs
│           └── FakeJsonCodec.cs
│
├── com.arenyx.remoteconfig.firebaseunity/
│   ├── Runtime/
│   │   ├── IFirebaseRemoteConfigFacade.cs
│   │   ├── FirebaseRemoteConfigFacade.cs
│   │   └── FirebaseRemoteConfigBackend.cs
│   └── Tests/EditMode/
│
├── com.arenyx.remoteconfig.sourcegen/
│   ├── Runtime/
│   │   ├── Attributes/                   # ConfigModel, ConfigKey, ConfigJson
│   │   └── *.Generator.dll              # Prebuilt Roslyn source generator
│   ├── Generator~/                      # Generator source (ignored by Unity)
│   └── Tests~/                          # Roslyn test project (ignored by Unity)
│
Assets/Game/Config/                       # Sample game integration
├── Models/
│   ├── GameplayConfig.cs                 # [ConfigModel] — feature flags, turn timer, balance
│   ├── LiveOpsConfig.cs                  # [ConfigModel] — events, XP multiplier, promo
│   ├── BalanceConfig.cs                  # JSON sub-config for gameplay
│   └── PromoConfig.cs                   # JSON sub-config for live ops
├── Bootstrap/
│   └── ConfigBootstrapper.cs            # Host + module init
└── Samples/
    └── MenuSafePointApply.cs            # Safe-point rebuild example
```

---

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines

- `#nullable enable` for all SDK code
- Prefer `var` for local declarations
- Config models should be `sealed record` with `init`-only properties
- Core package must never reference Unity or Firebase assemblies
- All public APIs need XML doc comments
- Add a `[ConfigModel]` sample in `Assets/Game/Config/Models/` when demonstrating new attribute features

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- [Firebase Unity SDK](https://firebase.google.com/docs/unity/setup) — Remote Config backend
- [Roslyn Source Generators](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) — Compile-time code generation
