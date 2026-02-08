# Zone-Based Movement System: Design Document

## 1. Executive Summary

Replace QuestingBots' per-map JSON quest files with a **map-agnostic, physics-inspired movement system** that makes raids feel alive by pushing bots toward interesting areas near the player. Inspired by Phobos's grid + vector-field architecture but designed to require **zero per-map configuration**.

### Key Goals
1. **Make bots move around the map**, creating dynamic, living raids
2. **No per-map JSON files** — system auto-discovers map geometry and points of interest
3. **Future-proof** — works on any map without manual configuration
4. **Testable** — pure-logic core with no Unity/EFT dependencies

### Phobos vs This Design

| Aspect | Phobos | This Design |
|--------|--------|-------------|
| Grid bounds | Hardcoded per-map `MapGeometry` (12 maps) | Auto-detected from `SpawnPointParams` |
| Cell size | Per-map config (25–125m) | `sqrt(mapArea / 150)` → ~150 cells |
| Zone sources | Hardcoded per-map zones | Auto-discovered from BSG `BotZone` objects |
| POI sources | Scene scan (containers, doors, exfils) | Same approach, plus spawn points |
| Movement model | advection + convergence + momentum + noise | Same physics model |
| Squad system | Custom `SquadRegistry` | Existing HiveMind boss/follower system |
| Actions | MoveToPosition, Ambush, Snipe, PlantItem | Same actions, selected by POI category |
| Config files | Per-map JSON | Single `zone_movement` config section |

---

## 2. Architecture Overview

```
[Scene] → MapBoundsDetector → WorldGrid (auto-sized cells)
[Scene] → PoiScanner → PointOfInterest[] → GridCell.POIs
[Scene] → ZoneDiscovery → AdvectionField (static zone influence)
[Players] → ConvergenceField (dynamic player attraction, 30s update)
[Bot state] → FieldComposer (advection + convergence + momentum + noise)
[Composite dir] → DestinationSelector → best neighbor cell
[GridCell] → ZoneQuestBuilder → Quest → BotJobAssignmentFactory → BotObjectiveLayer
```

### Integration Strategy

The zone movement system is a **fallback objective source** integrated via the existing quest pipeline. Existing quest-based objectives take priority. This means:

1. `WorldGridManager` initializes in `LocationData.Awake()` (before `BotQuestBuilder`)
2. `ZoneQuestBuilder.CreateZoneQuests()` creates a `Quest` in `BotQuestBuilder.LoadAllQuests()` with low desirability (5)
3. Quest registered via `BotJobAssignmentFactory.AddQuest()` — bots pick it up through normal assignment flow
4. `BotObjectiveLayer.trySetNextAction()` handles action-specific behavior (Ambush, Snipe, PlantItem, etc.)
5. Quest is repeatable (`MaxBots = 99`) — bots cycle through grid cell objectives

This is non-destructive: it required **zero changes** to `BotObjectiveManager`, `BotJobAssignmentFactory`, or `BotObjectiveLayer`.

---

## 3. Phase 1: Pure Logic Layer (Testable)

All classes in this phase have **zero Unity/EFT dependencies**. They use `Vector3` positions (compatible with the existing test shim) and return computed results. ~30–40 unit tests.

### 3.1 Directory Structure

```
src/SPTQuestingBots.Client/ZoneMovement/
├── Core/
│   ├── WorldGrid.cs
│   ├── GridCell.cs
│   ├── PointOfInterest.cs
│   └── PoiCategory.cs
├── Fields/
│   ├── AdvectionField.cs
│   ├── ConvergenceField.cs
│   └── FieldComposer.cs
└── Selection/
    ├── DestinationSelector.cs
    └── CellScorer.cs

tests/SPTQuestingBots.Client.Tests/ZoneMovement/
├── WorldGridTests.cs
├── GridCellTests.cs
├── AdvectionFieldTests.cs
├── ConvergenceFieldTests.cs
├── FieldComposerTests.cs
├── DestinationSelectorTests.cs
└── CellScorerTests.cs
```

### 3.2 Core Types

#### `PoiCategory` (enum)
```csharp
public enum PoiCategory
{
    Container,    // LootableContainer
    LooseLoot,    // LootItem
    Quest,        // TriggerWithId
    Exfil,        // ExfiltrationPoint
    SpawnPoint,   // SpawnPointMarker
    Synthetic     // NavMesh-sampled fill for empty cells
}
```

#### `PointOfInterest` (data class)
```csharp
public sealed class PointOfInterest
{
    public Vector3 Position { get; }
    public PoiCategory Category { get; }
    public float Weight { get; }  // Category-based: Container=1.0, Exfil=0.5, Synthetic=0.2
}
```

#### `GridCell`
- **Fields**: `Vector3 Center`, `int Col`, `int Row`, `List<PointOfInterest> POIs`, `GridCell[] Neighbors` (4 or 8 cardinal)
- **Computed**: `float PoiDensity` (sum of POI weights), `bool IsNavigable` (has at least one NavMesh-valid POI or synthetic point)
- **Purpose**: Unit of spatial partitioning. Bots move cell-to-cell.

#### `WorldGrid`
- **Constructor**: `WorldGrid(Vector3 minBounds, Vector3 maxBounds, float cellSize)`
- **Fields**: `GridCell[,] Cells`, `int Cols`, `int Rows`, `float CellSize`
- **Methods**:
  - `GridCell GetCell(Vector3 position)` — O(1) lookup by position
  - `GridCell GetCell(int col, int row)` — direct indexing
  - `void AddPoi(PointOfInterest poi)` — adds to appropriate cell
  - `IReadOnlyList<GridCell> GetNeighbors(GridCell cell)` — 4-connected neighbors within bounds
- **Auto-sizing**: `cellSize = sqrt((maxX - minX) * (maxZ - minZ) / targetCellCount)`
- **Target**: ~150 cells per map (configurable)

### 3.3 Field Types

#### `AdvectionField`
Static vector field that pushes bots toward interesting geographic zones.

- **Sources**: List of `(Vector3 position, float strength)` tuples (from BotZones)
- **Crowd repulsion**: List of `(Vector3 position)` for other bot positions → inverse-square repulsion
- **Method**: `Vector2 GetAdvection(Vector3 position, IReadOnlyList<Vector3> botPositions)`
  - Sum zone attraction vectors (weighted by 1/distance)
  - Subtract crowd repulsion vectors (weighted by `strength / distance²`)
  - Normalize result

#### `ConvergenceField`
Dynamic field pulling bots toward human players.

- **Update**: Recomputed periodically (every 30s)
- **Method**: `Vector2 GetConvergence(Vector3 position, IReadOnlyList<Vector3> playerPositions)`
  - Sum attraction toward each player, weighted by `1 / sqrt(distance)`
  - Normalize result
- **Staleness**: Returns cached value between updates; accepts `float currentTime` to check interval

#### `FieldComposer`
Combines all fields into a single direction vector.

- **Inputs**: advection, convergence, bot's current momentum (direction of travel), random noise
- **Weights** (configurable):
  - Convergence: `1.0` (strongest — bots should move toward players)
  - Advection: `0.5` (moderate — geographic interest)
  - Momentum: `0.5` (moderate — smooth paths, not zigzag)
  - Noise: `0.3` (mild — prevent deterministic movement)
- **Method**: `Vector2 GetCompositeDirection(Vector2 advection, Vector2 convergence, Vector2 momentum, float noiseAngle)`
  - Weighted sum → normalize
  - `noiseAngle` adds rotation in [-maxNoise, +maxNoise] radians

### 3.4 Selection

#### `CellScorer`
Scores candidate cells for destination selection.

- **Method**: `float Score(GridCell candidate, Vector2 compositeDirection, Vector3 fromPosition)`
  - `angleFactor = 1.0 - (angle / π)` — cells aligned with composite direction score higher
  - `poiFactor = min(candidate.PoiDensity / maxDensity, 1.0)` — POI-rich cells get a bonus
  - `score = angleFactor * (1 - poiWeight) + poiFactor * poiWeight`
  - `poiWeight` configurable (default: `0.3`)

#### `DestinationSelector`
Picks the best neighbor cell for a bot to move to.

- **Method**: `GridCell SelectDestination(WorldGrid grid, GridCell currentCell, Vector2 compositeDirection, Vector3 botPosition)`
  - Get neighbors of `currentCell`
  - Filter: must be navigable
  - Score each with `CellScorer`
  - Return highest-scoring cell
  - If no valid neighbors: return `currentCell` (hold position)

---

## 4. Phase 2: Scene Integration Layer (COMPLETED)

These classes bridge pure logic to the game world. They depend on Unity/EFT types and cannot be unit tested (except `MapBoundsDetector` and `ZoneActionSelector` which are pure logic), but the logic they contain is thin.

### 4.1 Directory Structure

```
src/SPTQuestingBots.Client/ZoneMovement/
├── Integration/
│   ├── MapBoundsDetector.cs    (pure logic, unit tested)
│   ├── PoiScanner.cs           (scene adapter)
│   ├── ZoneDiscovery.cs        (scene adapter)
│   ├── WorldGridManager.cs     (MonoBehaviour orchestrator + dynamic Update)
│   ├── ZoneQuestBuilder.cs     (quest factory)
│   └── ZoneDebugOverlay.cs     (OnGUI debug overlay)
└── Selection/
    └── ZoneActionSelector.cs   (pure logic, unit tested)
```

### 4.2 Classes

#### `MapBoundsDetector` (pure logic)
Auto-detect map bounds — **no per-map config needed**. Operates on raw `Vector3[]` with no Unity scene dependencies.

- **Method**: `static (Vector3 min, Vector3 max) DetectBounds(Vector3[] positions, float padding = 50f)`
- Y set to ±10000 (XZ plane only), padding expands each edge
- Unit tested with 9 tests

#### `PoiScanner` (scene adapter)
Scans Unity scene for points of interest using `Object.FindObjectsOfType`.

- Scans `LootableContainer` → `PoiCategory.Container`
- Scans `TriggerWithId` → `PoiCategory.Quest`
- Scans `ExfiltrationPoint` → `PoiCategory.Exfil`
- NavMesh validation (2m search distance) filters unreachable positions

#### `ZoneDiscovery` (scene adapter)
Discovers bot zones by grouping `SpawnPointParams` by `BotZoneName`.

- Computes centroid for each zone group
- Normalizes strength: largest zone = 1.0

#### `ZoneActionSelector` (pure logic)
Maps POI categories to bot actions with weighted random selection.

| Category | Action Distribution |
|----------|-------------------|
| Container | 60% Ambush, 20% Snipe, 10% HoldAtPosition, 10% PlantItem |
| LooseLoot | 50% HoldAtPosition, 30% Ambush, 20% MoveToPosition |
| Quest | 70% MoveToPosition, 20% HoldAtPosition, 10% Ambush |
| Exfil | 60% Snipe, 30% Ambush, 10% HoldAtPosition |
| SpawnPoint | 90% MoveToPosition, 10% HoldAtPosition |
| Synthetic | 100% MoveToPosition |

- Returns int action indices to avoid EFT enum dependency
- `GetHoldDuration(int)` returns per-action hold time ranges
- Unit tested with 13 tests (distribution validation, edge cases)

#### `WorldGridManager` (MonoBehaviour)
Orchestrator that creates the grid on raid start and manages field updates.

- **Awake**: Detect bounds → Create `WorldGrid` → `PoiScanner.ScanScene()` → spawn point POIs → `ZoneDiscovery` → `AdvectionField` → synthetic fill → `FieldComposer`/`CellScorer`/`DestinationSelector`
- **API**: `Grid`, `IsInitialized`, `GetCellForBot(Vector3)`, `GetCompositeDirection(...)`, `SelectDestination(...)`
- **Synthetic fill**: Empty grid cells get NavMesh-sampled synthetic POIs

#### `ZoneQuestBuilder` (quest factory)
Creates zone movement quests that plug into the existing quest pipeline.

- Creates one `Quest` with a `QuestObjective` per navigable `GridCell`
- Each objective gets a `QuestObjectiveStep` with action selected by `ZoneActionSelector` based on the cell's dominant POI category
- Registered via `BotJobAssignmentFactory.AddQuest()` with low desirability (5) as fallback
- `IsRepeatable = true`, `MaxBots = 99`

### 4.3 Integration Approach

**Design decision**: Instead of the originally planned `ZoneObjectiveProvider` adapter, zone movement uses the existing quest pipeline:

1. `WorldGridManager` initializes in `LocationData.Awake()` (before `BotQuestBuilder`)
2. `ZoneQuestBuilder.CreateZoneQuests()` creates a `Quest` in `BotQuestBuilder.LoadAllQuests()` (after spawn point wander, before spawn rush)
3. Quest registered via `BotJobAssignmentFactory.AddQuest()` — bots pick it up through normal assignment flow
4. `BotObjectiveLayer.trySetNextAction()` already handles Ambush/Snipe/PlantItem with two-phase dispatch (GoToObjective → action-specific behavior)

This approach required **zero changes** to `BotObjectiveManager`, `BotJobAssignmentFactory`, or `BotObjectiveLayer`.

---

## 5. Phase 3: Dynamic Fields + Debug (COMPLETED in v1.5.0)

### 5.1 Completed Integration (v1.4.0)

- **`LocationData.cs`**: `WorldGridManager` + `ZoneDebugOverlay` instantiated in `Awake()` (guarded by JSON config + F12 toggle)
- **`BotQuestBuilder.cs`**: `ZoneQuestBuilder.CreateZoneQuests()` called in `LoadAllQuests()`, registered via `BotJobAssignmentFactory.AddQuest()`
- **`QuestingConfig.cs`**: Added `ZoneMovement` property with `ZoneMovementConfig` model
- **No changes needed** to `BotObjectiveManager`, `BotJobAssignmentFactory`, or `BotObjectiveLayer`

### 5.2 Config Model

`ZoneMovementConfig` — 12 properties under `questing.zone_movement`:

| Property | Default | Description |
|----------|---------|-------------|
| `enabled` | `true` | Master toggle for zone movement |
| `target_cell_count` | `150` | Target number of grid cells per map |
| `convergence_update_interval_sec` | `30` | How often to refresh player convergence field |
| `convergence_weight` | `1.0` | Weight of player attraction in composite direction |
| `advection_weight` | `0.5` | Weight of zone attraction in composite direction |
| `momentum_weight` | `0.5` | Weight of current travel direction (smoothing) |
| `noise_weight` | `0.3` | Weight of random noise (prevent determinism) |
| `poi_score_weight` | `0.3` | Weight of POI density in cell scoring |
| `crowd_repulsion_strength` | `2.0` | Strength of bot-to-bot repulsion |
| `bounds_padding` | `50` | Padding (meters) around detected map bounds |
| `quest_desirability` | `5` | Quest priority (low = fallback) |
| `quest_name` | `"Zone Movement"` | Quest name in assignment system |

### 5.3 F12 Menu Config (v1.5.0)

Two BepInEx ConfigEntry fields in `QuestingBotsPluginConfig.cs` under "Zone Movement" section:

| Entry | Type | Default | Description |
|-------|------|---------|-------------|
| `ZoneMovementEnabled` | `bool` | `true` | Runtime toggle, overrides JSON config |
| `ZoneMovementDebugOverlay` | `bool` | `false` | Show debug overlay (IsAdvanced) |

### 5.4 Dynamic Convergence (v1.5.0)

`WorldGridManager.Update()` periodically refreshes cached player/bot positions:
- Timer-based: checks `convergenceUpdateInterval` (default 30s)
- Gets positions from `Singleton<GameWorld>.Instance.AllAlivePlayersList`
- Separates human players (`!IsAI`) from bots (`IsAI`)
- Cached lists used by `GetCompositeDirection()` and `GetRecommendedDestination()`

### 5.5 Recommended Destination API (v1.5.0)

`WorldGridManager.GetRecommendedDestination(Vector3 botPosition, float momentumX, float momentumZ)`:
- Returns the best next cell center using live field state
- Uses cached player/bot positions from Update()
- Calls GetCompositeDirection → SelectDestination pipeline
- Provides API for future bot objective lifecycle integration

### 5.6 Debug Overlay (v1.5.0)

`ZoneDebugOverlay` (MonoBehaviour) — OnGUI text panel showing:
- Grid dimensions, cell count, navigable cells, cell size
- POI breakdown by category
- Player's current cell, dominant category, POI density
- Gated behind `QuestingBotsPluginConfig.ZoneMovementDebugOverlay`

### 5.7 Future Work → Phase 4

See **Section 8** for detailed Phase 4 implementation plans:
- **Phase 4A**: Per-bot field state (`BotFieldState`, `ZoneMathUtils.ComputeMomentum`) — S ✅
- **Phase 4B**: Dynamic objective selection (`ZoneObjectiveCycler`, factory integration) — M ✅
- **Phase 4C**: Enhanced 2D debug minimap (grid cells, field arrows, bot dots) — M

---

## 6. Test Strategy

### 6.1 Unit Tests — 172 client tests total (Phases 1–4B)

All use the existing `Vector3` test shim (with `sqrMagnitude`, `magnitude`, `operator-`).

| Test Class | Tests | Coverage |
|------------|-------|----------|
| `WorldGridTests` | 15 | Grid creation, cell count, cell lookup by position, out-of-bounds handling, neighbor enumeration, POI insertion |
| `GridCellTests` | 6 | POI management, density calculation, navigability |
| `PointOfInterestTests` | 4 | Constructor, category-based weights |
| `AdvectionFieldTests` | 12 | Single zone attraction, multiple zones, crowd repulsion inverse-square, zero-distance handling, normalization |
| `ConvergenceFieldTests` | 9 | Single player attraction, multiple players, sqrt falloff, update interval gating, stale cache |
| `FieldComposerTests` | 8 | Weight application, all-zero input, single dominant field, noise rotation, normalization |
| `DestinationSelectorTests` | 7 | Best-angle neighbor, no navigable neighbors, edge cells, POI density bonus |
| `CellScorerTests` | 10 | Perfect alignment score, opposite direction penalty, POI density bonus, configurable weight |
| `ZoneActionSelectorTests` | 13 | Distribution validation per category, hold durations, weight table integrity, edge cases |
| `MapBoundsDetectorTests` | 9 | Single/multiple positions, padding, Y bounds, negative coordinates, error cases |
| `ZoneMathUtilsTests` | 20 | GetDominantCategory (10 cases), ComputeCentroid (5 cases), ComputeMomentum (5 cases) |
| `BotFieldStateTests` | 4 | ComputeMomentum delegation, GetNoiseAngle: different seeds, determinism, output range |
| `ZoneObjectiveCyclerTests` | 5 | Naming convention contract between ZoneQuestBuilder and ZoneObjectiveCycler, format consistency |

### 6.2 Phase 4C Planned Tests

| Test Class | Tests | Coverage |
|------------|-------|----------|

### 6.3 Integration verification — compile-verified

Scene integration classes (`PoiScanner`, `ZoneDiscovery`, `WorldGridManager`, `ZoneQuestBuilder`, `ZoneDebugOverlay`, `ZoneObjectiveCycler`) are thin adapters. Correctness verified by:
1. Pure logic tests (Phases 1-2 cover all computation, Phase 4A covers momentum/noise)
2. In-game debug overlay (`ZoneDebugOverlay` — F12 toggle, shows grid/POI stats)
3. In-game debug minimap (Phase 4C — shows field vectors, bot positions, cell categories)
4. Manual testing on 2-3 maps

---

## 7. Implementation Tasks (Ordered)

### Phase 1: Pure Logic (~8 classes, ~35 tests)

| # | Task | Depends On | Est. Size |
|---|------|-----------|-----------|
| 1 | Create `PoiCategory` enum + `PointOfInterest` data class | — | XS |
| 2 | Create `GridCell` with POI management + density | — | S |
| 3 | Create `WorldGrid` with auto-sizing + cell lookup + neighbors | 1, 2 | M |
| 4 | Create `AdvectionField` with zone attraction + crowd repulsion | — | M |
| 5 | Create `ConvergenceField` with player attraction + interval gating | — | S |
| 6 | Create `FieldComposer` combining all fields | 4, 5 | S |
| 7 | Create `CellScorer` with angle + POI scoring | 2 | S |
| 8 | Create `DestinationSelector` picking best neighbor | 3, 6, 7 | S |
| 9 | Write comprehensive unit tests for all Phase 1 classes | 1–8 | L |

### Phase 2: Scene Integration + Quest Pipeline (COMPLETED in v1.4.0)

| # | Task | Status |
|---|------|--------|
| 10 | `MapBoundsDetector` — auto-detect bounds from positions | Done |
| 11 | `PoiScanner` — scan scene for containers/quests/exfils | Done |
| 12 | `ZoneDiscovery` — discover zones from spawn points | Done |
| 13 | `WorldGridManager` — MonoBehaviour orchestrator | Done |
| 14 | `ZoneActionSelector` — POI-based action selection | Done |
| 15 | `ZoneQuestBuilder` — quest factory for grid cells | Done |
| 16 | `ZoneMovementConfig` — config model (12 properties) | Done |
| 17 | Wire into `LocationData.Awake()` + `BotQuestBuilder.LoadAllQuests()` | Done |
| 18 | Unit tests for `ZoneActionSelector` + `MapBoundsDetector` (22 tests) | Done |

### Phase 3: Dynamic Fields + Debug (COMPLETED in v1.5.0)

| # | Task | Status |
|---|------|--------|
| 19 | F12 menu config entries (ZoneMovementEnabled, ZoneMovementDebugOverlay) | Done |
| 20 | `ZoneDebugOverlay` — OnGUI debug overlay with grid/POI stats | Done |
| 21 | Dynamic convergence field updates in `WorldGridManager.Update()` | Done |
| 22 | `GetRecommendedDestination` API for dynamic bot destinations | Done |

### Phase 4: Dynamic Destination Cycling (PLANNED)

Phase 4 makes zone movement **truly dynamic**: bots choose their next destination based on live field state (advection, convergence, per-bot momentum, per-bot noise) instead of picking from a static pool of pre-built objectives. This is the Phobos-equivalent of `GotoObjectiveStrategy.AssignNewObjective()` → `LocationSystem.RequestNear()`.

Phase 4 is split into three sub-phases with explicit dependencies:

| # | Task | Depends On | Est. Size |
|---|------|-----------|-----------|
| 23 | Per-bot field state (`BotFieldState`, `ZoneMathUtils.ComputeMomentum`) | — | S |
| 24 | Dynamic objective selection (`ZoneObjectiveCycler`, factory integration) | 23 | M |
| 25 | Enhanced 2D debug minimap (grid cells, field arrows, bot dots) | 23, 24 | M |

---

## 8. Phase 4 Detailed Plans

### 8.1 Phase 4A: Per-Bot Field State (#23) ✅ COMPLETE

**Goal**: Give each bot unique momentum and noise vectors so field composition produces distinct directions per bot, eliminating herd movement.

**Research basis**: Phobos computes momentum on-the-fly in `LocationSystem.RequestNear()` as `(requestCoords - previousCoords)` — momentum is derived from the last assigned location, not stored explicitly. We adopt the same pattern but wrap it in a testable data class.

#### New File: `ZoneMovement/Core/BotFieldState.cs` (~35 lines)

```csharp
public sealed class BotFieldState
{
    public Vector3 PreviousDestination { get; set; }
    public int NoiseSeed { get; }

    public BotFieldState(int noiseSeed)
    {
        NoiseSeed = noiseSeed;
        PreviousDestination = Vector3.zero;
    }

    /// Computes normalized XZ momentum from previous destination to current position.
    public (float momX, float momZ) ComputeMomentum(Vector3 currentPosition)
        => ZoneMathUtils.ComputeMomentum(PreviousDestination, currentPosition);

    /// Returns a per-bot noise angle using seeded random + time variation.
    public float GetNoiseAngle(float time)
    {
        // Hash seed + time-bucket to get deterministic-per-bot-but-varying-over-time noise
        int timeBucket = (int)(time / 5f); // changes every 5s
        var rng = new System.Random(NoiseSeed ^ timeBucket);
        return (float)(rng.NextDouble() * 2 * System.Math.PI - System.Math.PI);
    }
}
```

#### Modified File: `ZoneMovement/Core/ZoneMathUtils.cs` (+15 lines)

```csharp
/// Computes normalized XZ-plane momentum direction from 'from' to 'to'.
/// Returns (0, 0) if positions are coincident.
public static (float momX, float momZ) ComputeMomentum(Vector3 from, Vector3 to)
{
    float dx = to.x - from.x;
    float dz = to.z - from.z;
    float mag = (float)Math.Sqrt(dx * dx + dz * dz);
    if (mag < 0.001f) return (0f, 0f);
    return (dx / mag, dz / mag);
}
```

#### Modified File: `WorldGridManager.cs` (+25 lines)

```csharp
private readonly Dictionary<string, BotFieldState> botFieldStates = new();

public BotFieldState GetOrCreateBotState(string botProfileId)
{
    if (!botFieldStates.TryGetValue(botProfileId, out var state))
    {
        state = new BotFieldState(botProfileId.GetHashCode());
        botFieldStates[botProfileId] = state;
    }
    return state;
}

/// Per-bot overload using tracked momentum and noise.
public Vector3? GetRecommendedDestination(string botProfileId, Vector3 botPosition)
{
    if (!IsInitialized) return null;
    var state = GetOrCreateBotState(botProfileId);
    var (momX, momZ) = state.ComputeMomentum(botPosition);
    // ... compose with per-bot noise from state.GetNoiseAngle(Time.time) ...
    // ... update state.PreviousDestination on success ...
}
```

#### Tests: 9 new tests

| Test | Coverage |
|------|----------|
| `ComputeMomentum_SamePoint_ReturnsZero` | Zero-distance edge case |
| `ComputeMomentum_NorthDirection_ReturnsPositiveZ` | Cardinal direction |
| `ComputeMomentum_EastDirection_ReturnsPositiveX` | Cardinal direction |
| `ComputeMomentum_DiagonalDirection_Normalized` | Normalization check |
| `ComputeMomentum_IgnoresYAxis` | XZ plane only |
| `BotFieldState_ComputeMomentum_DelegatesToUtils` | Integration |
| `BotFieldState_GetNoiseAngle_DifferentSeeds_DifferentAngles` | Per-bot uniqueness |
| `BotFieldState_GetNoiseAngle_SameSeedAndTime_Deterministic` | Reproducibility |
| `BotFieldState_GetNoiseAngle_RangeIsPiToPi` | Output bounds |

#### File Summary

| File | Action | Lines |
|------|--------|-------|
| `ZoneMovement/Core/BotFieldState.cs` | NEW | ~35 |
| `ZoneMovement/Core/ZoneMathUtils.cs` | MOD | +15 |
| `ZoneMovement/Integration/WorldGridManager.cs` | MOD | +25 |
| `tests/.../ZoneMovement/BotFieldStateTests.cs` | NEW | ~60 |
| `tests/.../ZoneMovement/ZoneMathUtilsTests.cs` | MOD | +25 |
| `tests/.../SPTQuestingBots.Client.Tests.csproj` | MOD | +1 link |

---

### 8.2 Phase 4B: Dynamic Objective Selection (#24) ✅ COMPLETE

**Goal**: When a bot finishes a zone movement objective, select the next objective using live field state (via `GetRecommendedDestination`) instead of the default nearest-to-bot selection.

**Research basis**: Phobos's `GotoObjectiveStrategy.AssignNewObjective()` calls `LocationSystem.RequestNear(squad, position, previousLocation)` which computes advection + convergence + momentum + noise to pick the best neighboring cell. We replicate this pattern by intercepting objective selection in `BotJobAssignmentFactory` for zone quests.

#### Architecture: Single Integration Point

```
Bot completes zone objective
  → BotObjectiveManager.Update() detects HasWaitedLongEnoughAfterEnding
  → BotJobAssignmentFactory.GetCurrentJobAssignment()
  → DoesBotHaveNewJobAssignment() → GetNewBotJobAssignment()
  → quest selected = zone quest (by desirability weighting)
  → [NEW] ZoneObjectiveCycler.SelectZoneObjective() replaces NearestToBot()
  → Returns field-based QuestObjective
```

#### New File: `ZoneMovement/Integration/ZoneObjectiveCycler.cs` (~55 lines)

```csharp
public static class ZoneObjectiveCycler
{
    /// Selects a zone quest objective using live field state instead of nearest-to-bot.
    /// Returns null if no matching objective found (falls back to default selection).
    public static QuestObjective SelectZoneObjective(
        BotOwner bot,
        Quest zoneQuest,
        WorldGridManager gridManager)
    {
        if (gridManager == null || !gridManager.IsInitialized)
            return null;

        // Get field-based recommended destination using per-bot state
        Vector3? destination = gridManager.GetRecommendedDestination(
            bot.Profile.Id, bot.Position);

        if (!destination.HasValue)
            return null;

        // Find the grid cell for the destination
        GridCell cell = gridManager.GetCellForBot(destination.Value);
        if (cell == null)
            return null;

        // Find the matching objective by cell coordinates
        string objectiveName = $"Zone ({cell.Col},{cell.Row})";
        QuestObjective match = zoneQuest.AllObjectives
            .FirstOrDefault(o => o.Name == objectiveName);

        if (match != null && match.CanAssignBot(bot))
            return match;

        // Fallback: find nearest navigable cell objective
        return zoneQuest.RemainingObjectivesForBot(bot)
            ?.Where(o => o.CanAssignBot(bot))
            ?.NearestToBot(bot);
    }
}
```

#### Modified File: `Controllers/BotJobAssignmentFactory.cs` (~15 lines)

In `GetNewBotJobAssignment()`, after the quest is selected and before the objective loop, add zone quest detection:

```csharp
// [NEW] For zone quests, use field-based selection instead of nearest-to-bot
if (quest?.Name == ConfigController.Config.Questing.ZoneMovement.QuestName)
{
    WorldGridManager gridManager = Singleton<GameWorld>.Instance
        ?.GetComponent<WorldGridManager>();

    QuestObjective zoneObjective = ZoneObjectiveCycler.SelectZoneObjective(
        bot, quest, gridManager);

    if (zoneObjective != null)
    {
        objective = zoneObjective;
        break;
    }
}
```

#### Phobos Destination Lifecycle Comparison

| Step | Phobos | QuestingBots (After #24) |
|------|--------|--------------------------|
| Trigger | Timer expires or all agents reached/failed | `HasWaitedLongEnoughAfterEnding()` |
| Request | `LocationSystem.RequestNear(squad, pos, prev)` | `ZoneObjectiveCycler.SelectZoneObjective(bot, quest, gridManager)` |
| Momentum | `(requestCoords - previousCoords)` | `BotFieldState.ComputeMomentum(botPosition)` |
| Fields | `advection[x,y] + convergence[x,y] + noise` | `FieldComposer.GetCompositeDirection(...)` |
| Selection | Best-angle neighbor cell | `DestinationSelector.SelectDestination(...)` |
| Assignment | `squad.Objective.Location = newLocation` | `BotJobAssignment(bot, quest, matchingObjective)` |
| Hold timer | `_guardDuration.SampleGaussian()` | `QuestObjectiveStep.MinElapsedTime` (already per-step) |

#### Test Strategy

The core logic (field composition, momentum, destination selection) is already tested in Phases 1-2. The new code in ZoneObjectiveCycler is thin integration glue — primarily string matching on objective names. Testing:

| Test | Coverage |
|------|----------|
| Objective name matching format `"Zone (col,row)"` validated | Already covered by ZoneQuestBuilder tests (existing) |
| Fallback to NearestToBot when no grid match | Integration test (manual) |
| Factory integration | In-game validation via debug overlay |

#### File Summary

| File | Action | Lines |
|------|--------|-------|
| `ZoneMovement/Integration/ZoneObjectiveCycler.cs` | NEW | ~55 |
| `Controllers/BotJobAssignmentFactory.cs` | MOD | +15 |

---

### 8.3 Phase 4C: Enhanced 2D Debug Minimap (#25)

**Goal**: Add a 2D minimap overlay that visualizes grid cells, field vectors, bot positions, and zone sources — providing real-time feedback on the zone movement system's behavior.

**Research basis**: Phobos's `Diag/ZoneTelemetry.cs` uses OnGUI (not GL/Gizmos) to render an 800px 2D minimap with cell coloring, advection/convergence arrows, agent dots, zone dots, grid lines, and a legend. This approach is simpler and more informative than 3D world-space drawing. We adopt the same pattern.

#### Architecture

```
ZoneDebugOverlay.OnGUI()
  ├── [existing] Text panel (grid stats, player cell, POI breakdown)
  └── [NEW] RenderMinimap()
        ├── Draw cells (colored by dominant POI category)
        ├── Draw advection vectors (white arrows per cell)
        ├── Draw convergence vectors (red arrows per cell)
        ├── Draw zone source dots (blue)
        ├── Draw bot position dots (cyan)
        ├── Draw player position dot (white)
        ├── Draw grid lines
        └── Draw legend
```

#### New File: `ZoneMovement/Diag/DebugDrawing.cs` (~65 lines)

Static helper class (mirrors Phobos's `Diag/DebugUI.cs`):

```csharp
public static class DebugDrawing
{
    /// Draws a line using GUIUtility.RotateAroundPivot (Phobos pattern).
    public static void DrawLine(Vector2 start, Vector2 end, float thickness)

    /// Draws a line with a custom color.
    public static void DrawLine(Vector2 start, Vector2 end, float thickness, Color color)

    /// Draws a filled rectangle.
    public static void DrawFilledRect(Rect rect, Color color)

    /// Draws a rectangle outline.
    public static void DrawRectOutline(Rect rect, Color color, float thickness)

    /// Draws a dot (small filled square).
    public static void DrawDot(Vector2 center, float radius, Color color)

    /// Draws centered text.
    public static Rect Label(Vector2 position, string text, bool centered = true)
}
```

#### Modified File: `ZoneDebugOverlay.cs` (major expansion, +200 lines)

Key additions:
- `RenderMinimap()` method (~120 lines) — mirrors Phobos's `RenderGrid()`
- Cell color map: `PoiCategory` → `Color` dictionary
- Grid-to-screen coordinate mapping (world min/max → display rect)
- Per-cell advection/convergence vector access from WorldGridManager
- Bot/player position overlay using cached lists from WorldGridManager
- Legend panel

Cell color scheme:

| POI Category | Color | Hex |
|-------------|-------|-----|
| Container | Gold | `(0.9, 0.75, 0.2, 0.7)` |
| LooseLoot | Orange | `(0.9, 0.5, 0.1, 0.7)` |
| Quest | Green | `(0.2, 0.8, 0.3, 0.7)` |
| Exfil | Red | `(0.8, 0.2, 0.2, 0.7)` |
| SpawnPoint | Blue | `(0.3, 0.4, 0.8, 0.7)` |
| Synthetic | Gray | `(0.3, 0.3, 0.3, 0.7)` |
| Non-navigable | Black | `(0.1, 0.1, 0.1, 0.8)` |

#### Modified File: `WorldGridManager.cs` (+15 lines)

Expose field data for visualization:

```csharp
/// Advection field instance (for debug visualization).
public AdvectionField Advection => advectionField;

/// Convergence field instance (for debug visualization).
public ConvergenceField Convergence => convergenceField;

/// Cached human player positions from last update.
public IReadOnlyList<Vector3> CachedPlayerPositions => cachedPlayerPositions;

/// Cached bot positions from last update.
public IReadOnlyList<Vector3> CachedBotPositions => cachedBotPositions;

/// Zone source positions discovered during initialization.
public IReadOnlyList<(Vector3 position, float strength)> ZoneSources => zoneSources;
```

(Also requires storing `zoneSources` during `Awake()`.)

#### Modified File: `QuestingBotsPluginConfig.cs` (+5 lines)

```csharp
public static ConfigEntry<bool> ZoneMovementDebugMinimap { get; private set; }
// In Init(): ZoneMovementDebugMinimap = config.Bind("Zone Movement", "Debug Minimap", false, ...);
```

#### Test Strategy

Debug visualization is purely visual — no unit tests. Verified by:
1. F12 toggle → minimap appears/disappears
2. Visual inspection: cells colored correctly, vectors point expected directions
3. Player/bot dots track correctly in real-time
4. Grid lines align with cell boundaries

#### File Summary

| File | Action | Lines |
|------|--------|-------|
| `ZoneMovement/Diag/DebugDrawing.cs` | NEW | ~65 |
| `ZoneMovement/Integration/ZoneDebugOverlay.cs` | MOD | +200 |
| `ZoneMovement/Integration/WorldGridManager.cs` | MOD | +15 |
| `Configuration/QuestingBotsPluginConfig.cs` | MOD | +5 |

---

## 9. Risk Assessment

### Phases 1–3 (existing)

| Risk | Mitigation |
|------|-----------|
| Auto-detected bounds miss map edges | Padding parameter (default 50m) + fallback to large area |
| Too few/many cells on unusual maps | Configurable `target_cell_count` + log cell count on grid creation |
| Bots cluster despite crowd repulsion | Tunable `crowd_repulsion_strength` + inverse-square falloff |
| NavMesh gaps in grid cells | Synthetic POI creation validates with `NavMesh.SamplePosition()` |
| Performance with many bots | Fields computed per-bot but use simple math; convergence cached for 30s |
| New maps added to SPT | Zero config needed — auto-detection handles any map geometry |

### Phase 4 (new)

| Risk | Mitigation |
|------|-----------|
| Zone objective name matching fragile | Names follow strict format `"Zone (col,row)"` set by ZoneQuestBuilder. Add constant. |
| Per-bot state dictionary grows unbounded | BotFieldState is ~20 bytes. Dictionary keyed by profile ID. Cleared on raid end via `WorldGridManager.OnDestroy()`. |
| OnGUI minimap performance with 150+ cells | Redraw only on convergence update interval (30s) using cached texture. Or: only draw when overlay enabled. |
| Factory integration changes break quest selection | Minimal change (~15 lines). Zone quest branch is isolated with null-check fallback to default NearestToBot. |
| Noise determinism causes visible patterns | Time-bucket rotation (5s) + XOR with seed ensures variation. Tunable via NoiseWeight config. |
| GetRecommendedDestination API backward compat | New overload `(string botId, Vector3 pos)` alongside existing `(Vector3 pos, float momX, float momZ)`. |

---

## 10. Success Criteria

### Phases 1–3

1. Bots move dynamically around the map toward interesting areas
2. Bots converge toward (but don't swarm) the human player
3. Movement feels natural — no zigzagging, no clustering
4. Works on all maps without per-map configuration
5. All pure-logic tests pass
6. No measurable performance regression vs current quest system

### Phase 4

7. Each bot follows a unique path (no herd movement) due to per-bot momentum + noise
8. Bots cycle through destinations dynamically based on live convergence/advection fields
9. Debug minimap accurately visualizes grid state, field vectors, and bot positions
10. Zone objective cycling integrates cleanly with the existing quest factory (zero changes to BotObjectiveManager/BotObjectiveLayer)
11. At least 12 new unit tests pass for BotFieldState and ComputeMomentum
