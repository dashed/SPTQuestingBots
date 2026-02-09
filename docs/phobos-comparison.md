# Phobos vs SPTQuestingBots: Technical Comparison

## 1. Executive Summary

**Phobos** and **SPTQuestingBots** are both SPT (Single Player Tarkov) mods that control AI bot behavior outside of combat. They share the same foundational dependency (BigBrain) and operate at the same layer priority level (19 and 18), making them natural competitors for bot attention during out-of-combat time.

| Aspect | Phobos | SPTQuestingBots |
|--------|--------|-----------------|
| **Purpose** | Procedural movement via grid/zone fields | Quest-driven objectives + zone-based fallback movement |
| **Architecture** | Pure client (ECS-like, ~62 files) | Client + Server (ECS-Lite + MonoBehaviour, ~180 files) |
| **BigBrain layers** | 1 (priority 19) | 4 (priorities 99, 26, 19, 18) |
| **Navigation** | Custom movement system with own pathfinding | BSG's built-in `BotOwner.FollowPath()` + PathfindingThrottle |
| **Squad model** | Explicit Squad entities with leader/member | HiveMind sensor-based boss/follower tracking |
| **Decision-making** | Utility AI (scored actions/strategies) | State machine (quest action enum) + zone field fallback |
| **Zone/field system** | Advection + convergence fields from JSON per-map configs | Advection + convergence fields, auto-detected from spawn points, per-bot momentum + noise |
| **Bot spawning** | None (uses whatever spawns) | Custom PMC/PScav generation system (optional) |
| **Stuck detection** | Two-tier: soft (EWMA) + hard (ring buffer + teleport) | Two-tier: soft (EWMA) + hard (ring buffer + teleport) — ported from Phobos |
| **Performance** | Batch nav jobs, ECS data layout, AggressiveInlining | Batch nav jobs, PathfindingThrottle, sleeping system, ECS-Lite dense iteration, AggressiveInlining, static buffers |
| **Maturity** | v0.1.11 (early) | v1.7.0 (C# port with ECS-Lite data layout) |

**Key takeaway**: These mods have converged significantly. QuestingBots now incorporates Phobos-inspired features (ECS-Lite dense entity storage with swap-remove, zone-based movement fields with per-bot momentum/noise, two-tier stuck detection, batched pathfinding, AggressiveInlining on hot paths) alongside its original quest-driven objective system. The primary remaining differences are: Phobos takes full control of bot movement (custom `Player.Move()` calls), while QuestingBots delegates to BSG's mover; Phobos uses per-map JSON zone configurations, while QuestingBots auto-detects zones from spawn points; and QuestingBots has a full bot spawning system and server component that Phobos lacks.

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

- **Author**: DanW (`com.DanW.QuestingBots`), C# port by Alberto Leal
- **Version**: 1.7.0
- **Scope**: Full quest-driven behavior, zone-based fallback movement, ECS-Lite data layout, boss/follower coordination, PMC/PScav spawning (optional), AI sleeping
- **Approach**: Bots receive actual game quest objectives (from 12 per-map JSON files) and navigate to complete them. When no quests are available, a Phobos-inspired zone movement system uses advection/convergence fields to guide bots toward interesting map areas. An ECS-Lite data layout (dense entity list with swap-remove, inspired by Phobos's EntityArray) provides centralized bot state, static system methods, and zero-allocation sensor iteration.
- **Maturity**: Mature; 13 action types, extensive spawning system, broad bot-type support, zone movement with debug overlay, ECS-Lite entity storage with 537 tests
- **Source**: `src/SPTQuestingBots.Client/` (~180 C# files) + `src/SPTQuestingBots.Server/` (9 C# files)

---

## 3. Architecture Comparison

### Project Structure

| Aspect | Phobos | SPTQuestingBots |
|--------|--------|-----------------|
| **Solution** | `Phobos.sln` (1 main project + Gym benchmark) | `SPTQuestingBots.sln` (2 main + 2 test projects) |
| **Target** | netstandard2.1 | Client: net472, Server: net9.0, Tests: net9.0 |
| **Build** | Standard .csproj | Makefile (`make build`, `make test`, `make ci`) |
| **Testing** | Gym project (BenchmarkDotNet) | NUnit 3.x + NSubstitute (479 client + 58 server = 537 tests) |
| **CI** | None observed | GitHub Actions (`ci.yml`) |
| **Code style** | File-scoped namespaces, primary constructors | Block-scoped namespaces, traditional constructors, CSharpier formatting |

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

**QuestingBots** uses an **ECS-Lite data layout + MonoBehaviour integration pattern** with a **pure-logic zone movement layer**:

```
BotLogic/ECS/        # BotEntity, BotRegistry — dense entity storage with swap-remove
  Systems/           #   HiveMindSystem, QuestScorer — static methods on dense lists
BotLogic/HiveMind/   # BotHiveMindMonitor — orchestrates ECS tick, BSG API bridge
BehaviorExtensions/  # CustomLayer/CustomLogic base classes, action type enum
BotLogic/            # Objective/, Follow/, Sleep/ - behavior per concern
Components/          # MonoBehaviour components (BotObjectiveManager, LocationData)
Controllers/         # Static singletons (ConfigController, LoggingController)
Models/              # Data classes for quests, pathing, spawning
Helpers/             # PositionHistory, RollingAverage, NavJob, PathfindingThrottle, TimePacing, FramePacing
ZoneMovement/        # Phobos-inspired grid + field system
  Core/              #   WorldGrid, GridCell, PointOfInterest, ZoneMathUtils
  Fields/            #   AdvectionField, ConvergenceField, FieldComposer
  Selection/         #   CellScorer, DestinationSelector, ZoneActionSelector
  Integration/       #   WorldGridManager, ZoneQuestBuilder, ZoneDebugOverlay, etc.
Patches/             # Harmony patches organized by category
```

Reference: `src/SPTQuestingBots.Client/BotLogic/ECS/BotEntityBridge.cs` - static adapter bridging BotOwner (game) to BotEntity (ECS).

### Key Architectural Differences

| Concern | Phobos | SPTQuestingBots |
|---------|--------|-----------------|
| **State management** | Structured component arrays on entities | ECS-Lite: dense `BotEntity` list + `BotEntityBridge` adapter for BSG interop |
| **Update loop** | PhobosManager.Update() ticks all systems | BigBrain calls IsActive()/Update() per layer/logic |
| **Zone movement** | Core architecture (everything flows through fields) | Fallback system (zone quests when no higher-priority quest) |
| **Pure logic separation** | Systems operate on component data | ZoneMovement/Core + Fields + Selection are pure logic (unit tested) |
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

**QuestingBots** uses a **hierarchical state machine** with **zone-based fallback**:

1. **Quest** -> **Objective** -> **Step** (data hierarchy)
2. `BotObjectiveManager` tracks current quest progress
3. `BotQuestingDecisionMonitor` decides: Quest, FollowBoss, Regroup, or idle
4. `QuestAction` enum determines specific behavior: MoveToPosition, HoldAtPosition, Ambush, Snipe, PlantItem, ToggleSwitch, CloseNearbyDoors, RequestExtract
5. **Zone movement fallback**: When no quest is available, `ZoneQuestBuilder` creates low-desirability objectives from grid cells. `ZoneActionSelector` picks contextual actions based on dominant POI category (e.g., Ambush near containers, Snipe near exfils).

Reference: `src/.../BotLogic/Objective/BotObjectiveLayer.cs:77-149` - quest action dispatch.

### Squad/Group Coordination

| Aspect | Phobos | QuestingBots |
|--------|--------|--------------|
| **Model** | Explicit `Squad` entity with `Leader` + `Members` list | `BotEntity` with `Boss`/`Followers` fields + `BotEntityBridge` adapter |
| **Formation** | BSG squad detection + optional scav squads | BSG boss/follower + custom PMC group spawning |
| **Leader selection** | First member, reassigned on death (`squad.Members[^1]`) | BSG's native boss system |
| **Shared state** | `SquadObjective` with guard timers | 5 sensor bools embedded on `BotEntity` + group query helpers |
| **Death handling** | `RemoveAgent()` on death event | `HiveMindSystem.CleanupDeadEntities()` with O(1) `IsActive` checks |
| **Separation** | Not supported | `HiveMindSystem.SeparateFromGroup()` with full group restructuring |

**Phobos squad mechanics** (`Phobos/Phobos/Entities/Squad.cs`):
- Squads are first-class entities with their own strategy scoring
- `SquadRegistry` maps BSG bot groups to Phobos squads
- Leader manages guard timers, objective selection
- Guard duration is configurable with Gaussian sampling for natural variation

**QuestingBots HiveMind** (`src/.../BotLogic/HiveMind/BotHiveMindMonitor.cs` + `BotLogic/ECS/`):
- 5 sensor bools embedded directly on `BotEntity`: `IsInCombat`, `IsSuspicious`, `CanQuest`, `CanSprintToObjective`, `WantsToLoot`
- Push sensors (InCombat, IsSuspicious, WantsToLoot) write only to ECS entities; pull sensors (CanQuest, CanSprintToObjective) iterate dense entity list with zero allocation
- Boss/follower hierarchy on `BotEntity.Boss` / `BotEntity.Followers` — O(1) dead checks via `IsActive`
- `HiveMindSystem`: static system methods for boss/follower cleanup, sensor resets, entity counting
- `BotEntityBridge`: static adapter bridging `BotOwner` (BSG) to `BotEntity` (ECS) with group query helpers
- Full group separation logic for spawned PMCs via `HiveMindSystem.SeparateFromGroup()`

---

## 6. Navigation & Movement

### Pathfinding

| Aspect | Phobos | QuestingBots |
|--------|--------|-----------------|
| **Path calculation** | Custom `NavJobExecutor` with batch processing | BSG's `BotOwner.FollowPath()` + `PathfindingThrottle` + `NavJobExecutor` |
| **Path following** | Custom `MovementSystem` with `Player.Move()` | BSG's native mover |
| **Batching** | Ramped batch size (queue/2, max 5 per frame) | `PathfindingThrottle` (max 5 NavMesh.CalculatePath/frame) + queue-based `NavJobExecutor` |
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
- **PathfindingThrottle** (`Helpers/PathfindingThrottle.cs`): Per-frame limiter for `NavMesh.CalculatePath` calls (max 5/frame) to prevent frame spikes with many bots
- **NavJobExecutor** (`Helpers/NavJobExecutor.cs`): Queue-based batched pathfinding with ramped batch size — same pattern as Phobos
- Monitors for path completion, recalculates when target changes

### Zone/Field-Based Objective Selection

Both mods now use physics-inspired spatial systems for directing bot movement:

| Aspect | Phobos | QuestingBots |
|--------|--------|-----------------|
| **Grid partitioning** | Map bounds from `Maps/Geometry.json` | Auto-detected from spawn point positions (`MapBoundsDetector`) |
| **Cell sizing** | Per-map configured | Auto-sized for ~150 cells per map (`WorldGrid`) |
| **Advection field** | Per-map JSON zone configs with force vectors | Zones auto-discovered from `BotZoneName` groups (`ZoneDiscovery`) |
| **Convergence field** | Radiates from player positions | Radiates from human players with sqrt-distance falloff, 30s update interval |
| **Crowd repulsion** | Congestion avoidance in field computation | Inverse-square falloff from other bot positions (`AdvectionField`) |
| **Field composition** | Combined field forces | `FieldComposer`: weighted advection + convergence + momentum + noise; per-bot via `BotFieldState` |
| **Per-bot state** | Momentum derived on-the-fly in `LocationSystem.RequestNear()` | `BotFieldState`: tracked `PreviousDestination` + seeded noise per bot, eliminates herd movement |
| **Destination selection** | Squad-assigned grid cells | `CellScorer` (direction alignment + POI density) → `DestinationSelector` (best navigable neighbor) → `ZoneObjectiveCycler` (field-based objective cycling) |
| **Objective cycling** | `GotoObjectiveStrategy.AssignNewObjective()` → `LocationSystem.RequestNear()` | `ZoneObjectiveCycler.SelectZoneObjective()` → `WorldGridManager.GetRecommendedDestination()` (integrated into `BotJobAssignmentFactory`) |
| **POI awareness** | Zone-level only | 6 categories: Container, LooseLoot, Quest, Exfil, SpawnPoint, Synthetic |
| **Action selection** | GotoObjective / Guard | 5 action types with category-weighted probabilities (`ZoneActionSelector`) |
| **Dynamic updates** | Runtime via zone config hot-reload | `WorldGridManager.Update()` caches live player/bot positions periodically |
| **Debug** | Gizmo toggles for advection grid, movement vectors | `ZoneDebugOverlay` text panel + 2D minimap (400px: cells, advection/convergence arrows, bot/player/zone dots, legend) |
| **Per-map config** | Required (`Maps/Geometry.json` + `Zones/{map}.json`) | None required — auto-detected from spawn points |

**Phobos** (`Phobos/Phobos/Systems/LocationSystem.cs`):
- World divided into grid cells based on map geometry (from `Maps/Geometry.json`)
- Zones defined per map in JSON with configurable force vectors and decay
- Squads assigned to grid cells based on combined field forces
- Runtime-tunable via BepInEx config (radius scale, force scale, decay)

**QuestingBots** (`src/.../ZoneMovement/`):
- `WorldGrid`: auto-sized 2D grid partitioning maps on XZ plane (~150 cells/map)
- `AdvectionField`: pushes bots toward geographic zones with crowd repulsion
- `ConvergenceField`: pulls bots toward human players with sqrt-distance falloff
- `FieldComposer`: combines advection, convergence, momentum, and noise with configurable weights
- `BotFieldState`: per-bot momentum (from `PreviousDestination`) + seeded noise, eliminating herd movement where all bots in the same cell choose the same direction
- `ZoneMathUtils.ComputeMomentum()`: normalized XZ-plane direction for per-bot momentum
- `CellScorer`: scores candidates by directional alignment + POI density blend
- `DestinationSelector`: picks best navigable neighbor cell
- `ZoneActionSelector`: maps POI categories to varied bot actions with weighted random selection
- `WorldGridManager.GetRecommendedDestination()`: two overloads — global (explicit momentum) and per-bot (auto-tracked momentum + noise via `BotFieldState`)
- `ZoneObjectiveCycler`: selects next zone objective using live field state instead of nearest-to-bot, matching Phobos's `GotoObjectiveStrategy.AssignNewObjective()` → `LocationSystem.RequestNear()` pattern
- Zone quests registered via `BotJobAssignmentFactory.AddQuest()` with low desirability (5) — zero changes to existing dispatch

### Quest-Based Objective Selection (QuestingBots Only)

QuestingBots has an additional layer that Phobos lacks entirely:
- 12 per-map JSON files define quest objectives with positions
- `eftQuestSettings.json` + `zoneAndItemQuestPositions.json` for quest metadata
- Server serves quest data to client via HTTP endpoints
- `BotObjectiveManager` assigns quests based on bot role, current progress, distance
- 13 action types: MoveToPosition, HoldAtPosition, Ambush, Snipe, PlantItem, ToggleSwitch, CloseNearbyDoors, RequestExtract, etc.

### Stuck Detection

Both mods now use a **two-tier stuck detection system**. QuestingBots ported its system directly from Phobos:

| Stage | Phobos (Soft) | Phobos (Hard) | QuestingBots (Soft) | QuestingBots (Hard) |
|-------|--------------|---------------|---------------------|---------------------|
| **Detection** | Speed below threshold | Position within radius for 5s+ | EWMA speed below threshold | Ring buffer position history |
| **Speed tracking** | Asymmetric EWMA | Rolling average + position history | Asymmetric EWMA (ported) | Rolling average + position history (ported) |
| **Y-axis** | Filtered out | Included | Filtered out | Included |
| **Stage 1** | Vault at 1.5s | Path retry at 5s | Vault at 1.5s | Path retry at 5s |
| **Stage 2** | Jump at 3s | Teleport at 10s | Jump at 3s | Teleport at 10s |
| **Stage 3** | Fail at 6s | Give up at 15s | Fail at 6s | Fail at 15s |
| **Teleport safety** | Proximity + LOS checks vs players | — | Proximity (<10m) + LOS checks vs players | — |

**QuestingBots implementations**:
- `SoftStuckDetector` (`Models/SoftStuckDetector.cs`): Asymmetric EWMA speed tracking, Y-axis filtering, vault→jump→fail escalation
- `HardStuckDetector` (`Models/HardStuckDetector.cs`): `PositionHistory` ring buffer + `RollingAverage` speed, path retry→teleport→fail escalation
- `PositionHistory` (`Helpers/PositionHistory.cs`): Ring buffer tracking N position samples
- `RollingAverage` (`Helpers/RollingAverage.cs`): Circular buffer with periodic drift correction

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

**Zone movement config** (`ZoneMovementConfig` — 12 properties under `questing.zone_movement`):

| Property | Default | Description |
|----------|---------|-------------|
| `enabled` | `true` | Master toggle |
| `target_cell_count` | `150` | Grid resolution target |
| `bounds_padding` | `50` | Padding around spawn-point bounds |
| `convergence_weight` | `0.4` | Player-attraction strength |
| `advection_weight` | `0.3` | Zone-push strength |
| `momentum_weight` | `0.2` | Direction continuity |
| `noise_weight` | `0.1` | Random exploration |
| `crowd_repulsion_strength` | `1.0` | Bot-to-bot repulsion |
| `convergence_update_interval_sec` | `30` | Position cache refresh rate |
| `poi_score_weight` | `0.3` | POI density vs direction blend |
| `quest_desirability` | `5` | Quest priority (low = fallback) |
| `quest_name` | `"Zone Movement"` | Quest name in assignment system |

**F12 menu entries** (BepInEx ConfigEntry):

| Entry | Default | Description |
|-------|---------|-------------|
| `ZoneMovementEnabled` | `true` | Runtime toggle, overrides JSON config |
| `ZoneMovementDebugOverlay` | `false` | Show debug text overlay (IsAdvanced) |
| `ZoneMovementDebugMinimap` | `false` | Show 2D minimap with cells, arrows, dots (IsAdvanced) |

**Quest data** (12 per-map JSON files + metadata):
- `eftQuestSettings.json`: Quest requirements and settings
- `zoneAndItemQuestPositions.json`: Zone/item positions
- Map-specific files: objective positions, step sequences

### Comparison

| Aspect | Phobos | QuestingBots |
|--------|--------|--------------|
| **Config source** | BepInEx + JSON files | Server HTTP + BepInEx F12 + JSON zone config |
| **Hot reload** | Zone configs via SettingChanged | F12 menu toggle for zone movement; most settings require restart |
| **Per-map tuning** | Required: Geometry.json + Zones/{map}.json | Auto-detected; no per-map config needed for zone movement |
| **Zone field tuning** | ~8 BepInEx entries (radius, force, decay scales) | 12 JSON properties (weights, intervals, cell count) + 2 F12 entries |
| **Complexity** | ~15 config entries | 50+ config entries |
| **Visual debugging** | Gizmo toggles in BepInEx config | F12 debug overlay + debug paths |

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

QuestingBots has an extensive bot spawning subsystem (disabled by default since v1.2.0):

**Generators** (`src/.../Components/Spawning/`):
- `BotGenerator` (abstract base): Async bot generation framework
- `PMCGenerator`: Creates PMC bots with proper profiles, gear, and spawn timing
- `PScavGenerator`: Creates player-scav bots with loot and behavior

**Registration** (`BotRegistrationManager`):
- Tracks bot types: Scav, PScav, PMC, Boss
- Manages spawn groups (`BotSpawnInfo`)
- Controls spawn rates and caps per map
- **Optimized**: Uses `HashSet<BotOwner>` for O(1) Contains() instead of `List<BotOwner>`

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
| **Spawns bots** | No | Yes (PMCs + PScavs, optional) |
| **Despawns bots** | No | Yes (via extraction system) |
| **Bot type tracking** | Agent entity only | Full type registry (Scav/PScav/PMC/Boss) |
| **StandBy disable** | Yes (prevents far deactivation) | No (uses sleeping system instead) |
| **Death handling** | Remove agent from systems | Track dead bots, update follower chains |
| **BSG mover** | Fully replaces | Coexists with |
| **Default spawning** | N/A | Disabled by default since v1.2.0 |

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

1. **ECS-Lite Data Layout** (v1.7.0, `BotLogic/ECS/`):
   - Dense `BotEntity` list with swap-remove (mirrors Phobos's `EntityArray<T>`)
   - All sensor, sleep, type, boss/follower, field state, and job assignment data on entities
   - Static system methods (`HiveMindSystem`, `QuestScorer`) iterate dense lists — no dictionary traversal
   - `BsgBotRegistry`-style sparse array for O(1) integer ID lookups without hash computation
   - `BotEntityBridge`: zero-allocation sensor iteration, static reusable buffers for `GetFollowers()`/`GetAllGroupMembers()`
   - `[AggressiveInlining]` on hot-path methods (pacing utilities, registry lookups)

2. **Sleeping System** (`src/.../BotLogic/Sleep/SleepingLayer.cs`):
   - Priority 99 (highest) - takes precedence over all other behavior
   - Disables bots far from human players (configurable per-map distances)
   - Minimum bot count threshold before sleeping activates
   - Exemptions for certain bot types and questing bots
   - Effectively reduces active bot count to those near the player

3. **Allocation Optimizations** (v1.1.0–v1.7.0):
   - Eliminated all dictionary-based sensor storage (5 `Dictionary<BotOwner, bool>` → embedded bools on `BotEntity`)
   - Eliminated `deadBots` list (`List<BotOwner>.Contains()` O(n) → `BotEntity.IsActive` O(1))
   - Static reusable buffers for `GetFollowers()`, `GetAllGroupMembers()` (zero allocation per call)
   - O(n) min/max scans replacing `Dictionary + OrderBy().First()` chains
   - `QuestScorer`: static buffers replacing 5 dictionary allocations + `OrderBy` in quest selection
   - `NumberOfActiveBots()`: dense entity iteration (was O(bots × assignments) dictionary scan)
   - `ConsecutiveFailedAssignments`: O(1) entity field read (was O(n) reverse list scan)
   - LINQ eliminated from all hot paths — for-loops throughout

4. **Pathfinding Throttle** (`Helpers/PathfindingThrottle.cs`):
   - Per-frame limiter: max 5 `NavMesh.CalculatePath` calls per frame
   - Prevents frame spikes when many bots recalculate simultaneously

5. **Batch Nav Jobs** (`Helpers/NavJobExecutor.cs`):
   - Queue-based batched pathfinding with ramped batch size
   - Same pattern as Phobos's NavJobExecutor

6. **Rate Limiting** (`Helpers/TimePacing.cs`, `FramePacing.cs`):
   - Reusable rate-limiter utilities with `[AggressiveInlining]`, inspired by Phobos
   - HiveMind tick: 50ms interval with deterministic 4-step sequence
   - Convergence field: 30s refresh interval
   - Delayed update base classes: configurable intervals (25ms–250ms)

7. **Delayed Update Base Classes**:
   - `CustomLayerDelayedUpdate`: configurable update intervals (25ms, 100ms, 250ms)
   - `MonoBehaviourDelayedUpdate`: similar throttling for MonoBehaviour components
   - `SleepingLayer` uses 250ms interval (only checks 4 times/second)
   - `BotObjectiveLayer` uses 25ms interval (40 times/second)

### Comparison

| Technique | Phobos | QuestingBots |
|-----------|--------|--------------|
| **Bot culling** | Disables StandBy (keeps all active) | Sleeping system disables far bots |
| **Update throttling** | Per-system update rates (TimePacing) | Per-layer/component intervals + TimePacing/FramePacing utilities |
| **Pathfinding** | Batched across frames | PathfindingThrottle (5/frame) + NavJobExecutor (batched) |
| **Data access** | Dense entity list (cache-friendly) | Dense entity list (ECS-Lite, same pattern as Phobos) |
| **Distance calcs** | Squared distances throughout | Mix of Distance() and sqrMagnitude |
| **Memory** | Pre-allocated arrays, pooling | Static buffers, zero-alloc sensor iteration, ring buffers |
| **LINQ** | Minimal | Eliminated from hot paths (replaced with for-loops) |
| **AggressiveInlining** | ~25+ methods | TimePacing/FramePacing, BsgBotRegistry lookup |
| **Zone field updates** | Every tick (hot-reloadable) | Timer-based (30s interval for convergence) |

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

**Zone Movement Overlap**:
- Both mods now have advection/convergence field systems
- If both are active, bots could receive conflicting movement directions
- QuestingBots' zone movement operates as a low-priority fallback quest, so it would only activate for bots not assigned to Phobos

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
| **C# version** | Modern (primary constructors, file-scoped namespaces) | Traditional (block-scoped namespaces) with `LangVersion latest` |
| **Null handling** | Pattern matching (`is not { IsAlive: true }`) | Null checks + early return |
| **Collections** | List initializers `[]`, tuples | `new List<T>()`, traditional patterns |
| **String formatting** | Interpolation throughout | Concatenation + some interpolation |
| **Access modifiers** | `internal` classes, `public` data | `internal` classes, mixed access |

### Code Organization

**Phobos**: Clean separation of concerns via ECS-like architecture. Each system is self-contained. Navigation, movement, location, and door handling are independent systems composed by PhobosManager. Actions and strategies are pluggable via `DefinitionRegistry<T>`.

**QuestingBots**: Feature-organized with clear domain boundaries. BotLogic split by concern (Objective, Follow, Sleep, HiveMind). Static controllers provide global access. Patches organized by category in subdirectories. Zone movement layer uses a pure-logic / integration split — `Core/`, `Fields/`, `Selection/` contain zero Unity/EFT dependencies and are fully unit-tested.

### Testing

| Aspect | Phobos | QuestingBots |
|--------|--------|--------------|
| **Unit tests** | None (Gym project is benchmarks) | NUnit + NSubstitute |
| **Test count** | 0 | 537 (479 client + 58 server) |
| **Client test coverage** | — | ECS data layout (BotEntity, BotRegistry, HiveMindSystem, QuestScorer, BotEntityBridge scenarios, BotFieldState, BsgBotRegistry, job assignments), stuck detection, zone movement, config deserialization |
| **CI** | None | GitHub Actions (format-check → lint → build → test) |
| **Benchmarks** | BenchmarkDotNet in Gym/ | None |
| **Formatting** | Not configured | CSharpier + `.editorconfig` |
| **Linting** | ReSharper annotations | Roslyn analyzers via `.editorconfig` |

### Error Handling

**Phobos**: Minimal error handling; trusts internal state. Uses `Log.Debug()` for diagnostic output. Can disable logging entirely for performance.

**QuestingBots**: Extensive logging via `LoggingController` with Info/Warning/Error levels. Defensive null checks throughout. Dead bot tracking prevents null reference on despawned bots. Zone movement initialization wrapped in try/catch with error logging.

---

## 14. Lessons & Recommendations

### What QuestingBots Has Adopted from Phobos

Since the original comparison, QuestingBots has ported several Phobos innovations:

1. **Two-tier stuck detection** (v1.1.0): Ported Phobos's `SoftStuckDetector` (asymmetric EWMA speed, vault→jump→fail at 1.5s/3s/6s) and `HardStuckDetector` (position history ring buffer, rolling average speed, teleport with proximity + LOS safety at 5s/10s/15s).

2. **Batched pathfinding** (v1.1.0): Added `PathfindingThrottle` (max 5 `NavMesh.CalculatePath` calls/frame) and `NavJobExecutor` (queue-based batched pathfinding with ramped batch size) — same patterns as Phobos.

3. **Zone-based movement fields** (v1.3.0-1.5.0): Built a full grid + vector-field system inspired by Phobos's advection/convergence architecture. Key difference: QuestingBots auto-detects map bounds and zones from spawn points, requiring no per-map JSON configuration. Per-bot field state (`BotFieldState`) tracks momentum and noise per bot — matching Phobos's pattern where `LocationSystem.RequestNear()` derives momentum from `(requestCoords - previousCoords)` — so each bot gets a unique direction even when occupying the same cell.

4. **Dynamic objective cycling** (v1.5.0): `ZoneObjectiveCycler` selects the next zone objective using live field state (advection + convergence + per-bot momentum + noise) instead of nearest-to-bot, matching Phobos's `GotoObjectiveStrategy.AssignNewObjective()` → `LocationSystem.RequestNear()` lifecycle. Integrated into `BotJobAssignmentFactory.GetNewBotJobAssignment()` with transparent fallback.

5. **Ring buffers and EWMA** (v1.1.0): `PositionHistory` and `RollingAverage` helpers ported from Phobos's stuck detection.

6. **ECS-Lite data layout** (v1.7.0): Full adoption of Phobos's `EntityArray<T>` pattern — dense `BotEntity` list with swap-remove, ID recycling via free stack, `BsgBotRegistry`-style sparse array for O(1) integer lookups. All sensor, sleep, type, boss/follower, field state, and job assignment data centralized on entities. Static system methods (`HiveMindSystem`, `QuestScorer`) iterate dense lists. `BotEntityBridge` provides the BSG interop layer. All old dictionaries (`deadBots`, `botBosses`, `botFollowers`, `sensors`, `botJobAssignments`) deleted — ECS is the sole data store.

7. **AggressiveInlining** (v1.7.0): `[MethodImpl(MethodImplOptions.AggressiveInlining)]` applied to `TimePacing`/`FramePacing` rate limiters and `BsgBotRegistry` sparse lookups, matching Phobos's use on hot-path methods.

8. **TimePacing / FramePacing** (v1.7.0): Reusable rate-limiter utilities ported from Phobos's `Helpers/Pacing.cs` pattern. Deterministic tick order in `BotHiveMindMonitor.Update()` mirrors Phobos's `PhobosManager.Update()` orchestration.

### What QuestingBots Could Still Learn from Phobos

1. **Custom movement system**: Phobos's path-deviation spring force and custom path following via `Player.Move()` produce smoother bot movement than BSG's native mover. QuestingBots still delegates to `BotOwner.FollowPath()`.

2. **Utility AI**: Phobos's score-based action selection with hysteresis is more extensible than QuestingBots' enum-based switch statement. New behaviors can be added without modifying existing dispatch code.

3. **BSG mover handoff**: Phobos's careful state restoration when returning control to BSG (`OnLayerChanged`) prevents the "bot standing still" bug that can occur when another layer takes over mid-path.

### What Phobos Could Learn from QuestingBots

1. **Quest-driven purpose**: Phobos bots move procedurally but lack purpose. QuestingBots' quest data gives bots meaningful objectives that align with the game's quest system, making them feel more realistic.

2. **Bot spawning**: QuestingBots' PMC/PScav generation system creates a dynamic population. Phobos only works with existing bots and cannot adjust spawn rates or types.

3. **Sleeping system**: QuestingBots' distance-based sleeping is highly effective for performance. Phobos disables BSG's StandBy but doesn't replace it, meaning all bots are always active.

4. **Broad bot-type support**: QuestingBots supports 20+ brain types including all bosses, followers, rogues, raiders, and cultists. Phobos supports 9 types.

5. **Auto-detection**: QuestingBots' zone movement auto-detects map bounds and zones from spawn points. Phobos requires manual `Geometry.json` + `Zones/{map}.json` files per map, which is fragile and requires updates when maps change.

6. **Server component**: Having quest data and bot generation on the server allows QuestingBots to leverage the full game database. Phobos is limited to client-side data.

7. **Testing infrastructure**: QuestingBots has 537 unit tests and CI. Phobos has none, relying on manual testing and benchmarks.

8. **Mod conflict detection**: QuestingBots actively detects and warns about conflicting mods both via BepInEx attributes and server-side checks. Phobos has no conflict detection.

9. **Diverse actions**: QuestingBots has 13 action types (ambush, snipe, plant item, unlock door, toggle switch, etc.) plus 5 zone action types that create varied bot behavior. Phobos has 2 actions (goto objective, guard).

10. **POI awareness**: QuestingBots' zone movement tracks 6 POI categories (Container, LooseLoot, Quest, Exfil, SpawnPoint, Synthetic) with weighted scoring. Phobos only considers zone-level attractors without individual POI granularity.

### Integration Possibilities

If both mods were to coexist:
- Phobos could provide the movement system while QuestingBots provides objectives
- QuestingBots' sleeping system could gate which bots Phobos processes
- Both zone field systems would need a coordination layer to avoid conflicting directions
- A shared "bot intent" interface could allow both mods to negotiate control of individual bots based on context (e.g., Phobos handles general movement, QuestingBots takes over for specific quest actions)
- QuestingBots' auto-detection approach could replace Phobos's per-map JSON configs
