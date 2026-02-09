# SPTQuestingBots

An AI bot questing and spawning system for **Single Player Tarkov (SPT) 4.x**. Bots perform quest objectives, spawn dynamically as PMCs and player Scavs, and navigate the map with purpose -- instead of mindlessly patrolling their spawn areas.

Ported from the original [SPT 3.x TypeScript mod](https://hub.sp-tarkov.com/files/file/1534-questing-bots/) (v0.10.3) by **DanW** to a native C# solution for SPT 4.x.

---

## Features

### Objective System
- Bots move around the map performing randomly-selected quest objectives (EFT quests, spawn rushes, boss hunts, airdrop chasing, sniping/camping, and custom quests)
- Bots react to combat, injuries, suspicious noises, low resources, and encumbrance
- Group coordination: followers stay near bosses, regroup when separated, share combat awareness
- Locked-door interaction: PMCs can unlock doors along quest paths
- Interop with SAIN (combat/extraction), Looting Bots (loot scanning), and Donuts (spawn management)

### PMC and Player-Scav Spawning
- PMCs spawn at actual EFT spawn points at raid start, with staggered replacements as they die
- Player Scavs spawn on a schedule mirroring live Tarkov reduced-raid-time settings
- Advanced spawning tricks EFT into treating AI as human players, preserving normal Scav/boss spawns
- Configurable group sizes (solo through 5-man squads), difficulty distribution, and spawn distances

### AI Limiter
- Built-in AI limiter that respects questing behavior (disables distant bots without breaking objectives)
- Per-map distance thresholds and configurable limits

### Scav Spawn Restrictions
- Spawn-rate limiting, max-alive-scav caps, and distance-based exclusion zones to prevent Scav swarms

### Zone-Based Movement System
- Grid + vector-field architecture gives idle bots purposeful movement toward interesting map areas
- Auto-detects map bounds from spawn points; no per-map configuration needed
- Bots are pulled toward human players (convergence field) and pushed toward geographic zones (advection field)
- Per-bot momentum and noise ensure each bot picks a unique direction, eliminating herd movement
- Dynamic objective cycling: bots select next destination via live field state instead of nearest-to-bot
- POI-aware: bots choose contextual actions (ambush, snipe, hold position) based on nearby containers, exfils, and quest triggers
- Serves as fallback when no higher-priority quests are available
- 2D debug minimap: real-time visualization of grid cells, field vectors, bot/player positions, and zone sources
- F12 menu toggles for enable/disable, debug overlay, and debug minimap

### Custom Movement System (Optional)
- Phobos-style `Player.Move()` replacement — enabled by default (`use_custom_mover`, default: true)
- Custom path follower with corner-reaching epsilon, path-deviation spring force, and sprint angle-jitter gating
- Chaikin path smoothing subdivides NavMesh corners for smoother trajectories
- 3 BSG patches: `ManualFixedUpdate` skip, `IsAI` → false (human-like params), vault enable for AI
- Layer handoff: 6-field BSG state sync on layer exit, `SetPlayerToNavMesh()` for clean mover resume
- Same `Player.Move()` paradigm as SAIN and Phobos — aligns with mod ecosystem
- When disabled, QuestingBots falls back to BSG's native `BotOwner.FollowPath()`

### ECS-Lite Data Layout
- Dense entity storage with swap-remove and ID recycling, inspired by Phobos's EntityArray pattern
- `BotEntity`: per-bot data container with stable recycled ID, boss/follower hierarchy, embedded sensor state, field state, and job assignment tracking
- `BotRegistry`: dense list with O(1) add/remove/lookup, plus BsgBotRegistry-style sparse array for O(1) integer ID lookups without hash computation
- Sensor booleans (combat, suspicious, questing, sprint, loot) embedded directly on entity — replaces 5 separate dictionaries
- Bot classification (`BotType` enum) and sleep state replace scattered HashSet/List lookups
- Zero-allocation group query helpers for checking sensor state across boss/follower hierarchies
- `HiveMindSystem`: static system methods for boss/follower lifecycle, sensor resets, and O(n) entity counting — replaces dictionary-based HiveMind operations
- `QuestScorer`: pure-logic quest scoring with static buffers — replaces 5 dictionary allocations + `OrderBy` in quest selection hot path
- `BotEntityBridge`: ECS-only integration layer — push sensors write only to ECS, pull sensors iterate dense entity list with zero allocation, boss/follower lifecycle uses O(1) `IsActive` checks, ProfileId→entity mapping for O(1) string lookups
- Full ECS migration complete (Phases 5A–5F, 6, 8): all old dictionaries (`deadBots`, `botBosses`, `botFollowers`, `sensors`, `botFieldStates`, `botJobAssignments`) and 6 sensor subclasses deleted; ECS is the sole data store for all sensor, sleep, type, boss/follower, zone movement field state, and job assignment data
- Deterministic tick order (Phase 7C): `BotHiveMindMonitor.Update()` orchestrates all ECS system calls in a fixed 4-step sequence
- Allocation cleanup (Phase 7D): static reusable buffers for `GetFollowers()`/`GetAllGroupMembers()`, O(n) min/max scans replacing Dictionary+LINQ chains, for-loop replacements for LINQ `.Where().ToArray()` patterns
- Job assignment wiring (Phase 8): `botJobAssignments` dictionary migrated to `BotEntityBridge` keyed by entity ID; `ConsecutiveFailedAssignments` cached as O(1) entity field; `NumberOfActiveBots()` iterates dense entity list
- `TimePacing` / `FramePacing`: reusable rate-limiter utilities with `[AggressiveInlining]`, inspired by Phobos
- Pure C# with zero Unity dependencies for full testability

---

## Architecture

SPTQuestingBots consists of two components:

| Component | Description |
|-----------|-------------|
| **Server Plugin** (`SPTQuestingBots.Server`) | C# server-side mod providing HTTP API endpoints for configuration, quest data, bot generation, and game state |
| **Client Plugin** (`SPTQuestingBots.Client`) | BepInEx plugin with Harmony patches, custom AI behavior layers (via BigBrain), spawning systems, and quest management |

The client plugin communicates with the server plugin over SPT's internal HTTP API to fetch configuration, quest data, and bot profiles.

For detailed architecture documentation, see [docs/architecture.md](docs/architecture.md).

---

## Prerequisites

| Requirement | Version |
|-------------|---------|
| SPT (Single Player Tarkov) | 4.x |
| .NET SDK | 9.0 |
| .NET Framework | 4.7.2 (target runtime, bundled with SPT) |
| BepInEx | 5.x (bundled with SPT) |
| BigBrain | 1.3.2+ |
| Waypoints | 1.7.1+ |

**Highly recommended:**
- [SAIN](https://hub.sp-tarkov.com/files/file/1062-sain-2-0-solarint-s-ai-modifications-full-ai-combat-system-replacement/) (4.0.3+) -- AI combat and extraction behavior
- [Looting Bots](https://hub.sp-tarkov.com/files/file/1096-looting-bots/) (1.5.2+) -- Bot looting behavior

---

## Installation

1. Download the latest release archive.
2. Extract and copy the **server plugin** to your SPT server mods directory:
   ```
   SPT/user/mods/DanW-SPTQuestingBots/
   ```
3. Copy the **client plugin** DLL to your BepInEx plugins directory:
   ```
   SPT/BepInEx/plugins/DanW-SPTQuestingBots/SPTQuestingBots.dll
   ```
4. Launch SPT. Configure options via the F12 BepInEx menu in-game.

---

## Mod Compatibility

**Compatible:**
- SWAG + DONUTS, Late to the Party, Performance Improvements (0.2.4+)

**Partially compatible:**
- Fika (disable `Enforced Spawn Limits` in F12 if using QuestingBots spawning)
- Path to Tarkov / Traveler / Entry Point Selector (must use a spawn manager mod like SWAG + DONUTS)
- Realism (disable its bot-spawning changes when using QuestingBots spawning)

**Not compatible:**
- AI Limit or similar mods that disable AI globally (use the built-in AI limiter instead)

> If using other spawn-management mods, disable the QuestingBots spawning system to avoid excessive bot counts. The spawning system auto-disables when SWAG + DONUTS, MOAR, Better Spawns Plus, Reality, or ABPS are detected.

---

## Development Setup

### 1. Clone the repository

```bash
git clone <repository-url>
cd SPTQuestingBots
```

### 2. Copy required game assemblies

Create a `libs/` folder in the repository root and copy DLLs from your SPT 4.x installation.

**Server plugin** (`SPTQuestingBots.Server`):

| DLL | Source Path |
|-----|-------------|
| `SPTarkov.Server.Core.dll` | SPT server build output |
| `SPTarkov.DI.dll` | SPT server build output |
| `SPTarkov.Common.dll` | SPT server build output |
| `Newtonsoft.Json.dll` | SPT server / EFT managed |

**Client plugin** (`SPTQuestingBots.Client`):

| DLL | Source Path |
|-----|-------------|
| `BepInEx.dll` | `SPT/BepInEx/core/` |
| `0Harmony.dll` | `SPT/BepInEx/core/` |
| `Assembly-CSharp.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `UnityEngine.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `UnityEngine.CoreModule.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `UnityEngine.IMGUIModule.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `UnityEngine.PhysicsModule.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `UnityEngine.TextRenderingModule.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `UnityEngine.AIModule.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `UnityEngine.UI.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `Comfort.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `Comfort.Unity.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `CommonExtensions.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `DissonanceVoip.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `ItemComponent.Types.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `Newtonsoft.Json.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `Sirenix.Serialization.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `Unity.Postprocessing.Runtime.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `Unity.TextMeshPro.dll` | `SPT/EscapeFromTarkov_Data/Managed/` |
| `spt-common.dll` | `SPT/BepInEx/plugins/spt/` |
| `spt-custom.dll` | `SPT/BepInEx/plugins/spt/` |
| `spt-reflection.dll` | `SPT/BepInEx/plugins/spt/` |
| `spt-singleplayer.dll` | `SPT/BepInEx/plugins/spt/` |
| `spt-prepatch.dll` | `SPT/BepInEx/patchers/` |
| `DrakiaXYZ-BigBrain.dll` | BigBrain mod |

### 3. Build

```bash
make build           # Build both server and client plugin DLLs
make build-server    # Build server plugin only
make build-client    # Build client plugin only
```

Build output goes to `build/`.

### 4. Run tests

```bash
make test            # Run all tests
make test-server     # Server tests only
make test-client     # Client tests only
```

### 5. Code formatting and linting

```bash
make format          # Auto-format with CSharpier
make format-check    # Check formatting (CI-safe, no changes)
make lint            # Check code style against .editorconfig
make lint-fix        # Auto-fix code style issues
```

### 6. Full CI pipeline

```bash
make ci              # restore -> format-check -> lint -> build-tests -> test
```

For detailed development instructions, see [docs/development.md](docs/development.md).

---

## Project Structure

```
SPTQuestingBots/
├── SPTQuestingBots.sln              # Solution file
├── Directory.Build.props            # Shared project properties (net472, version)
├── Makefile                         # Build, test, format, lint targets
├── .editorconfig                    # Code style rules
├── libs/                            # Game DLLs (gitignored, manually populated)
├── src/
│   ├── SPTQuestingBots.Server/      # Server-side plugin (C# port of TypeScript mod)
│   │   └── SPTQuestingBots.Server.csproj
│   └── SPTQuestingBots.Client/      # BepInEx client plugin
│       ├── SPTQuestingBots.Client.csproj
│       ├── QuestingBotsPlugin.cs    # Plugin entry point
│       ├── BehaviorExtensions/      # Custom BigBrain AI layers
│       ├── BotLogic/                # Bot AI decision making
│       │   ├── ECS/                 #   Entity data containers + system methods
│       │   │   └── Systems/         #   HiveMindSystem (static dense-list iteration)
│       │   ├── BotMonitor/          #   Health, combat, extraction monitors
│       │   ├── HiveMind/            #   Group coordination sensors
│       │   ├── Follow/              #   Boss follower behavior
│       │   ├── Objective/           #   Quest objective actions
│       │   ├── Sleep/               #   AI limiter sleep behavior
│       │   └── ExternalMods/        #   SAIN, LootingBots, Donuts interop
│       ├── Components/              # Core game components
│       │   └── Spawning/            #   PMC, PScav, Bot generators
│       ├── Configuration/           # 30+ configuration model classes
│       ├── Controllers/             # Bot management (jobs, objectives, config, logging)
│       ├── Helpers/                 # NavMesh, pathing, items, quests, debug utilities
│       ├── Models/                  # Data models (questing, pathing, debug gizmos)
│       ├── ZoneMovement/            # Grid + vector-field movement system
│       │   ├── Core/                #   WorldGrid, GridCell, PointOfInterest
│       │   ├── Fields/              #   AdvectionField, ConvergenceField, FieldComposer
│       │   ├── Selection/           #   CellScorer, DestinationSelector
│       │   └── Integration/         #   WorldGridManager, ZoneQuestBuilder, ZoneDebugOverlay
│       └── Patches/                 # 25+ Harmony patches
│           ├── Movement/            #   Custom mover BSG patches (opt-in)
│           ├── Spawning/            #   Spawn system patches
│           ├── Lighthouse/          #   Lighthouse-specific patches
│           └── Debug/               #   Debug visualization patches
├── tests/
│   ├── SPTQuestingBots.Server.Tests/  # Server unit tests (NUnit + NSubstitute)
│   └── SPTQuestingBots.Client.Tests/  # Client unit tests (NUnit + NSubstitute)
├── config/                          # JSON configuration files
│   ├── config.json                  # Main mod configuration
│   ├── eftQuestSettings.json        # EFT quest integration settings
│   └── zoneAndItemQuestPositions.json  # Zone/item position overrides
├── quests/
│   └── standard/                    # Standard quest definitions per map
└── docs/                            # Developer documentation
```

---

## Configuration

The mod is configured through `config/config.json` and the BepInEx F12 in-game menu. Key sections:

| Section | Description |
|---------|-------------|
| `enabled` | Master toggle for the entire mod |
| `debug` | Debug visualization and forced spawning options |
| `chance_of_being_hostile_toward_bosses` | Per-bot-type boss hostility chances |
| `questing` | Objective system: brain layer priorities, quest selection, pathing, stuck detection, door unlocking, sprint limits, extraction |
| `questing.bot_quests` | Quest-type settings: EFT quests, spawn rush, boss hunter, airdrop chaser, spawn wandering |
| `bot_spawns` | PMC/PScav spawning: group sizes, difficulty, distances, hostility, bot caps, boss limits |
| `questing.bot_pathing` | Path following: custom mover toggle (`use_custom_mover`), incomplete path retry, start position discrepancy |
| `questing.zone_movement` | Zone-based movement: grid size, field weights, POI scoring, convergence interval, debug overlay |
| `adjust_pscav_chance` | Dynamic player-Scav conversion rates when spawning system is disabled |

### Custom Quests

Create custom quest files in `quests/custom/` with the same filenames as `quests/standard/` (one per map). See the original README sections on quest data structures for the full schema (Quests, Objectives, Steps).

---

## Contributing

1. Fork the repository and create a feature branch
2. Follow the code style enforced by `.editorconfig` and CSharpier
3. Add tests for new functionality (NUnit 3.x + NSubstitute)
4. Run `make ci` to verify formatting, linting, and tests pass
5. Submit a pull request

---

## License

[MIT](https://opensource.org/licenses/MIT)

---

## Credits

- **DanW** -- Original SPTQuestingBots mod (v0.10.3, SPT 3.x TypeScript)
- **DrakiaXYZ** -- BigBrain and Waypoints frameworks, development assistance
- **Props** -- DONUTS spawning code inspiration
- **Skwizzy** -- Looting Bots interop
- **Solarint** -- SAIN interop and combat behavior balancing
- **nooky** -- Testing and SWAG + DONUTS compatibility
- **ozen** -- Testing and SPT 3.11 updates
- The SPT development team and the Discord testing community
