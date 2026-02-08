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

### 5.7 Future Work

- Enhanced debug visualization (3D grid cell outlines, field vector arrows, POI markers using GL drawing)
- Integration of `GetRecommendedDestination` into bot objective lifecycle for truly dynamic destination cycling
- Per-bot field computation (unique momentum vectors per bot)

---

## 6. Test Strategy

### 6.1 Unit Tests — 143 client tests total

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

### 6.2 Integration verification — compile-verified

Scene integration classes (`PoiScanner`, `ZoneDiscovery`, `WorldGridManager`, `ZoneQuestBuilder`, `ZoneDebugOverlay`) are thin adapters. Correctness verified by:
1. Pure logic tests (Phases 1-2 cover all computation)
2. In-game debug overlay (`ZoneDebugOverlay` — F12 toggle, shows grid/POI stats)
3. Manual testing on 2-3 maps

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

### Future Work

| # | Task | Est. Size |
|---|------|-----------|
| 23 | Enhanced 3D debug visualization (GL grid outlines, field vector arrows) | M |
| 24 | Integrate `GetRecommendedDestination` into bot objective lifecycle | M |
| 25 | Per-bot field computation with individual momentum vectors | S |

---

## 8. Risk Assessment

| Risk | Mitigation |
|------|-----------|
| Auto-detected bounds miss map edges | Padding parameter (default 50m) + fallback to large area |
| Too few/many cells on unusual maps | Configurable `target_cell_count` + log cell count on grid creation |
| Bots cluster despite crowd repulsion | Tunable `crowd_repulsion_strength` + inverse-square falloff |
| NavMesh gaps in grid cells | Synthetic POI creation validates with `NavMesh.SamplePosition()` |
| Performance with many bots | Fields computed per-bot but use simple math; convergence cached for 30s |
| New maps added to SPT | Zero config needed — auto-detection handles any map geometry |

---

## 9. Success Criteria

1. Bots move dynamically around the map toward interesting areas
2. Bots converge toward (but don't swarm) the human player
3. Movement feels natural — no zigzagging, no clustering
4. Works on all maps without per-map configuration
5. All pure-logic tests pass
6. No measurable performance regression vs current quest system
