# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.10.0] - 2026-02-10

### Added
- **Cover point system (3 phases)** — NavMesh position validation + sunflower spiral fallback + BSG voxel integration
  - `NavMeshPositionValidator`: TrySnap, IsReachable, HasLineOfSight via NavMesh.Raycast
  - `SunflowerSpiral`: golden-angle Vogel's formula from Phobos for radial candidate generation
  - `BsgCoverPointCollector`: wraps BSG `CoversData.GetVoxelesExtended()` for voxel-based cover queries
  - `CoverPositionSource` delegate pattern for swappable cover point providers
  - Reachability validation via `NavMesh.CalculatePath` + LOS verification via `NavMesh.Raycast`
- **Formation movement** — coordinated en-route squad movement with speed matching
  - `FormationSpeedController`: Sprint/Walk/MatchBoss/SlowApproach speed modes
  - `FormationPositionUpdater`: Column (trail) and Spread (fan) formation computation
  - `FormationSelector`: Column/Spread selection with `PathWidthProbe` delegate for NavMesh path-width detection
- **Squad voice commands** — contextual boss callouts, follower responses, and combat warnings
  - `SquadVoiceHelper`: 14 callout types (`SquadCalloutId`) mapped to BSG `EPhraseTrigger` voice lines
  - Contextual triggers: objective arrival, follower acknowledgment, combat warnings, loot notifications
- **Combat-aware tactical positioning** — threat-oriented squad repositioning (pioneering — neither Phobos nor SAIN implement this)
  - `CombatPositionAdjuster`: `ReassignRolesForCombat()` + `ComputeCombatPositions()` for dynamic role rotation
  - Version-gated re-evaluation when `CombatVersion` changes on squad entity
- **Zone movement integration** — follower spread across neighboring grid cells
  - `ZoneFollowerPositionCalculator`: round-robin cell assignment + golden-angle jitter for spatial diversity
- **Multi-level objective sharing** — two-tier communication chain with position degradation
  - Boss→follower objective broadcasting with per-tier accuracy degradation
  - Comm range gating (earpiece/voice) determines sharing quality
- **Bot LOD system** — Phobos-style all-active with distance-based update throttling
  - `BotLodCalculator`: 3-tier LOD (Full/Reduced/Minimal) with configurable distance thresholds and frame skip counts
  - `HumanPlayerCache`: static SoA cache with zero-allocation per-bot distance queries (once-per-tick snapshot)
  - BSG StandBy system fully disabled (`CanDoStandBy = false` + `Activate()`)
  - Config: `questing.bot_lod` with distance thresholds (150m/300m) and skip counts (2/4)
  - Sleeping retained as opt-in fallback (disabled by default)
- **Hybrid looting system** — utility AI scoring + dedicated controller with squad coordination
  - `LootTask`: `QuestUtilityTask` subclass, `BotActionTypeId=Loot(13)`, MaxBaseScore=0.55, hysteresis=0.15
  - `LootAction`: BigBrain action with state machine (Approach→Interact→Complete/Failed)
  - `LootScorer`: pure-logic scoring (value/distance/proximity/gear/cooldown), `ItemValueEstimator` with `PriceLookup` delegate
  - `LootInventoryPlanner`: gear upgrade planning (value+space), `GearComparer` (armor/weapon/container/rig)
  - `SquadLootCoordinator`: boss priority claims, follower loot windows, shared scan results
  - `LootClaimRegistry`: multi-bot loot deconfliction with bidirectional Dictionary mapping
  - Unity helpers: `LootScanHelper` (Physics.OverlapSphereNonAlloc), `LootInteractionHelper`, `ContainerInteractionHelper`, `GearSwapHelper`, `CorpseSearchHelper`
  - `InventorySpaceHelper`: approximate free grid slots (backpack + tactical vest)
  - Server: `DiscardLimitsService` (disables BSG discard limits)
  - LootingBots compat: auto-detect via `ExternalModHandler.IsNativeLootingEnabled()`
  - Config: `LootingConfig` with 22 JSON properties under `questing.looting`
  - 10 loot fields on `BotEntity` + shared loot arrays on `SquadEntity`
  - ~198 new tests (39 scoring + 26 inventory + 8 config + 52 coordination + ~73 other)
- **Vulture system (ported from Vulture mod)** — combat event detection, multi-phase ambush behavior, squad coordination
  - `CombatEventRegistry`: global ring buffer (128 slots) for zero-allocation combat event storage with `AggressiveInlining`
    - Spatial queries: `GetNearestEvent`, `GetIntensity`, `IsInBossZone`, `CleanupExpired`
    - `CombatEvent` struct with `CombatEventType` byte constants (Gunshot=1, Explosion=2, Airdrop=3)
  - `CombatEventScanner`: pure-logic system querying `CombatEventRegistry` per `BotEntity` per tick
    - Writes: `HasNearbyEvent`, `NearbyEventX/Y/Z`, `NearbyEventTime`, `CombatIntensity`, `IsInBossZone`
  - `VultureTask`: `QuestUtilityTask` subclass, `BotActionTypeId=Vulture(14)`, MaxBaseScore=0.60
    - Gates: `HasNearbyEvent`, `!IsInCombat`, `!IsInBossZone`, `!cooldown`, `intensity>=CourageThreshold`
    - Scoring: intensity component (IntensityWeight=0.30) + proximity component (ProximityWeight=0.30)
  - `VultureAction`: `GoToPositionAbstractAction` with 6-phase state machine
    - Approach (sprint) → SilentApproach (walk, flashlight off, pose=0.6) → HoldAmbush (paranoia lookback) → Rush (sprint) / Paranoia (random scan) → Complete
    - Config-driven: 12 tuning parameters from `VultureConfig`
  - `VultureSquadStrategy`: `SquadStrategy` subclass, BaseScore=0.75
    - Activates when leader enters vulture phases; fan-out positions perpendicular to approach direction
    - Roles: outer followers = Flanker, middle = Guard
  - Event sources: 3 Harmony patches/subscribers
    - `OnMakingShotPatch`: `Player.OnMakingShot` postfix (filters silenced weapons, tags boss shots)
    - `GrenadeExplosionSubscriber`: `BotEventHandler.OnGrenadeExplosive` event (filters smoke grenades)
    - `AirdropLandPatch`: extended to record `CombatEventType.Airdrop` (power=200)
  - `TimeOfDayHelper`: `GameWorld.GameDateTime` detection range modifier (night=0.65×, dawn/dusk lerp, day=1.0×)
  - `VultureConfig`: 32 JSON properties under `questing.vulture` in config.json
  - 9 vulture fields on `BotEntity`: `HasNearbyEvent`, `NearbyEventX/Y/Z/Time`, `CombatIntensity`, `IsInBossZone`, `VultureCooldownUntil`, `VulturePhase`
  - `VulturePhase` byte constants: None=0, Approach=1, SilentApproach=2, HoldAmbush=3, Rush=4, Paranoia=5, Complete=6
  - `updateCombatEvents()` added to `BotHiveMindMonitor` as step 6 in tick order
  - ~91 new tests (28 CombatEventRegistry + 12 CombatEventScanner + 28 VultureTask + 15 VultureSquadStrategy + 8 VultureConfig)
- **Linger system** — post-objective idle behavior with linear decay scoring
  - `LingerTask`: utility task (`BotActionTypeId=Linger(15)`, BaseScore=0.45, hysteresis=0.10)
    - Gates: `ObjectiveCompletedTime > 0`, `!IsInCombat`, `LingerDuration > 0`, elapsed < duration
    - Score: `baseScore * (1 - elapsed / duration)` — linear decay from 0.45 to 0 over linger duration
  - `LingerAction`: BigBrain action extending `CustomLogicDelayedUpdate`
    - Pauses patrol, slight crouch (pose=0.7), random head scans every 3–8s
    - Clears linger state on stop (ObjectiveCompletedTime=0, LingerDuration=0)
  - `LingerConfig`: 10 JSON properties under `questing.linger` (enabled, base_score, duration_min/max, head_scan_interval_min/max, pose, enable_for_pmcs/scavs/pscavs)
  - `BotEntityBridge.SyncQuestState()`: tracks `HasActiveObjective` true→false transitions to set `ObjectiveCompletedTime` and sample `LingerDuration` (10–30s)
  - 4 new fields on `BotEntity`: `ObjectiveCompletedTime`, `LingerDuration`, `IsLingering`, `CurrentGameTime`
  - ~27 new tests (20 LingerTask scoring + 7 LingerConfig deserialization)
- **Continuous GoToObjective scoring** — exponential distance-based decay replacing binary score
  - `GoToObjectiveTask.ScoreEntity()`: `BaseScore * (1 - exp(-distance / 75))` — 0m→0, 50m→0.31, 75m→0.41, 200m→0.61, asymptotic to 0.65
  - Eliminates abrupt score jumps when bot distance changes
  - 7 new distance gradient tests (zero, medium, long, monotonicity, upper bound, formula verification)
- **Context-aware speed and posture** — indoor/combat/approach-aware pose and sprint control
  - `GoToObjectiveAction.Update()`: replaces hardcoded `SetPose(1f)` with layered conditions:
    - Indoor (`EnvironmentId == 0`): pose 0.8, no sprint
    - Combat/suspicious: pose 0.6, no sprint
    - Near objective (<30m): pose 0.75; within 15m: no sprint
  - Uses `Math.Min` to apply the most restrictive condition
- **Zone movement as default fallback** — activates when no quest is available
  - `BotObjectiveLayer.IsActive()`: tries `tryZoneMovementFallback()` when `trySetNextActionUtility()` returns false
  - `tryZoneMovementFallback()`: dispatches `GoToObjective` with "ZoneWander" reason if zone movement is enabled
  - `spawn_point_wander.desirability` bumped from 0 to 3 so zone wander quests compete in quest selection
- **Variable wait times** — random sampling replaces flat 5s wait between objectives
  - `QuestObjectiveStep.SampleWaitTime()`: samples wait time from `[WaitTimeMin, WaitTimeMax]` range (default: 5–15s)
  - `default_wait_time_after_objective_completion` reduced from 5s to 3s (linger adds 10–30s idle on top)
  - Config: `wait_time_min` (5s) and `wait_time_max` (15s) in `questing` section
  - 5 new config validation tests
- **Investigate task** — lightweight gunfire response behavior
  - `InvestigateTask`: utility task (`BotActionTypeId=Investigate(16)`, MaxBaseScore=0.40, hysteresis=0.15)
    - Gates: `HasNearbyEvent`, `!IsInCombat`, not already vulturing, `CombatIntensity >= threshold(5)`
    - Scoring: intensity component (IntensityWeight=0.20) + proximity component (ProximityWeight=0.20)
  - `InvestigateAction`: BigBrain action extending `GoToPositionAbstractAction`
    - 2-state: cautious approach (speed 0.5, pose 0.6) → look around (head scanning, 5–10s)
    - Movement timeout configurable (default: 60s)
  - `InvestigateConfig`: 14 JSON properties under `questing.investigate`
  - 2 new fields on `BotEntity`: `IsInvestigating`, `InvestigateTimeoutAt`
  - ~25 new tests (22 InvestigateTask scoring + 3 InvestigateConfig deserialization)
- **Personality system** — bot-specific scoring modifiers across all utility tasks
  - `BotPersonality`: byte constants (Timid=0, Cautious=1, Normal=2, Aggressive=3, Reckless=4)
  - `PersonalityHelper`: maps from `BotDifficulty` (easy→Cautious, normal→Normal, hard→Aggressive, impossible→Reckless), weighted random fallback
  - `Aggression` float (0.0–1.0): Timid=0.1, Cautious=0.3, Normal=0.5, Aggressive=0.7, Reckless=0.9
  - `ScoringModifiers`: pure C# static helper computing per-task personality multipliers:
    - GoToObjective: lerp(0.85, 1.15, aggression) — aggressive bots rush more
    - Ambush/Snipe: lerp(1.2, 0.8, aggression) — cautious bots camp more
    - Linger: lerp(1.3, 0.7, aggression) — cautious bots linger longer
    - Vulture/Investigate: lerp(0.7/0.8, 1.3/1.2, aggression) — aggressive bots investigate more
    - Loot: lerp(1.1, 0.9, aggression)
  - Set once on bot registration from `BotDifficulty`
  - `PersonalityConfig`: config POCO under `questing.personality`
  - 3 new fields on `BotEntity`: `Personality` (byte), `Aggression` (float), `RaidTimeNormalized` (float)
  - ~49 new tests (14 BotPersonality + 30 ScoringModifiers + 5 PersonalityConfig)
- **Raid time behavior progression** — scoring multipliers change throughout the raid
  - `RaidTimeNormalized` (0.0=start, 1.0=end) synced from game timer each HiveMind tick
  - `ScoringModifiers.RaidTimeModifier()` per-task multipliers:
    - Early raid (0.0–0.2): GoToObjective ×1.2 (rush), Linger ×0.7 (less idle)
    - Mid raid (0.2–0.7): balanced (×1.0)
    - Late raid (0.7–1.0): Linger ×1.3 (more idle), Loot ×1.2 (more looting), GoToObjective ×0.8 (less rushing)
  - Combined modifier: `PersonalityModifier × RaidTimeModifier` applied to all 12 task scores
- **Convergence field tuning** — per-map config, combat event boost, time-based weight decay
  - `ConvergenceMapConfig`: per-map convergence settings (radius, force, enabled) for all 12 maps
    - Factory disabled, Customs 250m/1.0, Woods 400m/0.8, Laboratory 150m/1.2, etc.
  - `CombatPullPoint`: temporary convergence boost toward recent gunfire (linear decay over 30s)
    - `CombatEventRegistry.GatherCombatPull()`: zero-alloc combat event scanning for field integration
  - `ConvergenceTimeWeight`: time-based convergence multiplier — early raid 1.3× (creates encounters), mid 1.0×, late 0.7× (bots spread out)
  - Per-map config wired through `WorldGridManager` → `FieldComposer` → `ConvergenceField`
  - Config: `convergence_per_map`, `combat_convergence_*` under `questing.zone_movement`
  - ~33 new tests (ConvergenceMapConfig defaults + per-map + time weight + combat pull)
- Vulture port analysis document (`docs/vulture-port-analysis.md`)
- Bot looting analysis document (`docs/looting-analysis.md`)
- **Dedicated log file** — per-session log at `BepInEx/plugins/DanW-SPTQuestingBots/log/QuestingBots.log`
  - `LoggingController.InitFileLogger()`: creates/truncates log file with StreamWriter, gated by `debug.dedicated_log_file` config
  - `LoggingController.DisposeFileLogger()`: thread-safe flush/close on raid end
  - Frame-stamped log lines: `[yyyy-MM-dd HH:mm:ss.fff] [LEVEL] F{frame}: message` for timing correlation
  - Thread-safe via `lock` + `[AggressiveInlining]` on `WriteToFile()`
  - Dual-destination: all log calls write to both BepInEx `LogOutput.log` and dedicated file
  - `DebugConfig.DedicatedLogFile` toggle (default: true)
  - `LoggingController` test shim for test project
- **Extensive logging across 166 files** — 720 logging calls (was ~499 across 97 files)
  - ECS core + Utility AI: entity lifecycle, registry operations, task scoring/switching, quest scoring
  - Squad strategies: squad creation/destruction, member tracking, objective assignment, strategy selection, tactical positions
  - Formation movement: speed mode changes, position calculations, formation type selection
  - Combat positioning: role reassignment, combat position computation
  - LOD system: tier changes, human player cache refresh
  - Cover points: validation results, candidate generation, cover point collection
  - Looting system: score computation, claim/release, scan results, gear comparison, upgrade planning, squad coordination
  - Vulture system: phase transitions, combat event recording/scanning, strategy activation, fan-out positions
  - Zone movement: grid creation, field computation, cell scoring, destination selection, objective cycling
  - Movement/pathing: path start/complete, corner reaching/cutting, sprint state, handoff operations
- **XML doc comments across movement/pathing and configuration files**
  - `BotPathData`, `NavJob`, `PathfindingThrottle`, `StaticPathData`, `PathVisualizationData`: full class and member docstrings
  - `LootingConfig`, `VultureConfig`, `ZoneMovementConfig`, `SquadStrategyConfig`: property-level docstrings
  - `SquadCalloutId`, `SquadCalloutDecider`, `FormationSpeedController`: class and method docstrings
- **Spawn entry behavior** — bots pause and scan on first spawn instead of instantly beelining to objectives
  - `SpawnEntryTask`: utility task (`BotActionTypeId=SpawnEntry(17)`, MaxBaseScore=0.80, gating task)
    - Flat score of 0.80 during spawn duration, drops to 0 once complete — overrides all other tasks
    - Does NOT use `ScoringModifiers.CombinedModifier()` (flat gating, not personality-influenced)
    - Gates: `!IsSpawnEntryComplete`, elapsed time < spawn duration
  - `SpawnEntryAction`: BigBrain action extending `CustomLogicDelayedUpdate`
    - 3–5s pause with 360° look rotation, pose 0.85, sprint disabled
    - Squad stagger: 0.5s extra per member index for natural departure spread
  - Direction bias in `GoToObjectiveTask`: small dot-product bonus toward spawn facing direction for 30s after spawn
    - `SpawnFacingBias` decays linearly from 1.0 to 0.0 over `direction_bias_duration` (default: 30s)
    - Prevents immediate U-turns after spawn
  - `SpawnEntryConfig`: 7 JSON properties under `questing.spawn_entry` (enabled, base_duration_min/max, squad_stagger_per_member, direction_bias_duration/strength, pose)
  - 5 new fields on `BotEntity`: `SpawnTime`, `IsSpawnEntryComplete`, `SpawnEntryDuration`, `SpawnFacingX/Z`, `SpawnFacingBias`
  - ~31 new tests (SpawnEntryTask scoring + SpawnEntryConfig deserialization)
- **Head-look variance** — bots glance at flanks, combat events, and squad members while moving
  - `LookVarianceController`: pure C# static system with 3-priority look system
    - Priority 1: Combat event glance — look toward recent gunfire/explosions (configurable chance)
    - Priority 2: Squad member glance — look at nearby squad members within range
    - Priority 3: Flank check — random ±45° head rotation every 5–15s
  - `LookDirectionHelper`: thin Unity wrapper for `BotOwner.Steering.LookToDirection()`
  - Timer state on `BotEntity`: `NextFlankCheckTime`, `NextPoiGlanceTime`, `CurrentFacingX/Z`
  - Integrated into `GoToPositionAbstractAction.ApplyLookVariance()` — called every frame from all movement actions
  - `LookVarianceConfig`: 9 JSON properties under `questing.look_variance` (flank/POI intervals, squad range, combat event chance, enable toggles)
  - ~19 new tests (LookVarianceController priority logic + LookVarianceConfig deserialization)
- **Room clearing behavior** — bots slow down and check corners when entering buildings
  - `RoomClearController`: pure C# static system detecting outdoor→indoor environment transitions
    - `RoomClearInstruction` enum: None, SlowWalk, PauseAtCorner
    - Environment transition detection via BSG `EnvironmentId` (0=indoor, 1=outdoor)
    - Room clear timer: random duration between `DurationMin` and `DurationMax` (default: 3–8s)
    - `IsSharpCorner()`: 2D angle computation between path segments for corner detection
    - `TriggerCornerPause()`: brief pause at sharp corners (configurable duration)
  - Integrated into `GoToObjectiveAction.Update()`:
    - SlowWalk instruction: pose capped at config value (0.7), sprint disabled
    - PauseAtCorner instruction: slightly lower pose (config - 0.1), sprint disabled
  - `RoomClearConfig`: 9 JSON properties under `questing.room_clear` (enabled, duration_min/max, corner_pause_duration, corner_angle_threshold, pose, per-bot-type toggles)
  - Room clear state on `BotEntity`: `LastEnvironmentId`, `RoomClearUntil`, `IsInRoomClear`, `CornerPauseUntil`
  - ~24 new tests (RoomClearController state transitions + RoomClearConfig deserialization)
- **Per-map advection zone configs (Phobos-style)** — bounded influence zones with per-map tuning
  - `AdvectionZoneConfig`: hardcoded defaults for 8 maps with JSON override via `advection_zones_per_map`
    - `AdvectionZoneEntry`: ForceMin/Max, Radius, Decay, EarlyMultiplier, LateMultiplier, BossAliveMultiplier
    - `BuiltinZoneEntry`: tied to BSG BotZone names (resolved from spawn point centroids)
    - `CustomZoneEntry`: arbitrary world X/Z positions
  - `AdvectionZoneLoader`: resolves zones, injects bounded forces with `SampleForce()` and `ComputeTimeMultiplier()`
  - `AdvectionField.AddBoundedZone()`: Phobos formula `pow(clamp01(1 - dist/radius), decay) * strength`
  - Negative force supported (repulsor zones push bots away)
  - Customs Dorms: strong attractor early (1.5×), weak late (0.5×); Interchange center: boss alive boost (1.5×)
  - Wired into `WorldGridManager.Awake()` after `ZoneDiscovery`
  - ~47 new tests (17 AdvectionZoneConfig + 16 AdvectionZoneLoader + 14 AdvectionFieldBounded)
- **Dynamic objective generation** — quests generated from live game state
  - `DynamicObjectiveGenerator`: pure C# generator accepting data parameters, returning Quest objects
    - `GenerateFirefightObjectives()`: clusters nearby combat events → Ambush quests at cluster centroids
    - `GenerateCorpseObjectives()`: filters Death events → MoveToPosition quests at corpse locations
    - `GenerateBuildingClearObjectives()`: indoor zone positions → HoldAtPosition quests
  - `CombatEventClustering`: greedy seed-based clustering + death event filtering (pure C#, testable)
  - `DynamicObjectiveScanner`: MonoBehaviour orchestrator scanning every 30s with tracked quest lifecycle and auto-expiry
  - `CombatEventType.Death = 4` added; `OnBeenKilledByAggressorPatch` extended to record death events in `CombatEventRegistry`
  - `CombatEventRegistry.GatherActiveEvents()`: bulk active event retrieval for scanner
  - `DynamicObjectiveConfig`: 16 JSON properties under `questing.dynamic_objectives` (firefight/corpse/building-clear toggles, thresholds, desirability scores)
  - ~40 new tests (DynamicObjectiveGenerator + DynamicObjectiveConfig)
- **Patrol route system** — named patrol routes per map with waypoint-following behavior
  - `PatrolRoute`: Name, Type (Perimeter/Interior/Overwatch), waypoints with pause durations, personality+time filters, loop control
  - `PatrolRouteConfig`: hardcoded defaults for 5 maps (Customs 3 routes, Interchange 2, Shoreline 2, Reserve 2, Woods 1) + JSON override via `routes_per_map`
  - `PatrolRouteSelector`: proximity score (0.6 weight) + personality fit score (0.4 weight) + deterministic jitter tiebreak
  - `PatrolTask`: utility task #14, `BotActionTypeId=Patrol(18)`, MaxBaseScore=0.50, fills idle time between objectives
    - Gates: `!IsInCombat`, `!HasActiveObjective`, routes available, `!cooldown`
    - Lazy route assignment in `ScoreEntity()` via `PatrolRouteSelector`
  - `PatrolAction`: BigBrain `GoToPositionAbstractAction` with navigate→pause→advance state machine
    - Head scanning during pauses, movement timeout (90s), stuck detection, cooldown on completion
    - Loop routes (Perimeter/Interior) restart at waypoint 0; non-loop routes (Overwatch) complete after last waypoint
  - `ScoringModifiers`: Patrol case — cautious bots patrol more (1.2×), aggressive less (0.8×); late raid more patrolling (1.2×)
  - `PatrolConfig`: 10 JSON properties under `questing.patrol` (enabled, base_score, cooldown, waypoint radius, pose, per-bot-type toggles)
  - 4 new fields on `BotEntity`: `PatrolRouteIndex`, `PatrolWaypointIndex`, `IsPatrolling`, `PatrolCooldownUntil`
  - ~60 new tests (PatrolRouteConfig + PatrolRouteSelector + PatrolTask + PatrolConfig + PatrolRoute)

### Changed
- Utility AI now has 14 scored tasks (was 8): added Loot, Vulture, Linger, Investigate, SpawnEntry, and Patrol
- `QuestTaskFactory.TaskCount` updated from 8 to 14
- All task scores now modified by personality (aggression) and raid time progression multipliers
- GoToObjective scoring changed from binary (0 or BaseScore) to continuous exponential decay based on distance
- `default_wait_time_after_objective_completion` reduced from 5s to 3s
- `BotHiveMindMonitor.Update()` tick order expanded to 9 steps (was 5 in v1.9.0):
  1. updateBosses, 2. updateBossFollowers, 3. updatePullSensors, 4. ResetInactiveEntitySensors,
  5. updateSquadStrategies (+ formations, combat positioning, zone follower spread, voice commands),
  6. updateCombatEvents (CombatEventRegistry cleanup + CombatEventScanner),
  7. updateLootScanning, 8. refreshHumanPlayerCache, 9. updateLodTiers
- 1922 client tests total (was 938), 58 server tests, 1980 total

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
- **Squad strategies (Phobos-style coordinated group questing)** — enabled by default (`squad_strategy.enabled`, default: true; F12 toggle `Enable Squad Strategies`, default: true)
  - Followers receive tactical positions computed from their boss's quest objective instead of standing idle
  - `SquadEntity`: per-squad data container with stable recycled ID, leader/member tracking, shared objective, and strategy scoring state
  - `SquadRegistry`: dense squad storage with swap-remove, ID recycling, and BSG group ID mapping for O(1) external-ID-to-squad lookup
  - `SquadObjective`: shared objective state (position, tactical positions, arrival tracking, duration with Gaussian sampling)
  - `SquadRole` enum: None, Leader, Guard, Flanker, Overwatch, Escort
  - `SquadStrategy` / `SquadStrategyManager`: Phobos-style scored strategy framework with additive hysteresis and swap-remove active squad tracking
  - `GotoObjectiveStrategy`: primary squad strategy — observes boss objective, computes tactical positions via `TacticalPositionCalculator`, tracks member arrivals, adjusts hold duration with Gaussian sampling
  - `TacticalPositionCalculator`: quest-type-aware position computation (Ambush→flanking at 120° spread, Snipe→overwatch at 150° spread, PlantItem→perimeter guard, HoldAtPosition→circular spread, MoveToPosition→trail formation)
  - `SquadStrategyConfig`: 10-property config under `questing.squad_strategy` (enabled, arrival radius, formation spacing, spread angles, use quest type roles)
  - `GoToTacticalPositionTask` (score=0.70, h=0.20): utility task scoring high when follower is far from tactical position (>3m)
  - `HoldTacticalPositionTask` (score=0.65, h=0.10): utility task scoring high when follower is close to tactical position (<=3m)
  - `SquadTaskFactory`: static factory creating `UtilityTaskManager` with 2 follower-specific tasks
  - Three follower gates partially unlocked: Gate 1 (`BotObjectiveLayer.IsActive`) allows `SquadQuest` followers through, Gate 2 (`getFollowerDecision`) returns `SquadQuest` instead of killing assignments, Gate 3 implicitly handled by BotObjectiveLayer entry
  - Squad lifecycle wired into `BotHiveMindMonitor.Update()` as step 5: position sync, squad creation/wiring, objective sync, strategy scoring
  - `GoToObjectiveAction.tryMoveToObjective()` forks to tactical position when follower has one assigned
  - `BotEntityBridge`: 6 new squad methods (RegisterSquad, AddToSquad, RemoveFromSquad, HasTacticalPosition, SyncPosition, SyncSquadObjective)
  - ~167 new client tests: SquadEntity (16), SquadRegistry (35), SquadObjective (10), SquadStrategy (18), SquadStrategyManager (20), GotoObjectiveStrategy (19), TacticalPositionCalculator (24), GoToTacticalPositionTask (9), HoldTacticalPositionTask (10), SquadTaskFactory (5), SquadStrategyConfig (1)
- **Door collision bypass (Phobos-style)** — enabled by default (`bypass_door_colliders`, default: true)
  - `ShrinkDoorNavMeshCarversPatch`: Harmony postfix on `GameWorld.OnGameStarted` — shrinks all door NavMesh carver sizes to 37.5% to prevent narrow hallways from being blocked by open doors on the navmesh
  - `DoorCollisionHelper`: static helper caching door colliders at map start, then disabling `Physics.IgnoreCollision` + `EFTPhysicsClass.IgnoreCollision` between each bot's colliders and all door colliders
  - Per-bot bypass applied in `CustomLayerForQuesting` constructor (once per bot lifetime)
  - Cleanup at raid end via `BotsControllerStopPatch`
  - Config: `bypass_door_colliders` in `BotPathingConfig` (default: true)
  - 10 new config deserialization tests for `BotPathingConfig`
- **NavMesh.Raycast corner-cutting** — Phobos-style two-tier corner-reaching for smoother path following
  - `CustomPathFollower.TryCornerCut()`: pure-logic method that skips the current corner when the caller confirms NavMesh line-of-sight to the next corner (within 1m proximity)
  - `CustomMoverController.TryNavMeshCornerCut()`: Unity integration that performs `NavMesh.Raycast` and delegates to `TryCornerCut()` — called every frame before `ExecuteMovement()`
  - Complementary to existing Chaikin smoothing: Chaikin reduces corner count offline, NavMesh.Raycast skips corners at runtime when clear line-of-sight exists
  - 8 new tests covering all TryCornerCut edge cases
- **SAIN-inspired squad enhancements** — communication range gating, squad personality, probabilistic position sharing (enabled by default)
  - `CommunicationRange`: earpiece equipment check gates tactical position sharing between leader and followers (35m voice range, 200m earpiece range — inspired by SAIN's `isInCommunicationRange`)
  - `SquadPersonalityCalculator`: determines squad personality from member BotType distribution (Boss→Elite, PMC→GigaChads, Scav→Rats, PScav→TimmyTeam6)
  - `SquadPersonalitySettings`: coordination level (1-5) affects sharing probability via SAIN formula: `25% + coordination × 15%` (Elite=100%, GigaChads=85%, Rats=55%, TimmyTeam6=40%)
  - `SquadPersonalityType` enum: None, TimmyTeam6, Rats, GigaChads, Elite (matching SAIN's `ESquadPersonality`)
  - `HasEarPiece` field on `BotEntity`: synced from game equipment slot, determines communication range tier
  - Personality + coordination fields on `SquadEntity`: computed once when squad forms from member composition
  - `GotoObjectiveStrategy.AssignNewObjective()`: followers who fail comm range check or probability roll get `HasTacticalPosition = false` and fall back to simple follow behavior
  - Config: `enable_communication_range` (default: true), `communication_range_no_earpiece` (35m), `communication_range_earpiece` (200m), `enable_squad_personality` (default: true)
  - ~42 new client tests: SquadPersonalitySettings (10), SquadPersonalityCalculator (10), CommunicationRange (10), GotoObjectiveStrategy comm/personality (12)
- Updated `docs/utility-ai-analysis.md` — Phase 1 Core + Option A marked as implemented
- Updated `docs/phobos-comparison.md` — door bypass and corner-cutting moved from "Still Learn" to "Has Adopted"

### Changed
- 938 client tests total (was 896), 58 server tests, 996 total

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
