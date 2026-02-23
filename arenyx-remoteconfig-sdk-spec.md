# Arenyx Remote Config SDK (Unity + Firebase) — System Requirements

## 0) Assumptions
- A **blank Unity project** exists.
- **Firebase Unity SDK** is already installed and configured (Android `google-services.json` / iOS `GoogleService-Info.plist`).
- Goal: implement SDK as **embedded UPM packages** inside the Unity project first; later extract to an open-source repo with minimal changes.

---

## 1) Goals
1) Game code usage:
   - Inject `IConfig<TModel>`
   - Read `config.Current` (or `.Value`) to get an **immutable typed snapshot**.
2) SDK features:
   - Strong defaults + safe fallback.
   - Standard lifecycle: `SetDefaults` → `Activate cached` → `Build snapshot` → `Fetch`.
   - Apply updates only at **safe points** (menu / before matchmaking), avoid drift mid-match.
   - JSON configs: **parse once** on rebuild → callers never parse JSON.
3) Clear separation:
   - **Core** package has **no Unity/Firebase dependency**.
   - **FirebaseUnity** package is only an adapter.
   - **SourceGen** generates binders from model classes and attributes.

---

## 2) Non-goals
- No editor UI tooling for Remote Config authoring.
- No automation for A/B testing, rollouts, personalization.
- No server-authoritative configuration.
- No secret storage (Remote Config values are readable by clients).

---

## 3) Package Layout (embedded UPM)
Create 3 packages under `Packages/`:

```
Packages/
  com.arenyx.remoteconfig.core/
  com.arenyx.remoteconfig.firebaseunity/
  com.arenyx.remoteconfig.sourcegen/
Assets/
  Game/
    Config/
      Models/
      Bootstrap/
      Samples/
```

### Naming (required)
- Package names:
  - `com.arenyx.remoteconfig.core`
  - `com.arenyx.remoteconfig.firebaseunity`
  - `com.arenyx.remoteconfig.sourcegen`
- C# namespaces:
  - `Arenyx.RemoteConfig.Core`
  - `Arenyx.RemoteConfig.FirebaseUnity`
  - `Arenyx.RemoteConfig.SourceGen` (attributes only)
- `#nullable enable` for all SDK code.
- Prefer `var` for local declarations.

---

## 4) Core Runtime API (backend-agnostic)

### 4.1 `IRemoteConfigBackend`
```csharp
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Arenyx.RemoteConfig.Core;

public interface IRemoteConfigBackend
{
    Task EnsureInitializedAsync(CancellationToken ct);

    Task SetDefaultsAsync(IReadOnlyDictionary<string, object> defaults, CancellationToken ct);

    Task SetSettingsAsync(RemoteConfigSettings settings, CancellationToken ct);

    Task FetchAsync(TimeSpan cacheExpiration, CancellationToken ct);

    /// <summary>Return true if activated values changed and a rebuild is needed.</summary>
    Task<bool> ActivateAsync(CancellationToken ct);

    RemoteConfigValue GetValue(string key);
}

public readonly record struct RemoteConfigSettings(int FetchTimeoutMs, long MinimumFetchIntervalMs);

public readonly record struct RemoteConfigValue(bool HasValue, bool Bool, long Long, double Double, string String);
```

### 4.2 Binder + Store + Diagnostics
```csharp
#nullable enable
using System.Collections.Generic;

namespace Arenyx.RemoteConfig.Core;

public interface IConfigBinder<T>
{
    IReadOnlyDictionary<string, object> Defaults { get; }
    T Build(IRemoteConfigBackend backend, IJsonCodec? json, IConfigDiagnostics? diag);
}

public interface IConfig<out T>
{
    T Current { get; }
    long Version { get; }
}

public interface IJsonCodec
{
    bool TryDeserialize<T>(string json, out T? value);
}

public interface IConfigDiagnostics
{
    void OnMissingKey(string key);
    void OnParseError(string key, string? raw, string error);
    void OnSanitized(string key, string? raw, string reason);
}
```

### 4.3 `ConfigStore<T>` lifecycle requirements
**Behavior requirements**
- `InitializeAsync(settings, isDevBuild, ct)` must call in this order:
  1) `backend.EnsureInitializedAsync`
  2) `backend.SetDefaultsAsync(binder.Defaults)`
  3) `backend.SetSettingsAsync(settings)`
  4) `backend.ActivateAsync` (apply cached values from previous sessions)
  5) `Rebuild()` (build snapshot from activated values)
  6) `backend.FetchAsync(...)` (may be fire-and-forget)

- `ActivateAndRebuildAsync(ct)`:
  - calls `backend.ActivateAsync`
  - if returns `true`: rebuild snapshot + `Version++`
  - if returns `false`: do nothing (snapshot reference stays the same, `Version` unchanged)

**Thread-safety (minimum)**
- `Current` must be swapped atomically (prefer `T` is a class immutable snapshot).
- Reads of `Current` should not allocate.
- Do not parse JSON on reads.

---

## 5) FirebaseUnity Adapter Package

### 5.1 `FirebaseRemoteConfigBackend`
- Implements `IRemoteConfigBackend` using Firebase Remote Config Unity SDK.
- **Core must not reference Firebase assemblies**.

### 5.2 Testability via Facade
To avoid hard dependency on `FirebaseRemoteConfig.DefaultInstance` in tests:
- Create `IFirebaseRemoteConfigFacade` internal abstraction.
- Production facade wraps Firebase SDK instance.
- Tests use a fake facade.

**Mapping rules**
- Map `ConfigValue.BooleanValue/LongValue/DoubleValue/StringValue` to `RemoteConfigValue`.
- `HasValue`:
  - If Firebase does not expose a reliable “missing” flag, set `HasValue=true` and let binder fallback/clamp.

---

## 6) Source Generator Package

### 6.1 Purpose
Developers write only:
- a config model `TModel` (immutable)
- fields/properties annotated with attributes

Generator outputs a binder:
- `<Model>Binder : IConfigBinder<TModel>`
- `Defaults` dictionary
- `Build()` method performing:
  - key reads
  - clamp/sanitize
  - JSON parse
  - fallback defaults
  - optional post-process hook

### 6.2 Public Attributes
Required minimum attributes:
- `[ConfigModel]` on a class/record participating in generation.
- `[ConfigKey(...)]` for primitive types: `bool/long/double/string`.
- `[ConfigJson(...)]` for JSON typed objects stored as Remote Config `string`.

**Metadata required**
- `Key` (string)
- Default value:
  - primitive: `DefaultBool/DefaultLong/DefaultDouble/DefaultString`
  - JSON: `DefaultJson`
- Clamp:
  - `MinLong/MaxLong`, `MinDouble/MaxDouble` (optional)
- Optional:
  - `Required` (if missing → diagnostics)
  - `SanitizeReason` string (used in diagnostics)

### 6.3 Post-process hook (recommended)
After `Build()`:
- Generator calls a user-implemented hook if present:
  - `partial void PostProcess(ref TModel model);`

### 6.4 Output requirements
- Output namespace stable:
  - `Arenyx.RemoteConfig.Generated`
- Binder type naming:
  - `<ModelName>Binder`
- Avoid type conflicts across assemblies:
  - include assembly name / hash suffix if necessary.

---

## 7) Model Requirements (game-side)

### 7.1 Immutable snapshot
- Prefer `sealed record class` with `init`-only properties.

### 7.2 JSON typed parsing
- JSON string value is parsed **only in binder Build()**.
- On parse failure:
  - fallback to default typed object
  - call `diag.OnParseError(...)`

### 7.3 Example model (for sample)
Model: `GameplayConfig`
- `bool FfNewShop` key: `ff_new_shop`
- `long PvpTurnTimeMs` key: `pvp_turn_time_ms` clamp `5000..60000`
- `BalanceConfig Balance` from JSON key `balance_json_v1`

---

## 8) Safe Point Policy (integration guide + sample)
**Boot**
- `InitializeAsync()`:
  - activates cached values
  - rebuilds snapshot immediately for current session
  - starts `FetchAsync(...)` in background

**Safe point (Menu / before matchmaking)**
- Call `ActivateAndRebuildAsync()` to apply fetched updates.

**In-match**
- Capture at match start:
  - `var cfg = config.Current;`
  - use `cfg` for the whole match; do not rebuild mid-match.

---

## 9) Tests (mandatory)

### 9.1 Framework
- Unity Test Framework (NUnit) for Core and FirebaseUnity (EditMode tests).
- Source generator tests in a .NET test project using Roslyn (`Microsoft.CodeAnalysis.CSharp`).

---

### 9.2 Core tests (EditMode)
Create `FakeRemoteConfigBackend`:
- Stores `Dictionary<string, RemoteConfigValue>`
- Records call order (List<string>)
- Configurable `ActivateAsync()` return true/false
- `FetchAsync()` no-op

**Required test cases**
1) **Initialize order**
   - Verify call sequence:
     `EnsureInit → SetDefaults → SetSettings → Activate → Build → Fetch`.
2) **Defaults used**
   - Binder `Defaults` contains expected keys and correct value types.
3) **Missing key fallback**
   - If backend lacks key, snapshot uses default.
   - `diag.OnMissingKey` is called.
4) **Clamp/Sanitize**
   - If backend value is out-of-range, snapshot is clamped.
   - `diag.OnSanitized` is called.
5) **ActivateAndRebuild behavior**
   - `ActivateAsync=false` → `Version` unchanged, snapshot reference unchanged.
   - `ActivateAsync=true` → `Version++`, snapshot reference changes.
6) **JSON parse success**
   - Valid JSON → typed object values correct.
7) **JSON parse failure**
   - Invalid JSON → fallback typed default + `diag.OnParseError`.

---

### 9.3 FirebaseUnity adapter tests (EditMode)
Using facade injection:
1) Ensure `FirebaseRemoteConfigBackend` calls correct facade methods per Core contract.
2) Ensure mapping from `ConfigValue` to `RemoteConfigValue` is correct.

---

### 9.4 Source generator tests (Roslyn)
Create a .NET test project that:
- compiles input source containing `[ConfigModel]` + attributes
- runs generator
- validates generated code contains expected:
  - Defaults dictionary
  - Build logic reading correct keys
  - clamp logic
  - JSON parse calls via `IJsonCodec`
- generated output must compile together with Core interfaces.

---

## 10) Deliverables (file checklist)

### 10.1 `com.arenyx.remoteconfig.core`
- `Runtime/IRemoteConfigBackend.cs`
- `Runtime/IConfigBinder.cs`
- `Runtime/IConfig.cs`
- `Runtime/IJsonCodec.cs`
- `Runtime/IConfigDiagnostics.cs`
- `Runtime/ConfigStore.cs`
- `Tests/EditMode/ConfigStoreTests.cs`
- `Tests/EditMode/Fakes/FakeRemoteConfigBackend.cs`
- `Tests/EditMode/Fakes/FakeBinder.cs`
- `Tests/EditMode/Fakes/FakeJsonCodec.cs`
- `Tests/EditMode/Fakes/FakeDiagnostics.cs`
- `.asmdef` for Runtime and Tests

### 10.2 `com.arenyx.remoteconfig.firebaseunity`
- `Runtime/FirebaseRemoteConfigBackend.cs`
- `Runtime/IFirebaseRemoteConfigFacade.cs`
- `Runtime/FirebaseRemoteConfigFacade.cs`
- `Tests/EditMode/FirebaseRemoteConfigBackendTests.cs`
- `.asmdef` for Runtime and Tests

### 10.3 `com.arenyx.remoteconfig.sourcegen`
- `Runtime/Attributes/ConfigModelAttribute.cs`
- `Runtime/Attributes/ConfigKeyAttribute.cs`
- `Runtime/Attributes/ConfigJsonAttribute.cs`
- `Editor/Generator/Arenyx.RemoteConfig.SourceGen.dll` (prebuilt generator DLL)
- `.asmdef`/import settings for analyzer use
- `.NET` Roslyn test project under `Tests~` or `Tools/`
- Documentation on how to enable analyzers/source generators in Unity

### 10.4 Game sample (`Assets/Game/Config/Samples`)
- `GameplayConfig.cs` (model + attributes)
- `BalanceConfig.cs` (typed JSON)
- `ConfigBootstrapper.cs` (initialize store + backend + binder)
- `MenuSafePointApply.cs` (safe point activate/rebuild)
- `MatchCaptureConfig.cs` (capture snapshot at match start)

---

## 11) Acceptance Criteria
- Unity project imports with zero compile errors.
- `ConfigStore.InitializeAsync()` works without requiring network success.
- Reading `config.Current` does not allocate and does not parse JSON.
- `ActivateAndRebuildAsync()` rebuilds only when activated values changed.
- Core EditMode tests pass 100%.
- FirebaseUnity adapter tests pass 100% (via facade fakes).
- Source generator tests pass 100% and generated output compiles.

---

## 12) Implementation Phases (recommended)
1) Phase 1: Core runtime + fakes + tests, manual binder (no generator).
2) Phase 2: FirebaseUnity adapter + facade + tests.
3) Phase 3: Source generator + Roslyn tests.
4) Phase 4: Samples + docs.
