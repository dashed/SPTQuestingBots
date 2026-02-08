# Architecture

This document describes the architecture of SPTQuestingBots, a two-component mod for Single Player Tarkov (SPT) 4.x that adds an AI quest objective system and dynamic PMC/PScav spawning.

---

## System Overview

SPTQuestingBots is split into a **server plugin** and a **client plugin** that communicate over SPT's internal HTTP API.

```
┌─────────────────────────────────┐     HTTP API      ┌─────────────────────────────────┐
│       Server Plugin             │◄──────────────────►│       Client Plugin             │
│  (SPTQuestingBots.Server)       │                    │  (SPTQuestingBots.Client)        │
│                                 │                    │                                 │
│  - Configuration endpoints      │                    │  - BepInEx plugin (Unity)        │
│  - Quest template retrieval     │                    │  - Harmony patches (25+)         │
│  - Bot profile generation       │                    │  - BigBrain AI layers            │
│  - Hostility/wave management    │                    │  - Spawning system               │
│  - PScav conversion control     │                    │  - Quest management              │
└─────────────────────────────────┘                    └─────────────────────────────────┘
```

### Dependency Chain

```
SPT 4.0.0 (core)
├── SPT-BigBrain 1.4.0 (custom AI brain layer framework)
│   └── SPT-Waypoints 1.8.1 (bot navigation/pathfinding)
└── QuestingBots 1.0.0 (this mod)
    ├── Client plugin
    │   ├── Depends on BigBrain for CustomLayer/CustomLogic
    │   └── Depends on Waypoints for NavMesh data
    └── Server plugin
        └── Uses [Injectable] DI system
```

**Data flow:** The client plugin starts up inside the EFT game process (loaded by BepInEx). During initialization it calls the server plugin's HTTP endpoints to fetch `config.json`, EFT quest templates, quest zone positions, Scav raid settings, and USEC chance data. During gameplay, it calls endpoints to adjust PScav conversion chances and generate bot profiles.

---

## HTTP API Endpoints

All endpoints are registered by the server plugin and called by the client's `ConfigController`.

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/QuestingBots/GetConfig` | Static | Returns the full `config.json` configuration |
| `/QuestingBots/GetAllQuestTemplates` | Static | Returns all EFT quest templates (including mod-added quests) |
| `/QuestingBots/GetEFTQuestSettings` | Static | Returns `eftQuestSettings.json` override data |
| `/QuestingBots/GetZoneAndItemQuestPositions` | Static | Returns `zoneAndItemQuestPositions.json` position overrides |
| `/QuestingBots/GetScavRaidSettings` | Static | Returns Scav raid time settings per map |
| `/QuestingBots/GetUSECChance` | Static | Returns the PMC USEC faction chance |
| `/QuestingBots/AdjustPScavChance/{factor}` | Dynamic | Adjusts the PScav conversion chance by the given factor |
| Bot generation | Intercepted | Intercepts `BotCallbacks.generateBots` to support PScav group generation |

---

## Server Plugin Architecture

The server plugin (`SPTQuestingBots.Server`) is a C# port of the original TypeScript `mod.ts` and utility modules.

### Plugin Lifecycle

In SPT 4.x, server mods follow this lifecycle:

1. **IPreSptLoadModAsync** -- Register HTTP routes (static and dynamic routers), intercept `BotCallbacks`
2. **IOnLoad (PostDBModLoader)** -- Resolve DI dependencies, initialize services, validate configuration
3. **IOnLoad (PostSptModLoader)** -- Remove blacklisted brain types, detect conflicting spawning mods, configure the spawning system

### Services

| Service | Responsibility |
|---------|----------------|
| **CommonUtils** | Prefixed logging (`[Questing Bots]`) at debug/info/warning/error levels. Item name lookup via locale translations with template fallback. |
| **BotLocationService** | Bot hostility chance adjustment per location. PvE boss wave removal. Bot wave disabling (PMC, Scav, boss). EFT bot cap application. Rogue spawn delay removal. |
| **PMCConversionService** | Removes blacklisted brain types from PMC and player-Scav brain-type pools to prevent bots from getting stuck. |

### Configuration Loading

The server loads three JSON configuration files at startup:

- `config/config.json` -- Main mod configuration (spawning, questing, debug, hostility)
- `config/eftQuestSettings.json` -- Per-quest override settings
- `config/zoneAndItemQuestPositions.json` -- Manual position overrides for quest zones and items

Configuration arrays are validated on load (correct column count, integer constraints). If validation fails, the mod disables itself.

### Spawning Mod Detection

On post-load, the server checks for known spawning mods (SWAG/DONUTS, MOAR, Better Spawns Plus, RealPlayerSpawn, ABPS). If any are detected, the QuestingBots spawning system auto-disables to prevent excessive bot counts.

---

## Client Plugin Architecture

The client plugin (`SPTQuestingBots.Client`) is a BepInEx plugin loaded into the EFT game process. It contains the core AI behavior, spawning systems, quest management, and Harmony patches.

### Entry Point and SPT 4.x Patterns

`QuestingBotsPlugin` (`QuestingBotsPlugin.cs`) is the BepInEx plugin class:

- **Target framework:** `netstandard2.1`
- **Plugin declaration:** `[BepInPlugin]` with `[BepInDependency("com.SPT.core", "4.0.0")]` and `[BepInDependency("xyz.drakia.bigbrain", "1.4.0")]`
- **Declared incompatibilities:** AI Limit, AI Disabler
- **On `Awake()`:** Fetches server config, enables Harmony patches, registers bot generators, registers BigBrain layers, initializes F12 menu options

**SPT 4.x conventions used by the client plugin:**

- All Harmony patches extend `SPT.Reflection.Patching.ModulePatch`
- BigBrain layer registration: `BrainManager.AddCustomLayer(typeof(CustomLayer), brains, priority)`
- Custom layers inherit from `CustomLayer` and implement: `GetName()`, `IsActive()`, `GetNextAction()`, `IsCurrentActionEnding()`
- Custom logics inherit from `CustomLogic` and implement: `Start()`, `Stop()`, `Update(ActionData)`

### BehaviorExtensions

Custom AI behavior layers built on the BigBrain framework:

| Class | Base | Purpose |
|-------|------|---------|
| `CustomLayerForQuesting` | `CustomLayer` | Base layer for questing behavior; implements `GetName()`, `IsActive()`, `GetNextAction()`, `IsCurrentActionEnding()` |
| `CustomLayerDelayedUpdate` | `CustomLayer` | Layer with throttled update interval |
| `CustomLogicDelayedUpdate` | `CustomLogic` | Logic action with throttled updates; implements `Start()`, `Stop()`, `Update(ActionData)` |
| `GoToPositionAbstractAction` | `CustomLogic` | Base action for movement-to-position behaviors |
| `MonoBehaviourDelayedUpdate` | `MonoBehaviour` | Unity MonoBehaviour with configurable update interval |

### BotLogic Subsystem

The BotLogic module implements bot decision-making through several subsystems:

#### BotMonitor

`BotMonitorController` orchestrates a set of monitors that evaluate bot state each update:

| Monitor | Evaluates |
|---------|-----------|
| `BotCombatMonitor` | Whether the bot is in or recently exited combat |
| `BotHealthMonitor` | Health thresholds (head, chest, stomach, legs), hydration, energy |
| `BotHearingMonitor` | Suspicious noise detection with loudness, distance, and cooldown logic |
| `BotLootingMonitor` | Loot-break scheduling and Looting Bots integration |
| `BotExtractMonitor` | Extraction eligibility (alive time, quests completed, time remaining) |
| `BotQuestingMonitor` | Overall questing eligibility (combines other monitors + encumbrance) |
| `BotQuestingDecisionMonitor` | Final decision on whether to quest or hold |
| `BotMountedGunMonitor` | Whether the bot is using a stationary weapon |

#### HiveMind

Group coordination sensors that share state across bot groups:

| Sensor | Shares |
|--------|--------|
| `BotHiveMindCanQuestSensor` | Whether group members can quest |
| `BotHiveMindCanSprintToObjectiveSensor` | Whether the group can sprint |
| `BotHiveMindIsInCombatSensor` | Combat state propagation |
| `BotHiveMindIsSuspiciousSensor` | Suspicious-noise awareness sharing |
| `BotHiveMindWantsToLootSensor` | Loot-break coordination |

#### Follow

Boss-follower behavior:

| Action/Layer | Behavior |
|-------------|----------|
| `BotFollowerLayer` | Activates when a follower needs to stay near its boss |
| `BotFollowerRegroupLayer` | Activates when a follower is too far and must regroup |
| `FollowBossAction` | Movement logic for following the boss |
| `FollowerRegroupAction` | Movement logic for regrouping with the boss |
| `BossRegroupAction` | Boss behavior when waiting for followers to regroup |

#### Objective

Quest objective actions:

| Action | Behavior |
|--------|----------|
| `GoToObjectiveAction` | Navigate to a quest objective position |
| `HoldAtObjectiveAction` | Stay within a radius of a position for a duration |
| `AmbushAction` | Go to a position, stand still, and look at a target direction |
| `SnipeAction` | Like Ambush but interruptible by suspicious noises |
| `PlantItemAction` | Simulate planting a quest item at a location |
| `ToggleSwitchAction` | Navigate to and activate a switch |
| `UnlockDoorAction` | Navigate to and unlock a locked door |
| `CloseNearbyDoorsAction` | Close all doors within a radius |

#### Sleep

AI limiter behavior:

| Class | Purpose |
|-------|---------|
| `SleepingLayer` | BigBrain layer that activates when the bot is far enough from players |
| `SleepingAction` | Disables bot AI processing to reduce performance impact |

#### ExternalMods

Interop with third-party mods through abstracted function interfaces:

```
ExternalModHandler
├── Functions/
│   ├── Extract/     AbstractExtractFunction
│   │   ├── InternalExtractFunction    (vanilla EFT extraction)
│   │   └── SAINExtractFunction        (SAIN-controlled extraction)
│   ├── Hearing/     AbstractHearingFunction
│   │   ├── InternalHearingFunction    (built-in hearing sensor)
│   │   └── SAINHearingFunction        (SAIN hearing integration)
│   └── Loot/        AbstractLootFunction
│       ├── InternalLootFunction       (no-op fallback)
│       └── LootingBotsLootFunction    (Looting Bots integration)
├── Interop/
│   ├── SAINInterop            (reflection-based SAIN API calls)
│   └── LootingBotsInterop    (reflection-based Looting Bots API calls)
└── ModInfo/
    ├── DonutsModInfo
    ├── LootingBotsModInfo
    ├── SAINModInfo
    ├── PerformanceImprovementsModInfo
    └── PleaseJustFightModInfo
```

### Components

Core Unity components attached to GameObjects:

| Component | Responsibility |
|-----------|----------------|
| `BotObjectiveManager` | Per-bot component managing quest assignment, objective tracking, and completion |
| `BotQuestBuilder` | Generates quest objectives from EFT quest data, standard quests, and custom quests |
| `LocationData` | Caches map data (spawn points, extracts, zones, NavMesh) |
| `TarkovData` | Root component for mod initialization and lifecycle management |
| `QuestPathFinder` | Calculates NavMesh paths to quest objectives |
| `QuestMinLevelFinder` | Determines minimum player levels for EFT quests |
| `LightkeeperIslandMonitor` | Monitors bot access to Lightkeeper Island on Lighthouse |
| `DebugData` | Debug visualization data collection |
| `PathRenderer` | Debug path rendering using Unity LineRenderers |

#### Spawning

| Component | Responsibility |
|-----------|----------------|
| `BotGenerator` | Base class for bot generation with registration system |
| `PMCGenerator` | Generates PMC bots at EFT spawn points with group support |
| `PScavGenerator` | Generates player-Scav bots on a raid-time-based schedule |

### Configuration

Over 30 configuration classes that mirror the `config.json` structure:

- `ModConfig` -- Root configuration class
- `QuestingConfig` -- Questing system settings
- `BotSpawnsConfig` -- Spawning system settings
- `BotQuestsConfig` -- Per-quest-type settings
- `BotQuestingRequirementsConfig` -- Bot eligibility thresholds
- `HearingSensorConfig` -- Noise detection parameters
- `SprintingLimitationsConfig` -- Sprint behavior constraints
- `StuckBotDetectionConfig` -- Stuck-bot detection and remedies
- `ExtractionRequirementsConfig` -- Extraction eligibility
- And more (see `Configuration/` directory)

### Controllers

| Controller | Responsibility |
|------------|----------------|
| `ConfigController` | HTTP communication with server, configuration deserialization, PScav chance adjustment |
| `LoggingController` | Centralized logging through BepInEx logger |
| `BotRegistrationManager` | Tracks registered bots and their state |
| `BotJobAssignmentFactory` | Creates and manages bot job assignments |
| `BotObjectiveManagerFactory` | Creates BotObjectiveManager instances for bots |

### Helpers

| Helper | Responsibility |
|--------|----------------|
| `NavMeshHelpers` | Unity NavMesh queries (nearest point, path calculation, accessibility) |
| `BotPathingHelpers` | Bot path management and stuck detection |
| `QuestHelpers` | Quest data parsing and objective generation |
| `ItemHelpers` | Item template lookups and key checking |
| `BotBrainHelpers` | Brain-layer priority management |
| `BotGroupHelpers` | Group membership and boss/follower queries |
| `InteractiveObjectHelpers` | Door and switch interaction logic |
| `DebugHelpers` | Debug gizmo creation and management |
| `RaidHelpers` | Raid state queries (time remaining, map info) |
| `TarkovTypeHelpers` | Reflection-based type discovery for EFT internals |
| `GameCompatibilityCheckHelper` | Version compatibility validation |
| `RecodableComponentHelpers` | Recodable item interaction utilities |

### Harmony Patches

25+ Harmony patches organized by category:

#### Core Patches
| Patch | Target | Purpose |
|-------|--------|---------|
| `TarkovInitPatch` | Game init | Version compatibility check |
| `MenuShowPatch` | Main menu | Mod status display |
| `ServerRequestPatch` | HTTP requests | Intercepts server communication for custom endpoints |
| `BotsControllerSetSettingsPatch` | Bot controller | Initializes mod components when bots controller starts |
| `BotsControllerStopPatch` | Bot controller | Cleanup when bots controller stops |
| `BotOwnerBrainActivatePatch` | Bot brain | Registers bots with the mod when their brains activate |
| `BotOwnerSprintPatch` | Bot sprint | Controls sprint behavior based on questing state |
| `IsFollowerSuitableForBossPatch` | Follower check | Adjusts follower eligibility for quest groups |
| `OnBeenKilledByAggressorPatch` | Bot death | Handles bot death cleanup |
| `CheckLookEnemyPatch` | Enemy detection | Modifies enemy-detection behavior for questing bots |
| `ReturnToPoolPatch` | Object pooling | Cleanup when bots are returned to the pool |
| `AirdropLandPatch` | Airdrop | Triggers airdrop-chaser quests |
| `PScavProfilePatch` | Profile generation | Adjusts PScav profile creation |

#### Spawning Patches
| Patch | Purpose |
|-------|---------|
| `GameStartPatch` | Initializes spawning system on raid start |
| `TimeHasComeScreenClassChangeStatusPatch` | Delays game start until bot generation finishes |
| `ActivateBossesByWavePatch` | Controls boss wave activation order |
| `AddEnemyPatch` | Manages enemy registration for spawned bots |
| `TryLoadBotsProfilesOnStartPatch` | Pre-loads bot profiles |
| `SetNewBossPatch` | Handles boss replacement when original boss dies |
| `GetAllBossPlayersPatch` | Provides boss player data |
| `InitBossSpawnLocationPatch` | Controls boss spawn locations when spawning bosses first |
| `BotsGroupIsPlayerEnemyPatch` | Forces PMC-PMC hostility |

#### Advanced Spawning Patches
| Patch | Purpose |
|-------|---------|
| `GetListByZonePatch` | Makes spawned PMCs/PScavs not count toward zone bot limits |
| `ExceptAIPatch` | Makes spawned bots appear as human players to EFT |
| `BotDiedPatch` | Triggers replacement spawns when bots die |
| `TryToSpawnInZoneAndDelayPatch` | Controls spawn timing and location |

#### Scav Limit Patches
| Patch | Purpose |
|-------|---------|
| `SpawnPointIsValidPatch` | Enforces minimum spawn distance from players |
| `TrySpawnFreeAndDelayPatch` | Enforces spawn rate limits and max alive caps |
| `NonWavesSpawnScenarioCreatePatch` | Adjusts non-wave spawn scenario parameters |
| `BotsControllerStopPatch` | Scav limit cleanup |

#### Lighthouse Patches
| Patch | Purpose |
|-------|---------|
| `MineDirectionalShouldExplodePatch` | Prevents mines from killing questing bots |
| `LighthouseTraderZoneAwakePatch` | Handles Lightkeeper zone initialization |
| `LighthouseTraderZonePlayerAttackPatch` | Manages Lightkeeper hostility |

---

## Data Flow

### Quest Loading and Assignment

```
1. Server loads quest templates from EFT database
2. Client fetches templates via /QuestingBots/GetAllQuestTemplates
3. Client fetches zone positions via /QuestingBots/GetZoneAndItemQuestPositions
4. Client fetches EFT quest settings via /QuestingBots/GetEFTQuestSettings
5. BotQuestBuilder generates quest objectives:
   - EFT quest objectives (markers, items, kills)
   - Standard quests (from quests/standard/ files)
   - Custom quests (from quests/custom/ files)
   - Built-in quests (spawn rush, boss hunter, airdrop chaser, spawn wander)
6. When a bot spawns, BotObjectiveManager selects a quest using the scoring algorithm:
   - Distance score (weighted, randomized)
   - Desirability score (weighted, randomized)
   - Exfil-direction score (weighted, angle-based)
7. Bot navigates to objectives via NavMesh paths
8. On completion/failure, a new quest is selected
```

### Bot Spawning

```
1. Server configures hostility, disables conflicting waves, sets bot caps
2. Client's GameStartPatch initializes the spawning system on raid start
3. PMCGenerator spawns initial PMC wave at EFT spawn points
4. As PMCs die (BotDiedPatch), replacement PMCs spawn at distant points
5. After all PMCs have spawned, PScavGenerator begins scheduled spawns
6. Advanced spawning patches make bots appear as human players to EFT
7. Total alive PMC+PScav count is capped by bot_spawns.max_alive_bots
```

### Configuration Propagation

```
1. config.json is loaded by the server plugin at startup
2. Client fetches full config via /QuestingBots/GetConfig on Awake()
3. Config is deserialized into ModConfig class hierarchy
4. QuestingBotsPluginConfig creates F12 menu entries from config values
5. F12 menu changes update the in-memory config at runtime
6. Individual systems read config values through ConfigController.Config
```

---

## SPT 3.x to 4.x Migration Notes

For a detailed migration guide, see [migration.md](migration.md).

Key architectural changes:

| Aspect | SPT 3.x (TypeScript) | SPT 4.x (C#) |
|--------|----------------------|---------------|
| Server language | TypeScript / Node.js | C# / .NET |
| DI framework | tsyringe | .NET DI with `[Injectable]` attributes |
| Route registration | `StaticRouterModService` / `DynamicRouterModService` | C# controller pattern (`StaticRouter` / `DynamicRouter`) |
| Mod lifecycle | `IPreSptLoadMod` / `IPostDBLoadMod` / `IPostSptLoadMod` | `IPreSptLoadModAsync` / `IOnLoad` (PostDBModLoader) / `IOnLoad` (PostSptModLoader) |
| Mod metadata | `package.json` | `AbstractModMetadata` class |
| Callback interception | `container.afterResolution("BotCallbacks", ...)` | Controller-based interception |

---

## Reference Mods

The following mods were used as pattern references during the SPT 4.x port:

| Mod | Author | Used For |
|-----|--------|----------|
| **SPT-BigBrain** | DrakiaXYZ | Brain layer framework (direct dependency). Defines the `CustomLayer` / `CustomLogic` / `BrainManager` API that QuestingBots builds on. |
| **SPT-Waypoints** | DrakiaXYZ | Navigation and expanded NavMesh data (direct dependency via BigBrain). |
| **Phobos** | Janky | Squad coordination and zone-based behaviors. Example of BigBrain `CustomLayer` / `CustomLogic` patterns. |
| **SAIN** | Solarint | Comprehensive AI combat overhaul. Example of BigBrain integration, bot brain management, and external-mod interop patterns. |
| **Master-Tool** | -- | Server plugin patterns, `[Injectable]` DI system, and `ModulePatch` Harmony patch conventions for SPT 4.x. |
