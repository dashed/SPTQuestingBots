# Phobos vs SPTQuestingBots: Technical Comparison

## 1. Executive Summary

**Phobos** and **SPTQuestingBots** are both SPT (Single Player Tarkov) mods that control AI bot behavior outside of combat. They share the same foundational dependency (BigBrain) and operate at the same layer priority level (19 and 18), making them natural competitors for bot attention during out-of-combat time.

| Aspect | Phobos | SPTQuestingBots |
|--------|--------|-----------------|
| **Purpose** | Procedural movement via grid/zone fields | Quest-driven objectives from game data |
| **Architecture** | Pure client (ECS-like, ~62 files) | Client + Server (MonoBehaviour, ~174 files) |
| **BigBrain layers** | 1 (priority 19) | 4 (priorities 99, 26, 19, 18) |
| **Navigation** | Custom movement system with own pathfinding | BSG's built-in `BotOwner.FollowPath()` |
| **Squad model** | Explicit Squad entities with leader/member | HiveMind sensor-based boss/follower tracking |
| **Decision-making** | Utility AI (scored actions/strategies) | State machine (quest action enum) |
| **Bot spawning** | None (uses whatever spawns) | Custom PMC/PScav generation system |
| **Performance** | Batch nav jobs, ECS data layout, AggressiveInlining | Sleeping system, delayed update timers |
| **Maturity** | v0.1.11 (early) | v0.10.3 (mature) |

**Key takeaway**: These mods solve fundamentally different problems. QuestingBots gives bots purposeful, game-quest-aligned objectives with a full spawning system. Phobos provides a physics-inspired movement model (advection/convergence fields) that makes bots flow naturally across the map. They will conflict at the BigBrain layer level if run together, but their approaches are complementary in principle.

---

## 2. Project Overview

### Phobos

- **Author**: Janky (`com.janky.phobos`)
- **Version**: 0.1.11
- **Scope**: Procedural bot movement and squad coordination for out-of-combat situations
- **Approach**: Grid-based world partitioning with physics-like advection/convergence fields that push bots toward interesting areas near the player
- **Maturity**: Early-stage; two actions (GotoObjective, Guard), one strategy (GotoObjectiveStrategy)
- **Source**: `Phobos/Phobos/` (~62 C# files)

### SPTQuestingBots

- **Author**: DanW (`com.DanW.QuestingBots`)
- **Version**: 0.10.3
- **Scope**: Full quest-driven behavior, boss/follower coordination, PMC/PScav spawning, AI sleeping
- **Approach**: Bots receive actual game quest objectives (from 12 per-map JSON files) and navigate to complete them
- **Maturity**: Mature; 13 action types, extensive spawning system, broad bot-type support
- **Source**: `src/SPTQuestingBots.Client/` (~165 C# files) + `src/SPTQuestingBots.Server/` (9 C# files)

---

## 3. Architecture Comparison

### Project Structure

| Aspect | Phobos | SPTQuestingBots |
|--------|--------|-----------------|
| **Solution** | `Phobos.sln` (1 main project + Gym benchmark) | `SPTQuestingBots.sln` (2 main + 2 test projects) |
| **Target** | netstandard2.1 | Client: netstandard2.1, Server: net9.0 |
| **Build** | Standard .csproj | Makefile (`make build`, `make test`, `make ci`) |
| **Testing** | Gym project (BenchmarkDotNet) | NUnit 3.x + NSubstitute (55 server tests) |
| **CI** | None observed | GitHub Actions (`ci.yml`) |
| **Code style** | File-scoped namespaces, primary constructors | Block-scoped namespaces, traditional constructors |

### Design Philosophy

**Phobos** uses an **ECS-inspired architecture** where data (Components) is separated from logic (Systems):

```
Entities/     # Agent, Squad - thin identity types with component references
Components/   # Movement, Look, Objective, Stuck, Guard - pure data
Systems/      # MovementSystem, LookSystem, LocationSystem, DoorSystem - logic
Tasks/        # Actions (per-agent) and Strategies (per-squad) - utility AI
Data/         # Dataset, ComponentArray, EntityArray - ECS data containers
```

Reference: `Phobos/Phobos/Orchestration/PhobosManager.cs` - central orchestrator that ticks all systems in sequence.

**QuestingBots** uses a **MonoBehaviour + static controller pattern**:

```
BehaviorExtensions/  # CustomLayer/CustomLogic base classes, action type enum
BotLogic/            # Objective/, Follow/, Sleep/, HiveMind/ - behavior per concern
Components/          # MonoBehaviour components (BotObjectiveManager, LocationData)
Controllers/         # Static singletons (ConfigController, LoggingController)
Models/              # Data classes for quests, pathing, spawning
Patches/             # Harmony patches organized by category
```

Reference: `src/SPTQuestingBots.Client/BehaviorExtensions/CustomLayerDelayedUpdate.cs` - base class for all custom layers.

### Key Architectural Differences

| Concern | Phobos | SPTQuestingBots |
|---------|--------|-----------------|
| **State management** | Structured component arrays on entities | MonoBehaviour components on GameObjects |
| **Update loop** | PhobosManager.Update() ticks all systems | BigBrain calls IsActive()/Update() per layer/logic |
| **Extensibility** | Delegate hooks for registering new actions/strategies | Reflection-based type discovery |
| **Singletons** | `Comfort.Common.Singleton<T>` | Static classes + MonoBehaviour `GetOrAddComponent` |

---

## 4. BigBrain Integration

### Layer Registration

**Phobos** registers a single layer:

```csharp
// Phobos/Plugin.cs:110
BrainManager.AddCustomLayer(typeof(PhobosLayer), brains, 19);
```

**QuestingBots** registers four layers in `BehaviorExtensions/CustomLayerForQuesting.cs` (called from patches):

| Layer | Class | Priority | Update Interval | Brain Scope |
|-------|-------|----------|-----------------|-------------|
| Sleeping | `SleepingLayer` | 99 | 250ms | ALL (including snipers) |
| Regrouping | `BotFollowerRegroupLayer` | 26 | 25ms | ALL |
| Following | `BotFollowerLayer` | 19 | 25ms | ALL |
| Questing | `BotObjectiveLayer` | 18 | 25ms | Non-sniper only |

### Brain Types Supported

| Brain Type | Phobos | QuestingBots |
|------------|--------|--------------|
| PMC / PmcUsec / PmcBear | Yes | Yes |
| Assault (Scavs) | Yes | Yes |
| Knight / BigPipe / BirdEye | Yes | Yes |
| SectantPriest / SectantWarrior | Yes | Yes |
| ExUsec (Rogues) | No | Yes |
| BossBully / BossKilla / etc. | No | Yes |
| Follower types | No (implicit via squad) | Yes (20+ types) |
| CursAssault / Marksman | No | Yes |
| ArenaFighter / Bloodhound | No | Yes |

### Layer Activation Logic

**Phobos** (`PhobosLayer.IsActive()` at `Phobos/PhobosLayer.cs:105-111`):
```csharp
var isHealing = (BotOwner.Medecine.Using || ...) && lastEnemyTimeSeen < 60f;
var isInCombat = BotOwner.Memory.IsUnderFire || BotOwner.Memory.HaveEnemy || lastEnemyTimeSeen < 15f;
return !isHealing && !isInCombat;
```
Simple: active when not healing and not in combat (no enemy seen for 15s+).

**QuestingBots** (`BotObjectiveLayer.IsActive()` at `src/.../BotLogic/Objective/BotObjectiveLayer.cs:35-75`):
- Throttled via `canUpdate()` (returns cached `previousState` between intervals)
- Checks `BotQuestingDecisionMonitor.IsAllowedToQuest()`
- Checks for questing boss (followers defer)
- Handles pause requests, regroup decisions
- Determines quest action type from `objectiveManager.CurrentQuestAction`

### Action Dispatch

**Phobos**: Returns `DummyAction` (an empty `CustomLogic`). All behavior runs through `PhobosManager.Update()`, completely bypassing BigBrain's action dispatch:

```csharp
// PhobosLayer.cs:100-103
public override Action GetNextAction()
{
    return new Action(typeof(DummyAction), "Dummy Action");
}
```

**QuestingBots**: Uses BigBrain's full action dispatch with 13 distinct `CustomLogic` implementations:

```csharp
// CustomLayerDelayedUpdate.cs:61-87
switch (nextAction)
{
    case BotActionType.GoToObjective:
        return new Action(typeof(GoToObjectiveAction), actionReason);
    case BotActionType.FollowBoss:
        return new Action(typeof(FollowBossAction), actionReason);
    // ... 11 more action types
}
```

---

## 5. Bot Behavior Systems

### Decision-Making

**Phobos** uses a **Utility AI** system with two levels:

1. **Strategies** (squad-level): Evaluated per squad, assign objectives to squads
   - `GotoObjectiveStrategy` (base utility: 0.25) - picks grid cells using advection/convergence fields
2. **Actions** (agent-level): Evaluated per agent, scored with utility functions
   - `GotoObjectiveAction` (utility 0.5-0.65) - move toward assigned objective
   - `GuardAction` (utility 0-0.65) - hold position at a cover point near objective

Each action computes a score per agent based on distance and state. The highest-scoring action wins, with hysteresis to prevent rapid switching.

Reference: `Phobos/Phobos/Tasks/Actions/GotoObjectiveAction.cs:12-46` - utility scoring with distance-based boost.

**QuestingBots** uses a **hierarchical state machine**:

1. **Quest** -> **Objective** -> **Step** (data hierarchy)
2. `BotObjectiveManager` tracks current quest progress
3. `BotQuestingDecisionMonitor` decides: Quest, FollowBoss, Regroup, or idle
4. `QuestAction` enum determines specific behavior: MoveToPosition, HoldAtPosition, Ambush, Snipe, PlantItem, ToggleSwitch, CloseNearbyDoors, RequestExtract

Reference: `src/.../BotLogic/Objective/BotObjectiveLayer.cs:77-149` - quest action dispatch.

### Squad/Group Coordination

| Aspect | Phobos | QuestingBots |
|--------|--------|--------------|
| **Model** | Explicit `Squad` entity with `Leader` + `Members` list | `BotHiveMindMonitor` tracking boss/follower dictionaries |
| **Formation** | BSG squad detection + optional scav squads | BSG boss/follower + custom PMC group spawning |
| **Leader selection** | First member, reassigned on death | BSG's native boss system |
| **Shared state** | `SquadObjective` with guard timers | HiveMind sensors (5 types) |
| **Death handling** | `RemoveAgent()` on death event | Dead bot tracking, follower reassignment |
| **Separation** | Not supported | `SeparateBotFromGroup()` with full group restructuring |

**Phobos squad mechanics** (`Phobos/Phobos/Entities/Squad.cs`):
- Squads are first-class entities with their own strategy scoring
- `SquadRegistry` maps BSG bot groups to Phobos squads
- Leader manages guard timers, objective selection
- Guard duration is configurable with Gaussian sampling for natural variation

**QuestingBots HiveMind** (`src/.../BotLogic/HiveMind/BotHiveMindMonitor.cs`):
- 5 sensor types: InCombat, IsSuspicious, CanQuest, CanSprintToObjective, WantsToLoot
- Sensors track state per-bot and aggregate for boss/followers/group
- Boss follower relationships maintained via `botBosses` and `botFollowers` dictionaries
- Full group separation logic for spawned PMCs

---

## 6. Navigation & Movement

### Pathfinding

| Aspect | Phobos | QuestingBots |
|--------|--------|--------------|
| **Path calculation** | Custom `NavJobExecutor` with batch processing | BSG's `BotOwner.FollowPath()` |
| **Path following** | Custom `MovementSystem` with `Player.Move()` | BSG's native mover |
| **Batching** | Ramped batch size (queue/2, max 5 per frame) | One path at a time |
| **BSG mover** | Paused (`bot.Mover.Pause = true`, `bot.Mover.Stop()`) | Active (BSG handles movement) |
| **Door handling** | Custom: opens doors within 3m in current voxel | BSG's door handling + custom unlock/close actions |
| **Sprint logic** | Custom: checks outside, smooth path, angle jitter | Config-driven with door awareness, path corner checks |

**Phobos** (`Phobos/Phobos/Systems/MovementSystem.cs`) takes full control of bot movement:
- Calculates NavMesh paths in batches via `NavJobExecutor` (`Phobos/Phobos/Navigation/NavJobExecutor.cs`)
- Follows path corners with configurable epsilon (walk: 0.35m, sprint: 0.6m)
- Applies path-deviation spring force for smoother movement
- Sprint gated by: outdoor environment, smooth path (angle jitter < 20-45 deg based on urgency), ability to sprint
- Direct `Player.Move()` calls with calculated move direction

**QuestingBots** (`src/.../BehaviorExtensions/GoToPositionAbstractAction.cs`) delegates to BSG:
- Calculates path via `ObjectiveManager.BotPath.CheckIfUpdateIsNeeded()`
- Hands path to `BotOwner.FollowPath()` for execution
- Monitors for path completion, recalculates when target changes

### Objective Selection

**Phobos** uses a **physics-inspired spatial system** (`Phobos/Phobos/Systems/LocationSystem.cs`):
- World divided into grid cells based on map geometry (from `Maps/Geometry.json`)
- **Advection field**: zones defined per map pull/push bots via configurable force vectors
- **Convergence field**: radiates from player positions, drawing bots toward the player
- Squads assigned to grid cells based on combined field forces and congestion avoidance
- Runtime-tunable via BepInEx config (radius scale, force scale, decay)

**QuestingBots** uses **game quest data**:
- 12 per-map JSON files define quest objectives with positions
- `eftQuestSettings.json` + `zoneAndItemQuestPositions.json` for quest metadata
- Server serves quest data to client via HTTP endpoints
- `BotObjectiveManager` assigns quests based on bot role, current progress, distance

### Stuck Detection

| Stage | Phobos (Soft) | Phobos (Hard) | QuestingBots |
|-------|--------------|---------------|--------------|
| **Detection** | Speed below threshold for time | Position within radius for 5s+ | Position unchanged for configurable time |
| **Stage 1** | Vault at 1.5s | Path retry at 5s | Jump attempt |
| **Stage 2** | Jump at 3s | Teleport at 10s | Vault attempt |
| **Stage 3** | Fail at 6s | Give up at 15s | Mark stuck, skip objective |
| **Teleport** | Yes (with player proximity + LOS checks) | N/A | No |
| **Speed tracking** | Asymmetric EWMA for speed estimation | Rolling average + position history | Simple position delta |

Phobos's stuck remediation is notably more sophisticated:
- **Soft stuck** (`MovementSystem.cs:397-479`): Tracks actual vs expected speed with asymmetric exponential weighting
- **Hard stuck** (`MovementSystem.cs:483-633`): Uses position history ring buffer, rolling speed average, and multi-stage escalation including teleport with full visibility/proximity safety checks against human players

---

## 7. Patching Strategy

### Patch Count

| Category | Phobos | QuestingBots |
|----------|--------|--------------|
| Lifecycle | 3 | 3 |
| Layer bypass | 3 | 0 |
| Movement/Nav | 4 | 1 |
| Spawning | 0 | 12 |
| Combat/AI | 0 | 4 |
| Map-specific | 0 | 3 |
| Debug | 0 | 2 |
| Scav limits | 0 | 4 |
| **Total** | **12** | **35** (29 + 6 conditional) |

### All Phobos Patches

| Patch | Target | Type | Purpose |
|-------|--------|------|---------|
| `PhobosInitPatch` | `BotsController.Init` | Postfix | Create PhobosManager singleton |
| `PhobosFrameUpdatePatch` | `AICoreControllerClass.Update` | Postfix | Main update loop tick |
| `PhobosDisposePatch` | `GameWorld.Dispose` | Postfix | Cleanup singletons |
| `BypassAssaultEnemyFarPatch` | `GClass45.ShallUseNow` | Prefix | Disable BSG far-enemy layer |
| `BypassExfiltrationPatch` | `GClass75.ShallUseNow` | Prefix | Disable BSG extraction layer (priority 79) |
| `BypassPtrlBirdEyePatch` | `GClass79.ShallUseNow` | Prefix | Disable BSG patrol-BirdEye layer |
| `BotMoverSoftTeleportLogPatch` | `BotMover.Teleport` | Prefix | Debug logging |
| `BotMoverHardTeleportLogPatch` | `BotMover.method_10` | Prefix | Debug logging |
| `MovementContextIsAIPatch` | `MovementContext.IsAI` | Prefix | Enable player-like movement for AI |
| `EnableVaultPatch` | `Player.InitVaultingComponent` | Prefix | Enable AI vaulting |
| `BotMoverManualFixedUpdatePatch` | `BotMover.ManualFixedUpdate` | Prefix | Skip BSG mover when Phobos active |
| `ShrinkDoorNavMeshCarversPatch` | `GameWorld.OnGameStarted` | Postfix | Fix door nav blocking |

### All QuestingBots Patches

**Core (13)**:

| Patch | Target | Purpose |
|-------|--------|---------|
| `TarkovInitPatch` | Game init | Version validation |
| `BotsControllerSetSettingsPatch` | `BotsController` | Initialize quest data on raid start |
| `BotsControllerStopPatch` | `BotsController` | Cleanup on raid end |
| `BotOwnerBrainActivatePatch` | `BotOwner` | Register bot with BigBrain layers |
| `MenuShowPatch` | Menu | Show mod status |
| `CheckLookEnemyPatch` | Enemy detection | Filter PMC-on-PMC hostility |
| `OnBeenKilledByAggressorPatch` | Death handling | Track kills, update spawning |
| `AirdropLandPatch` | Airdrop | Handle airdrop quest objectives |
| `BotOwnerSprintPatch` | Sprint | Custom sprint control |
| `ServerRequestPatch` | HTTP requests | Intercept server communication |
| `ReturnToPoolPatch` | Bot pool | Cleanup on despawn |
| `IsFollowerSuitableForBossPatch` | Boss system | Custom follower assignment |
| `PScavProfilePatch` | Profile | Player-scav profile generation |

**Spawning (12)**: GameStart, LoadingScreen, ActivateBossesByWave, AddEnemy, TryLoadBotsProfiles, BotsGroupIsPlayerEnemy, SetNewBoss, GetAllBossPlayers, InitBossSpawnLocation, GetListByZone, ExceptAI, BotDied, TryToSpawnInZoneAndDelay

**Scav Limits (4)**: SpawnPointIsValid, TrySpawnFreeAndDelay, NonWavesSpawnScenarioCreate, BotsControllerStop

**Lighthouse (3)**: MineDirectionalShouldExplode, LighthouseTraderZoneAwake, LighthouseTraderZonePlayerAttack

**Debug (2)**: ProcessSourceOcclusion, HandleFinishedTask

### Potential Patch Conflicts

| Conflict Area | Risk | Details |
|---------------|------|---------|
| `BotMover.ManualFixedUpdate` | **HIGH** | Phobos skips BSG mover; QuestingBots relies on it |
| `MovementContext.IsAI` | **MEDIUM** | Phobos changes AI movement type; may affect QuestingBots pathing |
| `BotsController.Init` | **LOW** | Both hook init but do different things |
| `GameWorld.Dispose` | **LOW** | Both clean up independently |
| BSG layer bypass | **MEDIUM** | Phobos disables some BSG layers that QuestingBots may expect to exist |
| `Player.InitVaultingComponent` | **LOW** | Phobos enables vaulting; QuestingBots also handles vault in stuck detection |

---

## 8. Configuration Systems

### Phobos Configuration

**BepInEx config** (`Phobos/Plugin.cs:113-212`):
- **General**: Scav squad formation toggle
- **Objectives**: Guard duration ranges (base, adjusted, cut factor) as `Vector2` min/max
- **Zones**: Convergence radius/force scale, advection zone radius/force/decay scales (0-10 range)
- **Diagnostics**: Camera coords, advection grid, movement gizmos, debug logging toggle
- Runtime-changeable zone settings via `SettingChanged` events that recalculate fields

**JSON config** (`Phobos/Config/`):
- `Maps/Geometry.json`: Map bounds for grid partitioning
- `Maps/Zones/{mapId}.json`: Zone definitions per map (attractors/repulsors)
- Loaded via `ConfigBundle<T>` with hot-reload support

### QuestingBots Configuration

**Server config** (`config.json`):
- Loaded by `QuestingBotsConfigLoader` on server startup
- Served to client via `QuestingBotsStaticRouter` (6 HTTP endpoints)
- Controls: bot spawning, quest settings, PMC/PScav parameters, hostility, debug

**Client config** (`src/.../Configuration/ModConfig.cs`):
- `ConfigController.GetConfig()` fetches from server at startup
- BepInEx F12 menu options via `QuestingBotsPluginConfig`
- Per-map sleeping distances, bot type exceptions, quest toggles
- Extensive sleeping configuration: min distance to humans (per map), min distance to questing bots, sleepless bot types

**Quest data** (12 per-map JSON files + metadata):
- `eftQuestSettings.json`: Quest requirements and settings
- `zoneAndItemQuestPositions.json`: Zone/item positions
- Map-specific files: objective positions, step sequences

### Comparison

| Aspect | Phobos | QuestingBots |
|--------|--------|--------------|
| **Config source** | BepInEx + JSON files | Server HTTP + BepInEx F12 |
| **Hot reload** | Zone configs via SettingChanged | Requires restart for most settings |
| **Per-map tuning** | Geometry.json + Zones/{map}.json | 12 quest files + per-map sleeping distances |
| **Complexity** | ~15 config entries | 50+ config entries |
| **Visual debugging** | Gizmo toggles in BepInEx config | Debug paths via F12 menu |

---

## 9. Server vs Client Architecture

### QuestingBots Server Component

QuestingBots has a dedicated **server plugin** (`src/SPTQuestingBots.Server/`) that runs inside the SPT server process:

**Plugin** (`QuestingBotsServerPlugin.cs`):
- `[Injectable]` singleton with `IOnLoad` lifecycle
- Detects conflicting mods (SWAG, MOAR, Donuts) via `IReadOnlyList<SptMod>`
- Loads config via `ConfigServer.GetConfig<T>()`

**Routers**:
- `QuestingBotsStaticRouter`: 6 endpoints serving quest data, map data, config
- `QuestingBotsDynamicRouter`: 1 endpoint for dynamic bot data
- `QuestingBotGenerateRouter`: Overrides `BotStaticRouter` to intercept bot generation

**Services**:
- `BotLocationService`: Manages bot spawn locations and zones
- `PMCConversionService`: Converts bot profiles to PMC variants
- `CommonUtils`: Shared utility functions

### Phobos: Pure Client

Phobos has **no server component**. All logic runs client-side:
- Zone/map data stored as JSON files bundled with the plugin
- No HTTP communication needed
- No bot generation or profile modification
- Simpler deployment (single DLL + config files)

### Trade-offs

| Aspect | Server+Client (QuestingBots) | Pure Client (Phobos) |
|--------|------------------------------|----------------------|
| **Deployment** | Must install server + client | Single BepInEx plugin |
| **Data access** | Full server database (quests, items, profiles) | Limited to client-side game data |
| **Bot generation** | Can create custom PMC/PScav profiles | Works with existing bots only |
| **Mod conflicts** | Can detect + warn about conflicts server-side | Client-only detection |
| **Maintenance** | Two codebases to update | One codebase |

---

## 10. Bot Spawning & Lifecycle

### QuestingBots Spawning System

QuestingBots has an extensive bot spawning subsystem:

**Generators** (`src/.../Components/Spawning/`):
- `BotGenerator` (abstract base): Async bot generation framework
- `PMCGenerator`: Creates PMC bots with proper profiles, gear, and spawn timing
- `PScavGenerator`: Creates player-scav bots with loot and behavior

**Registration** (`BotRegistrationManager`):
- Tracks bot types: Scav, PScav, PMC, Boss
- Manages spawn groups (`BotSpawnInfo`)
- Controls spawn rates and caps per map

**Server-side generation**:
- `QuestingBotGenerateRouter` overrides bot generation to inject PMC/PScav profiles
- `PMCConversionService` handles profile conversion

**12 spawning patches** control:
- When bosses spawn (wave-based)
- How spawn points are validated
- Enemy/ally relationships for spawned bots
- Scav population limits

### Phobos Lifecycle

Phobos does not spawn bots. It only manages bots that already exist:

**Registration** (`PhobosLayer` constructor, `PhobosLayer.cs:33-56`):
- Disables BSG's `StandBy` system (prevents far-away bot deactivation)
- Registers agent with `PhobosManager.AddAgent()`
- Sets up door collision ignoring for all doors
- Subscribes to layer change and death events

**Cleanup** (`PhobosLayer.OnDead()`, `PhobosLayer.cs:58-63`):
- Unsubscribes death event
- Marks agent inactive
- Removes from PhobosManager

**BSG handoff** (`PhobosLayer.OnLayerChanged()`, `PhobosLayer.cs:65-93`):
- When Phobos gains control: stops BSG mover, marks agent active
- When Phobos loses control: resets mover state variables, calls `SetPlayerToNavMesh()`, marks inactive

### Comparison

| Aspect | Phobos | QuestingBots |
|--------|--------|--------------|
| **Spawns bots** | No | Yes (PMCs + PScavs) |
| **Despawns bots** | No | Yes (via extraction system) |
| **Bot type tracking** | Agent entity only | Full type registry (Scav/PScav/PMC/Boss) |
| **StandBy disable** | Yes (prevents far deactivation) | No (uses sleeping system instead) |
| **Death handling** | Remove agent from systems | Track dead bots, update follower chains |
| **BSG mover** | Fully replaces | Coexists with |

---

## 11. Performance Considerations

### Phobos Performance Patterns

1. **Batch Nav Jobs** (`NavJobExecutor.cs:33-47`):
   - Queue-based pathfinding with ramped batch size
   - Spreads calculation across frames: `Min(queue/2, batchSize)` per frame
   - Avoids frame spikes from multiple simultaneous path calculations

2. **ECS Data Layout** (`Data/`):
   - `ComponentArray<T>`, `EntityArray<T>` for cache-friendly iteration
   - Systems iterate over contiguous arrays rather than scattered MonoBehaviours

3. **Micro-optimizations**:
   - `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot path methods
   - Squared distances throughout (`sqrMagnitude` instead of `Distance()`)
   - Object pooling for nav jobs and queues
   - Ring buffers for position history in stuck detection
   - `TimePacing` for rate-limited updates

4. **Frame Budget**: Single `PhobosManager.Update()` call per frame ticks all systems in sequence, giving predictable frame cost.

### QuestingBots Performance Patterns

1. **Sleeping System** (`src/.../BotLogic/Sleep/SleepingLayer.cs`):
   - Priority 99 (highest) - takes precedence over all other behavior
   - Disables bots far from human players (configurable per-map distances)
   - Minimum bot count threshold before sleeping activates
   - Exemptions for certain bot types and questing bots
   - Effectively reduces active bot count to those near the player

2. **Delayed Update Base Classes**:
   - `CustomLayerDelayedUpdate`: configurable update intervals (25ms, 100ms, 250ms)
   - `MonoBehaviourDelayedUpdate`: similar throttling for MonoBehaviour components
   - `SleepingLayer` uses 250ms interval (only checks 4 times/second)
   - `BotObjectiveLayer` uses 25ms interval (40 times/second)

3. **HiveMind Update Throttling** (`BotHiveMindMonitor.cs:38`):
   - 50ms update interval for sensor aggregation
   - Batch-updates all sensors per cycle

### Comparison

| Technique | Phobos | QuestingBots |
|-----------|--------|--------------|
| **Bot culling** | Disables StandBy (keeps all active) | Sleeping system disables far bots |
| **Update throttling** | Per-system update rates | Per-layer/component intervals |
| **Pathfinding** | Batched across frames | One-at-a-time via BSG |
| **Data access** | Contiguous arrays (cache-friendly) | Dictionary lookups, LINQ |
| **Distance calcs** | Squared distances throughout | Mix of Distance() and sqrMagnitude |
| **Memory** | Pre-allocated arrays, pooling | Dynamic collections, LINQ allocations |

---

## 12. Compatibility & Conflicts

### Running Both Mods Together

**BigBrain Layer Priority Conflict**:
- Phobos: priority 19
- QuestingBots Following: priority 19
- QuestingBots Questing: priority 18

When both are loaded, for bots whose brain types are registered with both mods, Phobos and QuestingBots' Following layer would compete at priority 19. BigBrain resolves same-priority layers by registration order, which depends on BepInEx plugin load order.

**Movement System Conflict**:
- Phobos patches `BotMover.ManualFixedUpdate` to skip BSG's mover when Phobos is active
- QuestingBots relies on BSG's mover via `BotOwner.FollowPath()`
- If Phobos takes control, QuestingBots' path following will not execute
- If QuestingBots takes control, Phobos may still intercept the mover update

**BSG Layer Bypass**:
- Phobos disables 3 BSG layers (assault-enemy-far, exfiltration, patrol-birdeye)
- These disables are global (prefix returning false), affecting all bots regardless of which mod controls them
- QuestingBots' extraction system may be affected by the exfiltration layer bypass

### Shared Dependencies

| Dependency | Phobos | QuestingBots |
|------------|--------|--------------|
| BigBrain | Yes (via `xyz.drakia.waypoints`) | Yes (`xyz.drakia.bigbrain` 1.3.2) |
| Waypoints | Yes (direct dependency) | Yes (via BigBrain) |
| SPT.Core | No dependency declared | Yes (`com.SPT.core` 4.0.0) |
| BepInEx | Yes | Yes |

### Incompatible Mods

QuestingBots explicitly declares incompatibilities:
```csharp
// QuestingBotsPlugin.cs:16-17
[BepInIncompatibility("com.pandahhcorp.aidisabler")]
[BepInIncompatibility("com.dvize.AILimit")]
```

Plus server-side detection of: SWAG, MOAR, Donuts (spawning conflicts).

Phobos declares no incompatibilities.

---

## 13. Code Quality & Patterns

### Language Features

| Feature | Phobos | QuestingBots |
|---------|--------|--------------|
| **C# version** | Modern (primary constructors, file-scoped namespaces) | Traditional (block-scoped namespaces) |
| **Null handling** | Pattern matching (`is not { IsAlive: true }`) | Null checks + early return |
| **Collections** | List initializers `[]`, tuples | `new List<T>()`, traditional patterns |
| **String formatting** | Interpolation throughout | Concatenation + some interpolation |
| **Access modifiers** | `internal` classes, `public` data | `internal` classes, mixed access |

### Code Organization

**Phobos**: Clean separation of concerns via ECS-like architecture. Each system is self-contained. Navigation, movement, location, and door handling are independent systems composed by PhobosManager. Actions and strategies are pluggable via `DefinitionRegistry<T>`.

**QuestingBots**: Feature-organized with clear domain boundaries. BotLogic split by concern (Objective, Follow, Sleep, HiveMind). Static controllers provide global access. Patches organized by category in subdirectories.

### Testing

| Aspect | Phobos | QuestingBots |
|--------|--------|--------------|
| **Unit tests** | None (Gym project is benchmarks) | NUnit + NSubstitute |
| **Test count** | 0 | 55 (server tests) |
| **CI** | None | GitHub Actions |
| **Benchmarks** | BenchmarkDotNet in Gym/ | None |
| **Formatting** | Not configured | CSharpier + .editorconfig |
| **Linting** | ReSharper annotations | Roslyn analyzers |

### Error Handling

**Phobos**: Minimal error handling; trusts internal state. Uses `Log.Debug()` for diagnostic output. Can disable logging entirely for performance.

**QuestingBots**: Extensive logging via `LoggingController` with Info/Warning/Error levels. Defensive null checks throughout. Dead bot tracking prevents null reference on despawned bots.

---

## 14. Lessons & Recommendations

### What QuestingBots Could Learn from Phobos

1. **Custom movement system**: Phobos's path-deviation spring force and custom path following produce smoother bot movement than BSG's native mover. QuestingBots could benefit from similar smoothing.

2. **Batched pathfinding**: Phobos's `NavJobExecutor` spreads path calculations across frames, preventing spikes. QuestingBots calculates paths synchronously which can cause frame drops with many active bots.

3. **ECS data layout**: Phobos's contiguous component arrays are more cache-friendly than QuestingBots' dictionary-based lookups and MonoBehaviour component access patterns.

4. **Stuck detection sophistication**: Phobos's two-tier stuck detection with asymmetric EWMA speed tracking, position history ring buffers, and safe teleportation is more robust than QuestingBots' timer-based approach.

5. **Utility AI**: Phobos's score-based action selection with hysteresis is more extensible than QuestingBots' enum-based switch statement. New behaviors can be added without modifying existing dispatch code.

6. **BSG mover handoff**: Phobos's careful state restoration when returning control to BSG (`OnLayerChanged` in `PhobosLayer.cs:65-93`) prevents the "bot standing still" bug that can occur when another layer takes over mid-path.

### What Phobos Could Learn from QuestingBots

1. **Quest-driven purpose**: Phobos bots move procedurally but lack purpose. QuestingBots' quest data gives bots meaningful objectives that align with the game's quest system, making them feel more realistic.

2. **Bot spawning**: QuestingBots' PMC/PScav generation system creates a dynamic population. Phobos only works with existing bots and cannot adjust spawn rates or types.

3. **Sleeping system**: QuestingBots' distance-based sleeping is highly effective for performance. Phobos disables BSG's StandBy but doesn't replace it, meaning all bots are always active.

4. **Broad bot-type support**: QuestingBots supports 20+ brain types including all bosses, followers, rogues, raiders, and cultists. Phobos supports 9 types.

5. **Server component**: Having quest data and bot generation on the server allows QuestingBots to leverage the full game database. Phobos is limited to client-side data.

6. **Testing infrastructure**: QuestingBots has 55 unit tests and CI. Phobos has none, relying on manual testing and benchmarks.

7. **Mod conflict detection**: QuestingBots actively detects and warns about conflicting mods both via BepInEx attributes and server-side checks. Phobos has no conflict detection.

8. **Diverse actions**: QuestingBots has 13 action types (ambush, snipe, plant item, unlock door, toggle switch, etc.) that create varied bot behavior. Phobos has 2 actions (goto objective, guard).

### Integration Possibilities

If both mods were to coexist:
- Phobos could provide the movement system while QuestingBots provides objectives
- QuestingBots' sleeping system could gate which bots Phobos processes
- Phobos's advection/convergence fields could influence QuestingBots' objective selection as a fallback when no quest is assigned
- A shared "bot intent" interface could allow both mods to negotiate control of individual bots based on context (e.g., Phobos handles general movement, QuestingBots takes over for specific quest actions)
