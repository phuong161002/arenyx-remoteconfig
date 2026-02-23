# Arenyx Remote Config SDK — Delta Changes (Host + Generated Module + Static Facade)

This document describes **only the changes** relative to the previous spec/prompt you already have.  
Use this as the **follow-up prompt** for the coding agent.

---

## A) Architecture: switch from “each ConfigStore initializes itself” to “one Host unifies lifecycle”

### A1) Add `RemoteConfigHost` (Core) — the only place that calls backend lifecycle
**New:** `RemoteConfigHost` is responsible for:
- `EnsureInitializedAsync`
- `SetDefaultsAsync(merged defaults from all configs)`
- `SetSettingsAsync(once)`
- `ActivateAsync(once)`
- `RebuildAll(once)` (build all snapshots after activation)
- `FetchAsync(once)` (background)

**Safe point:** `ActivateAndRebuildAsync()` must be called only at menu/before matchmaking.  
If activated values changed → rebuild **all** stores in one pass.

### A2) Remove lifecycle methods from `ConfigStore<T>`
**Change:** `ConfigStore<T>` must **not** expose:
- `InitializeAsync()`
- `ActivateAndRebuildAsync()`

`ConfigStore<T>` becomes a pure snapshot holder:
- `T Current`
- `long Version`
- internal rebuild entrypoint, called by Host:
  - `internal void Rebuild(IRemoteConfigBackend backend, IJsonCodec? json, IConfigDiagnostics? diag, long globalVersion)`

### A3) Add internal store interface so Host can manage all stores uniformly
**New:** `internal interface IConfigStoreInternal`
- `IReadOnlyDictionary<string, object> Defaults { get; }`
- `Type ModelType { get; }`
- `void Rebuild(IRemoteConfigBackend backend, IJsonCodec? json, IConfigDiagnostics? diag, long globalVersion)`

Host keeps `List<IConfigStoreInternal>`.

### A4) Versioning: use **one global version** shared by all configs
**Change:** Host owns `long _globalVersion`.  
Whenever Host rebuilds (initial rebuild and safe-point rebuild), do `_globalVersion++`.  
`IConfig<T>.Version` should return this global version to ensure all models are synced.

---

## B) Registration: remove manual register; SourceGen generates a module/registry

### B1) Add `IRemoteConfigModule` (Core)
**New:**
```csharp
public interface IRemoteConfigModule
{
    void Register(IRemoteConfigHostBuilder builder);
}
```

### B2) Add `IRemoteConfigHostBuilder` (Core)
**New:**
```csharp
public interface IRemoteConfigHostBuilder
{
    void Add<T>(IConfigBinder<T> binder) where T : class;
}
```

### B3) Host API adds `AddModule()`
**New:** `host.AddModule(IRemoteConfigModule module)` which calls `module.Register(builder)` and collects all binders/stores.

### B4) Defaults merge policy (mandatory)
Host must merge defaults from all binders/stores **before** calling backend `SetDefaultsAsync`.
- If keys are duplicated:
  - same value/type → OK
  - different value/type → throw in dev/Editor (recommended), or report via diagnostics if you prefer soft-fail.

---

## C) Optional Static Facade (for open-source “easy mode”)

> Core remains instance-based. Static facade is only a wrapper.

### C1) Add `ArenyxRemoteConfig` static class (recommended in Unity/Firebase package)
**New:**
- `InitializeAsync(backend, settings, isDevBuild, json, diag, ct)`
  - creates Host
  - adds generated module
  - calls `host.InitializeAsync`
- `Get<T>() => IConfig<T>`
- `ApplyAtSafePointAsync() => host.ActivateAndRebuildAsync()`

### C2) Test hook
Add `internal ResetForTests()` compiled only for tests to avoid global state flakiness.

---

## D) Backend value mapping: Firebase typed getters can throw — do not parse in adapter

### D1) Change Core `RemoteConfigValue` to be raw-string based
**Replace** the old `RemoteConfigValue(bool HasValue, bool Bool, long Long, double Double, string String)` with:
```csharp
public readonly record struct RemoteConfigValue(bool HasValue, string Raw, RemoteValueSource Source);

public enum RemoteValueSource
{
    Unknown = 0,
    Static = 1,
    Default = 2,
    Remote = 3
}
```

### D2) Firebase adapter maps only `StringValue` + `Source`
**Change:** `MapValue(ConfigValue cv)` must not call `cv.BooleanValue/LongValue/DoubleValue`.  
Those may throw `FormatException` or `Convert` exceptions.

Adapter should return:
- `Raw = cv.StringValue`
- `Source` mapped from Firebase `ValueSource`
- `HasValue = true` (binder decides fallback) or a best-effort based on `Source/Raw`.

Binder (generated) does all parsing using non-throwing `TryParse`.

---

## E) Source Generator output changes

### E1) Generator must produce 2 artifacts
1) `<ModelName>Binder : IConfigBinder<Model>`
2) `GeneratedRemoteConfigModule : IRemoteConfigModule`
   - `Register(builder)` must call `builder.Add(new <ModelName>Binder());` for every `[ConfigModel]`.

### E2) Defaults must come from attributes, not initializers
**Mandatory:** defaults used for `SetDefaultsAsync` are generated from attribute metadata.
- Primitive defaults from `DefaultBool/DefaultLong/DefaultDouble/DefaultString`
- JSON defaults from `DefaultJson` (string)

### E3) Binder parsing rules (non-throwing)
- Bool parse should follow Firebase semantics:
  - true: `1,true,t,yes,y,on`
  - false: `0,false,f,no,n,off,""` (empty string is false)
- Numeric parse uses invariant culture and `TryParse`.
- Parse failure → `diag.OnParseError(key, raw, ...)` + fallback default.
- Clamp/sanitize → `diag.OnSanitized(...)`.

---

## F) Tests (Core): migrate from ConfigStore lifecycle tests to Host lifecycle tests

### F1) Replace “Initialize order” test to be Host-only
Verify backend calls happen **once**:
- `EnsureInit → SetDefaults(merged) → SetSettings → Activate → Fetch`

Also verify Host rebuild happens after Activate (use binder counters or diagnostics to assert build was invoked).

### F2) Add “N configs do not call Activate N times”
Create module with 2 binders/stores and call `host.InitializeAsync()`.  
Assert backend `ActivateAsync` was called **exactly once**.

### F3) Global version sync
After init:
- `configA.Version == configB.Version`

After safe-point apply with `ActivateAsync => changed`:
- both versions increase together.

### F4) Defaults merge + conflict
- same key same default → OK
- same key different default/type → must throw (or report per chosen policy)

### F5) Rebuild-all behavior
- `changed=true` → all config snapshots replaced (reference changes if model is class)
- `changed=false` → snapshots unchanged and `Version` unchanged

---

## G) Deliverables (delta file checklist)

### G1) Core package
**Add**
- `Runtime/RemoteConfigHost.cs`
- `Runtime/IRemoteConfigHost.cs`
- `Runtime/IRemoteConfigHostBuilder.cs`
- `Runtime/IRemoteConfigModule.cs`
- `Runtime/IConfigStoreInternal.cs`

**Modify**
- `Runtime/ConfigStore.cs` (remove lifecycle methods; add internal rebuild)
- `Runtime/RemoteConfigValue` (raw-based)

**Replace tests**
- Replace `ConfigStoreTests` with `RemoteConfigHostTests`

### G2) SourceGen package
**Modify generator outputs**
- Add `GeneratedRemoteConfigModule` generation
- Update Roslyn tests to assert module registration + binder outputs

### G3) FirebaseUnity package
**Modify adapter**
- Map raw string only; no typed getters
**Optional**
- Add `ArenyxRemoteConfig` static facade (or a separate unity convenience package)

---

## H) Updated acceptance criteria (delta)
- With multiple config models, backend `EnsureInit/SetDefaults/SetSettings/Activate/Fetch` are called **once** at init.
- `ActivateAndRebuildAsync()` rebuilds **all** configs in one pass and increments one **global** version.
- Firebase adapter never throws due to typed getters.
- Generated module eliminates manual registration.
