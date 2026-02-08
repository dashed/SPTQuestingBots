# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

### Changed
- Zone quests now use field-based objective cycling for dynamic, non-repetitive bot movement patterns
- 172 client tests total (was 158), 58 server tests, 230 total

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
