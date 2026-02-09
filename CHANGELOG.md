# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.9.0] - 2026-02-09

### Added
- **Utility AI: Phobos-style scored task framework for bot action selection** — enabled by default via F12 (`Use Utility AI for Action Selection`, default: true)
  - `UtilityTask` abstract base: column-major scoring, additive hysteresis, swap-remove active entity tracking
  - `UtilityTaskManager`: Score→Pick→Execute pipeline with per-entity `ScoreAndPick()` convenience method
  - `UtilityTaskAssignment` readonly struct tracking current task + ordinal index
  - `QuestUtilityTask` abstract base: extends `UtilityTask` with `BotActionTypeId` mapping and no-op `Update()` (BigBrain handles execution)
  - `QuestActionId` / `BotActionTypeId`: int constants mirroring game enums for pure-logic scoring without game dependencies
  - 8 concrete quest utility tasks replacing the `BotObjectiveLayer.trySetNextAction()` switch statement:
    - `GoToObjectiveTask` (h=0.25): travel phase for MoveToPosition + two-phase actions (Ambush/Snipe/PlantItem when far)
    - `AmbushTask` (h=0.15): scores high when close to ambush position
    - `SnipeTask` (h=0.15): scores high when close to snipe position
    - `HoldPositionTask` (h=0.10): always active for HoldAtPosition quest actions
    - `PlantItemTask` (h=0.15): scores high when close to plant-item position
    - `UnlockDoorTask` (h=0.20): high-priority when door blocks current path
    - `ToggleSwitchTask` (h=0.10): active for ToggleSwitch quest actions
    - `CloseDoorsTask` (h=0.10): active for CloseNearbyDoors quest actions
  - `QuestTaskFactory`: static factory creating `UtilityTaskManager` with all 8 tasks
  - `BotEntityBridge.SyncQuestState()`: syncs 5 quest state fields from `BotObjectiveManager` to `BotEntity` before scoring
  - `BotObjectiveLayer.trySetNextActionUtility()`: utility AI path with config-gated branch; existing switch retained as fallback
  - 5 new quest state fields on `BotEntity`: `CurrentQuestAction`, `DistanceToObjective`, `IsCloseToObjective`, `MustUnlockDoor`, `HasActiveObjective`
  - F12 config entry `UseUtilityAI` (Main section, advanced, default: true)
  - ~79 new client tests: QuestActionId (1), BotActionTypeId (1), BotEntityQuestState (2), GoToObjectiveTask (16), AmbushTask (5), SnipeTask (5), HoldPositionTask (5), PlantItemTask (5), UnlockDoorTask (5), ToggleSwitchTask (4), CloseDoorsTask (4), QuestUtilityTaskBase (2), ScoreAndPick (3), QuestActionTransitions (18), QuestTaskFactory (4)
- **Door collision bypass (Phobos-style)** — enabled by default (`bypass_door_colliders`, default: true)
  - `ShrinkDoorNavMeshCarversPatch`: Harmony postfix on `GameWorld.OnGameStarted` — shrinks all door NavMesh carver sizes to 37.5% to prevent narrow hallways from being blocked by open doors on the navmesh
  - `DoorCollisionHelper`: static helper caching door colliders at map start, then disabling `Physics.IgnoreCollision` + `EFTPhysicsClass.IgnoreCollision` between each bot's colliders and all door colliders
  - Per-bot bypass applied in `CustomLayerForQuesting` constructor (once per bot lifetime)
  - Cleanup at raid end via `BotsControllerStopPatch`
  - Config: `bypass_door_colliders` in `BotPathingConfig` (default: true)
  - 10 new config deserialization tests for `BotPathingConfig`
- Updated `docs/utility-ai-analysis.md` — Phase 1 Core + Option A marked as implemented
- Updated `docs/phobos-comparison.md` — door bypass moved from "Still Learn" to "Has Adopted"

### Changed
- 721 client tests total (was 711), 58 server tests, 779 total

## [1.8.0] - 2026-02-08

### Added
- **Custom movement system (Phobos-style `Player.Move()` replacement)** — enabled by default (`use_custom_mover`, default: true)
  - `CustomPathFollower`: pure-logic path engine with corner-reaching epsilon (walk=0.35m, sprint=0.6m), path-deviation spring force, and sprint angle-jitter gating
  - `CustomMoverController`: Unity integration layer bridging `CustomPathFollower` to `Player.Move()` with ECS state sync
  - `CustomMoverConfig`: configurable thresholds (corner epsilon, spring strength, sprint jitter, Chaikin iterations)
  - `PathSmoother`: Chaikin corner-cutting subdivision for smoother NavMesh paths
  - `SprintAngleJitter`: XZ-plane angle-jitter calculator with urgency-based sprint thresholds (Low=20°, Medium=30°, High=45°)
  - `PathDeviationForce`: XZ-plane spring force pulling bots back toward ideal path line via dot-product projection
  - `CustomMoverHandoff`: BSG state sync helper — syncs 6 obfuscated `BotMover` fields on layer transitions
  - `MovementState` struct on `BotEntity`: tracks path status, sprint, stuck, corner progress, IsCustomMoverActive flag
  - 3 Harmony patches (conditionally registered when `use_custom_mover` is true):
    - `BotMoverFixedUpdatePatch`: prefix skip of `ManualFixedUpdate` when custom mover is active (per-bot ECS check)
    - `MovementContextIsAIPatch`: `IsAI` → false for human-like movement parameters
    - `EnableVaultPatch`: enables vaulting for AI bots via `InitVaultingComponent`
  - Wired into `GoToPositionAbstractAction` (lifecycle) and `GoToObjectiveAction` (per-frame tick)
  - `BotEntityBridge`: 4 movement state access methods (`IsCustomMoverActive`, `ActivateCustomMover`, `DeactivateCustomMover`, `GetEntityByProfileId`)
  - ~60 new tests: SprintAngleJitter (22), PathDeviationForce (15), CustomPathFollower (12), MovementState wiring (11)
- Updated `docs/custom-movement-analysis.md` — all 4 phases marked as implemented
- **ECS Phase 5A: dual-write gap closure** — all write operations now flow through ECS
  - `BotEntityBridge.DeactivateBot()` called on boss/follower death in `updateBosses()`/`updateBossFollowers()`
  - `BotEntityBridge.SetSleeping()` wired in `RegisterSleepingBot()`/`UnregisterSleepingBot()`
  - `BotHiveMindMonitor.Clear()` calls `BotEntityBridge.Clear()`
- **ECS Phase 5B: push sensor ECS-only writes** — old dictionary writes removed from `UpdateValueForBot()`
  - Push sensors (InCombat, IsSuspicious, WantsToLoot) write only to ECS entities via `BotEntityBridge.UpdateSensor()`
  - `BotEntityBridge.UpdateLastLootingTime()` called directly from `UpdateValueForBot()` when WantsToLoot is set
- **ECS Phase 5C: pull sensor dense iteration** — eliminates delegate allocation per 50ms tick
  - New `updatePullSensors()` iterates `BotEntityBridge.Registry.Entities` directly with a for-loop
  - Zero `Action<BotOwner>` allocation per tick (previously allocated a delegate per call)
  - Writes CanQuest and CanSprintToObjective directly to entity fields
- **ECS Phase 5D: boss/follower lifecycle on ECS** — O(1) dead checks replace O(n) list scan
  - `updateBosses()` iterates dense ECS entity list instead of `botBosses.Keys` dictionary
  - `entity.IsActive` replaces `deadBots.Contains()` — O(1) instead of O(n) per check
  - `updateBossFollowers()` calls `HiveMindSystem.CleanupDeadEntities()` for ECS-side boss/follower reference cleanup
  - Old dictionary writes retained alongside ECS writes for Phase 5F removal
- **ECS Phase 5E: BotRegistrationManager reads via ECS** — O(1) ProfileId mapping
  - `BotEntityBridge.IsBotSleeping(string profileId)` — O(1) via `_profileIdToEntity` dictionary (replaces `sleepingBotIds.Contains()` O(n) list scan)
  - `BotEntityBridge.IsBotAPMC(BotOwner)` — reads `entity.BotType == BotType.PMC`
  - `BotEntityBridge.GetBotType(BotOwner)` — reads `entity.BotType` with `MapBotTypeReverse()`
  - `_profileIdToEntity` dictionary for O(1) string ProfileId→BotEntity lookup, populated in `RegisterBot()`
- **ECS Phase 7A: BsgBotRegistry sparse array** — O(1) integer ID lookup without hash computation
  - `BotRegistry.Add(int bsgId)`, `GetByBsgId(int)`, `ClearBsgId(int)` with `[AggressiveInlining]`
  - `BotEntityBridge.GetEntityByBsgId(int bsgId)` for O(1) lookup by `BotOwner.Id`
  - Wired into `BotEntityBridge.RegisterBot()` — entities registered with BSG ID at spawn
- **ECS Phase 7B: TimePacing / FramePacing utilities** — reusable rate limiters inspired by Phobos
  - `TimePacing` (`Helpers/TimePacing.cs`): time-based rate limiter with `ShouldRun(float)` + `Reset()`, `[AggressiveInlining]`
  - `FramePacing` (`Helpers/FramePacing.cs`): frame-based rate limiter with `ShouldRun(int)` + `Reset()`, `[AggressiveInlining]`
  - Pure C#, zero Unity dependencies
- **ECS Phase 5F: remove old data structures** — ECS is now the sole data store
  - Deleted 6 sensor files: `BotHiveMindAbstractSensor`, `BotHiveMindIsInCombatSensor`, `BotHiveMindIsSuspiciousSensor`, `BotHiveMindWantsToLootSensor`, `BotHiveMindCanQuestSensor`, `BotHiveMindCanSprintToObjectiveSensor` (~310 lines total)
  - Deleted `deadBots`, `botBosses`, `botFollowers`, `sensors` dictionaries from `BotHiveMindMonitor`
  - Deleted `_deadBossBuffer`, `RegisterBot()`, `GetBoss()`, `throwIfSensorNotRegistred()` from `BotHiveMindMonitor`
  - Deleted `sleepingBotIds` list, `IsBotSleeping()`, `PMCs`/`Bosses` properties, `IsARegisteredPMC`/`IsARegisteredBoss` extensions from `BotRegistrationManager`
  - `RegisterSleepingBot()`/`UnregisterSleepingBot()` now use `BotEntityBridge.IsBotSleeping()` as guard
  - `addBossFollower()` checks ECS `followerEntity.Boss == bossEntity` instead of `botFollowers[boss].Contains(bot)`
  - `updateBossFollowers()` reduced to single `HiveMindSystem.CleanupDeadEntities()` call
  - Removed `BotHiveMindMonitor.RegisterBot()` call from `BotOwnerBrainActivatePatch`
  - `BotInfoGizmo` switched to `BotEntityBridge.IsBotSleeping()` / `.GetBoss()`
- **ECS Phase 6: BotFieldState wiring** — zone movement per-bot state on ECS entities
  - `BotEntityBridge` stores `Dictionary<int, BotFieldState>` keyed by entity ID
  - `RegisterBot()` creates `BotFieldState(profileId.GetHashCode())`, sets `entity.FieldNoiseSeed` and `entity.HasFieldState`
  - `GetFieldState(BotOwner)` and `GetFieldState(string profileId)` for O(1) lookups
  - `WorldGridManager.botFieldStates` dictionary removed
  - `WorldGridManager.GetOrCreateBotState()` method removed
  - `WorldGridManager.GetRecommendedDestination()` now uses `BotEntityBridge.GetFieldState()`
- **ECS Phase 7C: deterministic tick order** — documents the fixed system call sequence in `BotHiveMindMonitor.Update()`
  - 4-step tick: `updateBosses()` → `updateBossFollowers()` → `updatePullSensors()` → `ResetInactiveEntitySensors()`
  - Push sensors (InCombat, IsSuspicious, WantsToLoot) are event-driven and do not participate in the tick
  - XML doc comment + inline step numbers for code clarity
- **ECS Phase 7D: allocation cleanup** — eliminates 5 remaining allocation hotspots
  - `GetFollowers()`: static `_followersBuffer` reused per call, returns `IReadOnlyList<BotOwner>` (zero allocation)
  - `GetFollowerCount()`: new O(1) method for count-only callers (2 call sites in `BotJobAssignmentFactory`)
  - `GetAllGroupMembers()`: static `_groupMembersBuffer` reused per call, returns `IReadOnlyList<BotOwner>` (zero allocation)
  - `GetLocationOfNearestGroupMember()`: inlined to iterate boss+followers directly (no intermediate collection)
  - `NearestToBot()`: `Dictionary` + `.OrderBy().First()` → O(n) min-scan with local variables
  - `SetExfiliationPointForQuesting()`: `.ToDictionary()` + `.OrderBy().Last()` → O(n) max-scan for-loop
  - `TryArchiveRepeatableAssignments()`: `.Where().Where().ToArray()` → single for-loop with in-place `Archive()`
- **ECS Phase 8: job assignment wiring** — migrates `botJobAssignments` dictionary to ECS
  - `BotEntityBridge._jobAssignments`: `Dictionary<int, List<BotJobAssignment>>` keyed by entity ID (replaces `BotJobAssignmentFactory.botJobAssignments` string-keyed dictionary)
  - 10 new bridge methods: `GetJobAssignments(BotOwner/string)`, `EnsureJobAssignments`, `HasJobAssignments`, `GetConsecutiveFailedAssignments`, `IncrementConsecutiveFailedAssignments`, `ResetConsecutiveFailedAssignments`, `RecomputeConsecutiveFailedAssignments`, `AllJobAssignments`
  - `NumberOfActiveBots()` iterates dense `Registry.Entities` instead of dictionary keys
  - `ConsecutiveFailedAssignments` O(1) entity field read (was O(n) reverse list scan)
  - `BotObjectiveManager.FailObjective()` calls `IncrementConsecutiveFailedAssignments`
  - `BotObjectiveManager.CompleteObjective()` calls `ResetConsecutiveFailedAssignments`
  - ~28 dictionary access points migrated in `BotJobAssignmentFactory`
  - `.Last()` → `[Count-1]`, `.TakeLast(2).First()` → `[Count-2]` (zero-alloc)
  - 3 unsafe dictionary accesses fixed (now safely returns empty list)
- 135 new client tests: BotEntityBridge scenarios (+81 incl. 16 Phase 8, 8 Phase 7D), BsgBotRegistry (15), BotFieldState (12), JobAssignment (8), TimePacing (9), FramePacing (10)
- Updated `docs/ecs-data-layout-analysis.md` — ECS-Lite Option B 100% complete

### Changed
- ECS is now the sole data store — all old dictionaries and sensor classes deleted; push/pull sensors, sleep, type, boss/follower, field state, job assignments all managed via ECS only
- `botJobAssignments` dictionary removed from `BotJobAssignmentFactory`
- 595 client tests total (was 344), 58 server tests, 653 total

## [1.7.0] - 2026-02-08

### Added
- **ECS Phase 1: BotEntity + BotRegistry foundation** — dense entity storage inspired by Phobos EntityArray pattern
  - `BotEntity`: pure C# data container with stable recycled ID, IsActive flag, Boss/Followers hierarchy, IEquatable support
  - `BotRegistry`: dense `List<BotEntity>` storage with swap-remove, `List<int?>` sparse ID→index map, `Stack<int>` ID recycling
  - Zero Unity/EFT dependencies — fully testable in net9.0
  - 48 new unit tests: `BotEntityTests` (15) + `BotRegistryTests` (33)
- **ECS Phase 2: Sensor state + embedded classification** — moves HiveMind sensor data onto BotEntity
  - 5 sensor booleans embedded on BotEntity: `IsInCombat`, `IsSuspicious`, `CanQuest`, `CanSprintToObjective`, `WantsToLoot` (replaces 5 `Dictionary<BotOwner, bool>`)
  - `LastLootingTime` (replaces `botLastLootingTime` dictionary)
  - `BotType` enum (Unknown, PMC, Scav, PScav, Boss) — replaces `registeredPMCs`/`registeredBosses` HashSet lookups
  - `IsSleeping` flag — replaces `sleepingBotIds` list lookup
  - `BotSensor` enum with `GetSensor`/`SetSensor` for generic access
  - Zero-allocation group query helpers: `CheckSensorForBoss`, `CheckSensorForAnyFollower`, `CheckSensorForGroup`
  - 26 new unit tests for all Phase 2 fields, methods, and group query scenarios
- **ECS Phase 3: System extraction** — static system methods operating on dense entity lists (Phobos pattern)
  - `HiveMindSystem`: 7 static methods for HiveMind logic — `ResetInactiveEntitySensors`, `CleanupDeadEntities`, `AssignBoss`, `RemoveBoss`, `SeparateFromGroup`, `CountActive`, `CountActiveByType`
  - Replaces O(n²) `NumberOfActiveBots()` with O(n) dense iteration via `CountActive`/`CountActiveByType`
  - Bidirectional boss/follower lifecycle management (assign, remove, separate, dead-entity cleanup)
  - 30 new unit tests covering all system methods, edge cases (null args, self-assign, chain hierarchies)
- **ECS Phase 4: Job assignment optimization** — reduces LINQ allocations in quest selection hot path
  - `QuestScorer`: pure-logic scoring system with `QuestScoringConfig` struct, `ScoreQuest()`, and `SelectHighestIndex()`
  - `GetRandomQuest()` rewritten: 5 dictionary allocations + `ToDictionary` + `OrderBy` → static buffers + `QuestScorer` O(n) scan
  - `FindQuest()`: double LINQ iteration (`.Where().Count()` + `.First()`) → single-pass for-loop
  - `NumberOfConsecutiveFailedAssignments()`: `.Reverse().TakeWhile().Count()` → reverse for-loop
  - `FindQuestsWithZone()`: `.Where().ToArray()` → static list buffer (zero allocation)
  - 24 new unit tests for `QuestScorer` (scoring math, distance/desirability/angle factors, `SelectHighestIndex`)
- **ECS Wiring: BotEntityBridge** — dual-write integration layer connecting game events to ECS data
  - `BotEntityBridge`: thin static adapter bridging `BotOwner` (game type) to `BotEntity` (ECS data) with `Dictionary<BotOwner, BotEntity>` + `BotRegistry`
  - Dual-write pattern: every game event writes to both old dictionary-based HiveMind AND new dense ECS entities (reads still from old system for safe transition)
  - 7 hook points across 4 files: spawn registration (`BotOwnerBrainActivatePatch`), raid cleanup (`BotsControllerStopPatch`), sleep/wake (`SleepingAction`), sensor sync + boss/follower + group separation (`BotHiveMindMonitor`)
  - Enum mapping helpers: `MapBotType` (Controllers.BotType → ECS.BotType), `MapSensorType` (BotHiveMindSensorType → BotSensor)
  - 15 new integration-style scenario tests covering full bot lifecycle (register → sensor → boss/follower → sleep → separate → clear)
- **ECS Migration: reads switched from dictionaries to dense ECS entities** — completes the dual-write → single-read transition
  - Added missing dual-writes for 3 self-updating sensors: `CanQuest`, `CanSprintToObjective` (pull sensors that bypass `UpdateValueForBot`), and `LastLootingTime` (DateTime tracked in private dictionary)
  - Added entity→BotOwner reverse lookup (`Dictionary<int, BotOwner>`) in `BotEntityBridge` for boss/follower return methods
  - Added 13 read convenience methods on `BotEntityBridge`: `IsRegistered`, `GetSensorForBot`, `GetSensorForBossOfBot`, `GetSensorForGroup`, `HasBoss`, `GetBoss`, `GetFollowers`, `GetAllGroupMembers`, `GetDistanceToBoss`, `GetActiveBrainLayerOfBoss`, `GetLocationOfBoss`, `GetLocationOfNearestGroupMember`, `HasFollowers`, `GetLastLootingTimeForBoss`, `GetBotOwner`
  - Switched ~28 read call sites across 12 files from `BotHiveMindMonitor` to `BotEntityBridge`
  - Removed 14 dead read methods from `BotHiveMindMonitor` (`GetValueForBot`, `GetValueForBossOfBot`, `GetValueForFollowers`, `GetValueForGroup`, `GetLastLootingTimeForBoss`, `IsRegistered`, `HasBoss`, `HasFollowers`, `GetFollowers`, `GetAllGroupMembers`, `GetActiveBrainLayerOfBoss`, `GetDistanceToBoss`, `GetLocationOfBoss`, `GetLocationOfNearestGroupMember`) and `GetLastLootingTimeForBoss` from `BotHiveMindWantsToLootSensor`
  - `BotHiveMindMonitor` retains only: writes (`UpdateValueForBot`, `SeparateBotFromGroup`), lifecycle (`RegisterBot`, `Clear`), internal (`GetBoss` for `AbstractSensor`), and `Update` tick
  - 14 new tests covering ECS read paths (group sensors, boss/follower queries, LastLootingTime, HasBoss/HasFollowers lifecycle)
- ECS data layout analysis document (`docs/ecs-data-layout-analysis.md`) — Phobos architecture deep dive, QuestingBots audit, cache coherency math, and phased implementation plan

### Changed
- 344 client tests total (was 187), 58 server tests, 402 total

## [1.6.0] - 2026-02-08

### Added
- **Zone movement Phase 4A: per-bot field state** — eliminates herd movement where all bots in the same cell choose the same direction
  - `BotFieldState`: per-bot momentum (from tracked `PreviousDestination`) and seeded noise angle
  - `ZoneMathUtils.ComputeMomentum()`: normalized XZ-plane direction for per-bot momentum derivation
  - `WorldGridManager.GetRecommendedDestination(string, Vector3)`: new per-bot overload using auto-tracked momentum + noise via `BotFieldState`
  - 9 new unit tests: `BotFieldStateTests` (4) + `ZoneMathUtilsTests.ComputeMomentum` (5)
- **Zone movement Phase 4B: dynamic objective selection** — bots select next zone objective using live field state instead of nearest-to-bot
  - `ZoneObjectiveCycler`: selects zone objectives via advection + convergence + per-bot momentum + noise, matching Phobos's `GotoObjectiveStrategy.AssignNewObjective()` pattern
  - Integrated into `BotJobAssignmentFactory.GetNewBotJobAssignment()` with transparent fallback to default selection
  - 5 new contract tests: `ZoneObjectiveCyclerTests` (naming convention validation)
- **Zone movement Phase 4C: enhanced 2D debug minimap** — real-time visualization of zone movement system state
  - `DebugDrawing`: static OnGUI drawing primitives (filled rects, lines, arrows, dots) using RotateAroundPivot pattern
  - `MinimapProjection`: pure-logic coordinate mapping (world XZ → screen pixels) and POI category → color mapping
  - `ZoneDebugOverlay.RenderMinimap()`: 400px 2D minimap with cell coloring, advection/convergence arrows, bot/player/zone dots, and legend
  - `WorldGridManager`: exposed Advection, Convergence, CachedPlayerPositions, CachedBotPositions, ZoneSources for debug visualization
  - F12 menu "Debug Minimap" toggle (`ZoneMovementDebugMinimap`)
  - 15 new unit tests: `MinimapProjectionTests` (coordinate mapping + color mapping)

### Changed
- Zone quests now use field-based objective cycling for dynamic, non-repetitive bot movement patterns
- 187 client tests total (was 158), 58 server tests, 245 total

## [1.5.0] - 2026-02-08

### Added
- **Zone movement Phase 3: dynamic fields, debug overlay, F12 config**
  - `WorldGridManager.Update()`: periodic convergence field refresh with live player/bot positions
  - `WorldGridManager.GetRecommendedDestination()`: API for computing dynamic bot destinations using live field state
  - `ZoneDebugOverlay`: OnGUI overlay showing grid stats, POI counts, and player cell info (gated behind F12 toggle)
  - F12 menu "Zone Movement" section with Enable toggle and Debug Overlay toggle
- F12 config entry `ZoneMovementEnabled` overrides JSON config for runtime toggling
- F12 config entry `ZoneMovementDebugOverlay` for in-game debug info

### Changed
- Zone movement initialization now respects both JSON config (`zone_movement.enabled`) and F12 menu toggle
- WorldGridManager caches player/bot positions for use by convergence field and destination API

## [1.4.1] - 2026-02-08

### Removed
- Deleted legacy TypeScript source files (`src/mod.ts`, `src/BotLocationUtil.ts`, `src/CommonUtils.ts`, `src/PMCConversionUtil.ts`)
- Deleted `types/` directory (742 TypeScript declaration files from SPT 3.x)
- Deleted `dist/` directory (old release build output)
- Deleted `bepinex_dev/` directory (old SPT 3.x C# client source)
- Deleted Node.js artifacts (`package.json`, `package-lock.json`, `tsconfig.json`, `packageBuild.ts`)
- Deleted ESLint config (`.eslintignore`, `.eslintrc.json`)
- Deleted VS Code workspace file (`mod.code-workspace`)
- Removed obsolete `.gitignore` entries for `node_modules/` and `bepinex_dev/Libraries/`

### Fixed
- Suppressed ConfigServer obsolete warnings (`CS0618`) with `#pragma` — SPT 4.2 migration not yet possible (config types not in DI container)
- Eliminated all 33 compiler warnings: `CS0618` (21), `CS1998` (7), `CS9113` (2), `CS8604` (4), `CS8632` (4)

## [1.4.0] - 2026-02-08

### Added
- **Zone movement Phase 2: scene integration + action model** — bridges Phase 1 pure logic to the game world
  - `MapBoundsDetector`: auto-detects map bounds from spawn point positions with configurable padding
  - `PoiScanner`: scans Unity scene for containers, quest triggers, and exfil points as POIs (NavMesh-validated)
  - `ZoneDiscovery`: discovers bot zones by grouping spawn points by `BotZoneName`, computes centroids as advection sources
  - `WorldGridManager` (MonoBehaviour): orchestrator that creates grid, populates POIs/zones, builds field components on `Awake()`
  - `ZoneQuestBuilder`: creates fallback `Quest` with one objective per navigable grid cell, registered via `BotJobAssignmentFactory.AddQuest()`
  - `ZoneActionSelector`: maps POI categories to varied bot actions (Ambush, Snipe, HoldAtPosition, PlantItem, MoveToPosition) with weighted random selection inspired by Phobos
- `ZoneMovementConfig`: 12-property configuration model under `questing.zone_movement` with sensible defaults
- Zone movement wired into `LocationData.Awake()` (before `BotQuestBuilder`) and `BotQuestBuilder.LoadAllQuests()` (after spawn point wander)
- 22 new unit tests for `ZoneActionSelector` (13 tests) and `MapBoundsDetector` (9 tests) — 143 client tests total

### Changed
- Zone movement quests use low desirability (5) as fallback when no higher-priority quests are available
- Bot actions at zone destinations vary by dominant POI category: containers → 60% Ambush, exfils → 60% Snipe, spawn points → 90% MoveToPosition

## [1.3.0] - 2026-02-08

### Added
- **Zone-based movement system** (Phase 1: pure logic layer) — grid + vector-field architecture inspired by Phobos
  - `WorldGrid`: auto-sized 2D grid partitioning maps on the XZ plane (~150 cells per map, no per-map config)
  - `GridCell`: spatial partition with POI tracking and navigability checks
  - `PointOfInterest` + `PoiCategory`: typed POI model with category-based default weights
  - `AdvectionField`: pushes bots toward geographic zones with crowd repulsion (inverse-square falloff)
  - `ConvergenceField`: pulls bots toward human players with sqrt-distance falloff, cached with 30s update interval
  - `FieldComposer`: combines advection, convergence, momentum, and noise into composite movement direction
  - `CellScorer`: scores candidate cells by directional alignment + POI density blend
  - `DestinationSelector`: picks best navigable neighbor cell for bot movement
- 71 new unit tests for all zone movement classes (121 client tests total)
- Comprehensive design document (`docs/zone-movement-design.md`)
- Full XML documentation on all zone movement classes

## [1.2.0] - 2026-02-08

### Changed
- Bot spawning is now **disabled by default** (`bot_spawns.enabled` defaults to `false`)
  - The mod's core value is bot movement/questing — bots spawned by the base game or other mods automatically get questing behavior
  - Custom bot spawning (PMCs, PScavs) is still available as an opt-in feature by setting `bot_spawns.enabled: true` in config.json
- Guarded game start delay behind `bot_spawns.enabled` to prevent referencing spawning state when spawning is disabled

### Added
- 3 new config deserialization tests for spawning-optional behavior (58 server tests total)

## [1.1.0] - 2026-02-08

### Performance
- Eliminated `.Keys.ToArray()` allocations in HiveMind hot loops (`BotHiveMindAbstractSensor`, `BotHiveMindMonitor`) — 7+ array allocations removed per 50ms tick
- Replaced `Dictionary + OrderBy().First()` with simple min-tracking loop in `GetLocationOfNearestGroupMember()`
- Replaced LINQ chain in `GetAllGroupMembers()` with explicit for-loop
- Replaced `List<BotOwner>.Contains()` (O(n)) with `HashSet<BotOwner>` (O(1)) in `BotRegistrationManager`
- Replaced nested `.Where().Where().Where().Count()` LINQ in `BotJobAssignmentFactory.NumberOfActiveBots()` with counting for-loop
- Replaced `.Where()` x5 chain in `BotJobAssignmentFactory.GetAllPossibleQuests()` with reusable static buffer + for-loop
- Restructured `updateBossFollowers()` to use deferred removal pattern instead of `.ToArray()` for safe dictionary mutation
- Added per-frame `PathfindingThrottle` (max 5 `NavMesh.CalculatePath` calls/frame) to prevent frame spikes with many bots
- Added `NavJobExecutor` for queue-based batched pathfinding with ramped batch size

### Changed
- Replaced single-tier stuck detection with two-tier system (soft + hard) ported from Phobos
  - **Soft stuck** (`SoftStuckDetector`): EWMA speed tracking, ignores Y-axis; vault at 1.5s, jump at 3s, fail at 6s
  - **Hard stuck** (`HardStuckDetector`): ring buffer position history + rolling average speed; path retry at 5s, teleport at 10s, fail at 15s
- Safe teleportation for hard-stuck bots: proximity check (< 10m) + line-of-sight check against human players
- Stuck detection now attempts vault before jump (vault at 1.5s, jump at 3s) — vault is less disruptive
- Reduced stuck detection thresholds: vault 8s→1.5s, jump 6s→3s, debounce 4s→2s

### Added
- `PositionHistory` ring buffer for tracking bot positions over N samples (ported from Phobos)
- `RollingAverage` circular buffer for speed averaging with periodic drift correction (ported from Phobos)
- `SoftStuckDetector` — frame-to-frame stuck detection with asymmetric EWMA speed tracking
- `HardStuckDetector` — long-term stuck detection using position history and rolling average speed
- `PathfindingThrottle` — per-frame limiter for `NavMesh.CalculatePath` calls
- `NavJob` + `NavJobExecutor` — queue-based async pathfinding infrastructure
- Comprehensive unit tests for `PositionHistory`, `RollingAverage`, `SoftStuckDetector`, `HardStuckDetector` (50 client tests)
- Detailed Phobos vs QuestingBots technical comparison (`docs/phobos-comparison.md`)
- Phobos lessons implementation plan with 3-phase roadmap (`docs/phobos-lessons-implementation-plan.md`)

## [1.0.0] - 2026-02-08

Initial C# port of SPTQuestingBots, based on the original TypeScript mod v0.10.3 by DanW.

### Added
- Full C# port of the server-side TypeScript mod (`mod.ts`, `CommonUtils.ts`, `BotLocationUtil.ts`, `PMCConversionUtil.ts`)
- C# solution with separate Server (net9.0) and Client (netstandard2.1) projects
- SDK-style `.csproj` files with shared properties via `Directory.Build.props`
- Makefile build system with targets: `build`, `test`, `format`, `lint`, `ci`, `copy-libs`, `check-libs`, `clean`
- `copy-libs` / `check-libs` Makefile targets for automated DLL management from SPT installations
- CSharpier code formatting integration
- `.editorconfig` with comprehensive C# code style rules
- NUnit 3.x + NSubstitute unit test infrastructure (55 server tests, client smoke tests)
- GitHub Actions CI workflow (format check, lint, tests)
- Developer documentation (`docs/architecture.md`, `docs/development.md`, `docs/migration.md`)
- Comprehensive README.md with architecture overview, setup instructions, and project structure
- Spawning mod detection logic (detects SWAG, MOAR, BetterSpawnsPlus, RealPlayerSpawn, BotPlacementSystem)
- `generateBots` interception for PScav conversion via `QuestingBotGenerateRouter` (overrides `BotStaticRouter` with `[Injectable(typeOverride)]`)
- Comprehensive XML documentation on all server-side C# files

### Fixed
- JSON serialization mismatch in `/QuestingBots/GetConfig` route (was using `System.Text.Json` instead of Newtonsoft, producing PascalCase keys instead of the expected snake_case)
- GClass obfuscated name changes for SPT 4.x (`GClass385`→`GClass395`, `GClass522`→`BotActionNodesClass`, `GClass168`→`BotNodeAbstractClass`, `GClass529`→`BotCurrentPathAbstractClass`, `GClass677`→`GClass699`, `GClass663`→`BotProfileDataClass`, `GClass3424`→`KeyInteractionResultClass`, `GClass3901`→`GClass1661`)
- Private fields now public properties in SPT 4.x (`BotMover`, `BotDoorOpener`, `BotExfiltrationData`, `BotsGroup`, `BotsController`, `BotSpawner`)
- SPT client API change: `AiHelpers` → `AIExtensions`
- `BotCreationDataClass.Create` updated for `GInterface22` parameter
- Server csproj `InternalsVisibleTo` moved to correct MSBuild element
- Test project DLL references fixed for runtime resolution

### Changed
- Server plugin ported from TypeScript/Node.js to C# for SPT 4.x compatibility
- Converted tsyringe DI patterns to .NET DI with `[Injectable]` attributes
- Converted StaticRouterModService/DynamicRouterModService to C# StaticRouter/DynamicRouter pattern
- Updated mod lifecycle from `IPreSptLoadMod`/`IPostDBLoadMod`/`IPostSptLoadMod` to `IPreSptLoadModAsync`/`IOnLoad`
- Reorganized client BepInEx plugin source into structured namespace hierarchy
- Updated all Harmony patches for SPT 4.x API changes
- Both server and client compile cleanly against SPT 4.x assemblies (0 errors, 0 warnings)

### Migration Notes
- Based on SPTQuestingBots v0.10.3 (SPT 3.x, `sptVersion: >=3.11.2 <3.12.0`)
- See [docs/migration.md](docs/migration.md) for detailed SPT 3.x to 4.x migration information
