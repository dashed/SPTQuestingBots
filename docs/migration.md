# SPT 3.x to 4.x Migration Guide

This document describes the migration of SPTQuestingBots from SPT 3.x (TypeScript) to SPT 4.x (C#).

---

## Overview

SPT 4.x rewrites the server from TypeScript/Node.js to C#/.NET. This affects all server-side mods:

| Aspect | SPT 3.x | SPT 4.x |
|--------|---------|---------|
| Server runtime | Node.js + TypeScript | .NET (C#) |
| DI framework | tsyringe (`container.resolve`) | .NET DI (`[Injectable]` attribute) |
| Mod entry points | `IPreSptLoadMod`, `IPostDBLoadMod`, `IPostSptLoadMod` | `IPreSptLoadModAsync`, `IOnLoad` (PostDBModLoader), `IOnLoad` (PostSptModLoader) |
| Mod metadata | `package.json` (name, version, sptVersion, author, license) | `AbstractModMetadata` class |
| Route registration | `StaticRouterModService.registerStaticRouter()` / `DynamicRouterModService.registerDynamicRouter()` | C# `StaticRouter` / `DynamicRouter` pattern |
| Callback interception | `container.afterResolution("BotCallbacks", ...)` | Controller-based interception |
| Configuration | JSON imports in TypeScript | JSON deserialization with C# model classes |
| Logging | `ILogger` (Winston) resolved from container | `ILogger` from SPT DI |

The **client plugin** (BepInEx/Harmony) is less affected since it runs inside the game process, but it still requires updates for changed SPT client DLL APIs.

---

## Server-Side Migration

### TypeScript to C# Conversion

The original server mod consists of four TypeScript files:

| Original File | Ported To | Description |
|---------------|-----------|-------------|
| `src/mod.ts` | `QuestingBotsController.cs` | Main mod class, route handlers, bot generation |
| `src/CommonUtils.ts` | `CommonUtils.cs` (service) | Logging and item name lookup |
| `src/BotLocationUtil.ts` | `BotLocationService.cs` | Hostility, waves, bot caps |
| `src/PMCConversionUtil.ts` | `PMCConversionService.cs` | Brain type blacklisting |

### DI Migration: tsyringe to .NET DI

**Before (TypeScript / tsyringe):**
```typescript
public postDBLoad(container: DependencyContainer): void {
    this.configServer = container.resolve<ConfigServer>("ConfigServer");
    this.databaseServer = container.resolve<DatabaseServer>("DatabaseServer");
    this.iBotConfig = this.configServer.getConfig(ConfigTypes.BOT);
}
```

**After (C# / .NET DI):**
```csharp
[Injectable]
public class QuestingBotsController
{
    private readonly ConfigServer _configServer;
    private readonly DatabaseServer _databaseServer;

    public QuestingBotsController(ConfigServer configServer, DatabaseServer databaseServer)
    {
        _configServer = configServer;
        _databaseServer = databaseServer;
    }
}
```

### Route Registration

**Before (TypeScript):**
```typescript
staticRouterModService.registerStaticRouter(`StaticGetConfig${modName}`,
    [{
        url: "/QuestingBots/GetConfig",
        action: async () => {
            return JSON.stringify(modConfig);
        }
    }], "GetConfig"
);
```

**After (C# StaticRouter pattern):**
```csharp
public class GetConfigRouter : StaticRouter
{
    public GetConfigRouter() : base("/QuestingBots/GetConfig") { }

    public override async Task<string> HandleAsync(string url, string body, string sessionId)
    {
        return JsonConvert.SerializeObject(modConfig);
    }
}
```

### Dynamic Route Migration

**Before (TypeScript):**
```typescript
dynamicRouterModService.registerDynamicRouter(`DynamicAdjustPScavChance${modName}`,
    [{
        url: "/QuestingBots/AdjustPScavChance/",
        action: async (url: string) => {
            const urlParts = url.split("/");
            const factor: number = Number(urlParts[urlParts.length - 1]);
            // ...
            return JSON.stringify({ resp: "OK" });
        }
    }], "AdjustPScavChance"
);
```

**After (C# DynamicRouter pattern):**
```csharp
public class AdjustPScavChanceRouter : DynamicRouter
{
    public AdjustPScavChanceRouter() : base("/QuestingBots/AdjustPScavChance/") { }

    public override async Task<string> HandleAsync(string url, string body, string sessionId)
    {
        var urlParts = url.Split('/');
        var factor = double.Parse(urlParts[^1]);
        // ...
        return JsonConvert.SerializeObject(new { resp = "OK" });
    }
}
```

### BotCallbacks Interception

**Before (TypeScript):**
```typescript
container.afterResolution("BotCallbacks", (_t, result: BotCallbacks) => {
    result.generateBots = async (url, info, sessionID) => {
        const bots = await this.generateBots(
            { conditions: info.conditions }, sessionID, info.GeneratePScav
        );
        return this.httpResponseUtil.getBody(bots);
    }
}, {frequency: "Always"});
```

**After (C#):**
This requires a controller-based approach in SPT 4.x. The exact pattern depends on the SPT 4.x server API, but typically involves registering a handler that wraps or replaces the default bot generation endpoint.

### Mod Metadata

**Before (`package.json`):**
```json
{
    "name": "SPTQuestingBots",
    "version": "0.10.3",
    "author": "DanW",
    "license": "MIT",
    "sptVersion": ">=3.11.2 <3.12.0"
}
```

**After (`AbstractModMetadata` subclass):**
```csharp
public class QuestingBotsMetadata : AbstractModMetadata
{
    public override string Name => "SPTQuestingBots";
    public override string Version => "1.0.0";
    public override string Author => "DanW";
}
```

### Mod Lifecycle

**Before (TypeScript):**
```typescript
class QuestingBots implements IPreSptLoadMod, IPostSptLoadMod, IPostDBLoadMod {
    preSptLoad(container) { /* register routes */ }
    postDBLoad(container) { /* resolve deps, validate config */ }
    postSptLoad(container) { /* configure spawning */ }
}
```

**After (C#):**
```csharp
[Injectable]
public class QuestingBotsPlugin : IPreSptLoadModAsync, IOnLoad
{
    public async Task PreSptLoadAsync(IServiceProvider provider) { /* register routes */ }
    public void OnLoad() { /* resolve deps, configure spawning */ }
}
```

The two separate `IOnLoad` registrations (PostDBModLoader vs PostSptModLoader) provide ordering equivalent to the old `postDBLoad` / `postSptLoad` split.

---

## Client-Side Migration

### Updated SPT Client DLL References

The client plugin references SPT BepInEx plugins that may have API changes between 3.x and 4.x:

| DLL | Notes |
|-----|-------|
| `spt-common.dll` | HTTP client utilities -- verify `RequestHandler` API |
| `spt-custom.dll` | Custom game patches -- check for renamed/removed patches |
| `spt-reflection.dll` | Reflection utilities for accessing private EFT types |
| `spt-singleplayer.dll` | Single-player game mode patches |
| `spt-prepatch.dll` | Pre-patching assembly |

### Assembly-CSharp API Changes

EFT updates between SPT 3.x and 4.x may change the game's `Assembly-CSharp.dll`. Common areas of breakage:

- **Type renames/moves:** EFT frequently obfuscates class names. Types referenced by Harmony patches may need updating.
- **Method signature changes:** Patched methods may have added/removed parameters.
- **Removed types:** Some classes may be consolidated or removed.
- **New abstractions:** EFT may introduce new base classes or interfaces.

Each Harmony patch should be verified against the 4.x `Assembly-CSharp.dll`. The `TarkovInitPatch` version check (`MinVersion` / `MaxVersion`) should be updated to match SPT 4.x version ranges.

### BepInEx Plugin Attribute

Update the version in the plugin attribute:

```csharp
// Before
[BepInPlugin("com.DanW.QuestingBots", "DanW-QuestingBots", "0.10.3")]

// After
[BepInPlugin("com.DanW.QuestingBots", "DanW-QuestingBots", "1.0.0")]
```

Update dependency and version requirements as needed for SPT 4.x-compatible BigBrain and Waypoints releases.

---

## Configuration Changes

### config.json

The `config.json` format is preserved from v0.10.3. No structural changes were made during the port. All existing configuration keys remain valid.

### New Configuration

No new configuration options were added in the initial C# port. The focus was on functional parity with the TypeScript version.

### Deprecated Options

No options were deprecated in the initial port.

---

## Known Issues and Workarounds

### Patches Requiring Manual Verification

All 25+ Harmony patches must be verified against the SPT 4.x `Assembly-CSharp.dll`. Patches that target obfuscated types are most likely to break. Check:

1. Target type still exists (not renamed/removed)
2. Target method signature unchanged
3. Method parameters match patch expectations
4. Return type unchanged

### External Mod Compatibility

External mod interop uses reflection (`SAINInterop`, `LootingBotsInterop`) and is version-sensitive:

| Mod | Risk | Notes |
|-----|------|-------|
| SAIN | Medium | Reflection targets may change between SAIN versions for SPT 4.x |
| Looting Bots | Medium | API methods may be renamed/restructured |
| Donuts / SWAG | Low | Detection is name-based (`spawningModNames` array) |
| BigBrain | Low | Core dependency, brain layer API is stable |
| Waypoints | Low | Core dependency, NavMesh data format is stable |

### Server JSON Serialization

The TypeScript mod used native `JSON.stringify`/`JSON.parse`. The C# port uses `Newtonsoft.Json`. Ensure serialization settings (camelCase vs PascalCase, null handling) match what the client expects. The client dynamically loads `SerializerSettings` from the game's assemblies via reflection to maintain compatibility.

---

## Testing the Migration

### Verification Checklist

- [ ] Server plugin loads without errors in SPT 4.x server log
- [ ] Client plugin loads without errors in BepInEx log
- [ ] `/QuestingBots/GetConfig` returns valid JSON matching `config.json`
- [ ] `/QuestingBots/GetAllQuestTemplates` returns EFT quest data
- [ ] `/QuestingBots/GetEFTQuestSettings` returns quest settings
- [ ] `/QuestingBots/GetZoneAndItemQuestPositions` returns position data
- [ ] `/QuestingBots/GetScavRaidSettings` returns Scav raid settings
- [ ] `/QuestingBots/GetUSECChance` returns USEC chance value
- [ ] PMCs spawn at raid start on all maps
- [ ] Player Scavs spawn on schedule
- [ ] Bots perform quest objectives (navigate to positions, plant items, etc.)
- [ ] Bot groups follow bosses correctly
- [ ] AI limiter disables distant bots without breaking questing
- [ ] SAIN interop works (extraction, hearing) when SAIN is installed
- [ ] Looting Bots interop works (loot scanning) when Looting Bots is installed
- [ ] Spawning system auto-disables when SWAG/DONUTS is detected
- [ ] F12 menu options function correctly
- [ ] Custom quests in `quests/custom/` are loaded and assigned
- [ ] Debug visualizations work when `debug.enabled = true`
- [ ] No performance regression compared to v0.10.3
- [ ] `make ci` passes (format, lint, tests)
