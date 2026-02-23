# Delta Implementation Plan: Host + Generated Module

## Overview

Migrate from "each ConfigStore initializes itself" to "one RemoteConfigHost unifies lifecycle".
**RemoteConfigValue stays unchanged** (pre-parsed struct with Bool/Long/Double/String).

---

## Phase 1: Core Runtime — New Interfaces (4 new files)

### 1.1 `IConfigStoreInternal.cs` — NEW
Internal interface so Host can manage stores uniformly.

### 1.2 `IRemoteConfigModule.cs` — NEW
Module registration interface.

### 1.3 `IRemoteConfigHostBuilder.cs` — NEW
Builder for adding binders.

### 1.4 `IRemoteConfigHost.cs` — NEW
Public interface for the host.

## Phase 2: Core Runtime — Modified files (1 file)

### 2.1 `ConfigStore.cs` — MODIFY
- Remove `InitializeAsync()` and `ActivateAndRebuildAsync()`
- Remove constructor deps on backend/json/diag (move to Rebuild args)
- Implement `IConfigStoreInternal`
- Add `Rebuild(backend, json, diag, globalVersion)` called by Host
- Keep `IConfig<T>` (Current + Version)

## Phase 3: Core Runtime — Host Implementation (1 new file)

### 3.1 `RemoteConfigHost.cs` — NEW
- `AddModule(IRemoteConfigModule)` → calls module.Register(builder)
- Builder creates ConfigStore<T> per binder
- `InitializeAsync()`: EnsureInit → merge defaults → SetDefaults → SetSettings → Activate → RebuildAll → Fetch
- `ActivateAndRebuildAsync()`: Activate once → if changed, RebuildAll
- `Get<T>()` → lookup by typeof(T)
- Global version shared by all stores

## Phase 4: Source Generator — Add Module Generation (1 modified file)

### 4.1 `ConfigBinderGenerator.cs` — MODIFY
Add second output: `GeneratedRemoteConfigModule : IRemoteConfigModule`
that registers all [ConfigModel] binders.

## Phase 5: Tests (replace + add)

### 5.1 Replace `ConfigStoreTests` → `RemoteConfigHostTests`
### 5.2 Update FakeBinder to work with new ConfigStore constructor
### 5.3 Add second test binder for multi-config scenarios

## Phase 6: Game Samples

### 6.1 Update `ConfigBootstrapper.cs` to use Host + Module

---

## Execution Order

| Step | Action |
|------|--------|
| 1 | Add new Core interfaces |
| 2 | Rewrite ConfigStore<T> → snapshot holder |
| 3 | Implement RemoteConfigHost |
| 4 | Update FakeBinder + add FakeBinderB |
| 5 | Replace tests → RemoteConfigHostTests |
| 6 | Update source generator to emit Module |
| 7 | Rebuild generator DLL |
| 8 | Update game samples |
