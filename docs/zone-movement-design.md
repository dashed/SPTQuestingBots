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
| Actions | MoveToPosition, Ambush, Snipe, PlantItem | MoveToPosition + HoldAtPosition (simpler) |
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
[GridCell] → ZoneObjectiveProvider → BotObjectiveManager → GoToPositionAbstractAction
```

### Integration Strategy

The zone movement system is a **fallback objective source**. Existing quest-based objectives take priority if available. This means:

1. `BotObjectiveManager` checks quest objectives first (existing behavior)
2. If no quest available → query `ZoneObjectiveProvider` for a zone-based destination
3. `ZoneObjectiveProvider` calls `DestinationSelector` → returns a `GridCell` → provides `Position` to the bot
4. Bot uses existing `GoToPositionAbstractAction` to move (KEEP existing execution layer)
5. When bot reaches destination → hold for configurable duration → request next destination

This is non-destructive: it doesn't break any existing functionality.

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

## 4. Phase 2: Scene Integration Layer

These classes bridge pure logic to the game world. They depend on Unity/EFT types and cannot be unit tested, but the logic they contain is thin.

### 4.1 Directory Structure

```
src/SPTQuestingBots.Client/ZoneMovement/Integration/
├── MapBoundsDetector.cs
├── PoiScanner.cs
├── ZoneDiscovery.cs
├── WorldGridManager.cs
└── ZoneObjectiveProvider.cs
```

### 4.2 Classes

#### `MapBoundsDetector`
Auto-detect map bounds — **no per-map config needed**.

```csharp
public static (Vector3 min, Vector3 max) DetectBounds(SpawnPointParams[] spawnPoints, float padding = 50f)
{
    // Find min/max X and Z across all spawn points
    // Add padding to ensure bots don't get stuck at edges
    // Y is ignored (2D grid on XZ plane)
}
```

**Fallback**: If no spawn points (shouldn't happen), use `LocationScene` bounds or hardcoded large area.

#### `PoiScanner`
Scan Unity scene for points of interest.

```csharp
public static List<PointOfInterest> ScanScene()
{
    // FindObjectsOfType<LootableContainer>() → PoiCategory.Container
    // FindObjectsOfType<TriggerWithId>() → PoiCategory.Quest
    // FindObjectsOfType<ExfiltrationPoint>() → PoiCategory.Exfil
    // FindObjectsOfType<SpawnPointMarker>() → PoiCategory.SpawnPoint
    // Validate each position with NavMesh.SamplePosition()
}
```

#### `ZoneDiscovery`
Find BSG BotZone objects as advection sources.

```csharp
public static List<(Vector3 position, float strength)> DiscoverZones()
{
    // FindObjectsOfType<BotZone>() → extract patrol points
    // Each patrol point becomes an advection source
    // Strength proportional to number of patrol waypoints in zone
}
```

#### `WorldGridManager` (MonoBehaviour)
Orchestrator that creates the grid on raid start and manages field updates.

- **Awake**: Detect bounds → Create `WorldGrid` → Scan POIs → Discover zones → Build `AdvectionField`
- **Update**: Every 30s, update `ConvergenceField` with current human player positions
- **API**: `GridCell GetCellForBot(Vector3 position)`, `Vector2 GetCompositeDirection(Vector3 position, Vector3 momentum, IReadOnlyList<Vector3> botPositions, IReadOnlyList<Vector3> playerPositions)`
- **Synthetic fill**: For grid cells with zero POIs, sample NavMesh at cell center to create synthetic POIs

#### `ZoneObjectiveProvider`
Adapter between the zone system and `BotObjectiveManager`.

```csharp
public class ZoneObjectiveProvider
{
    // Returns a BotJobAssignment-compatible position for a bot
    public Vector3? GetNextDestination(BotOwner bot, WorldGridManager gridManager)
    {
        var currentCell = gridManager.GetCellForBot(bot.Position);
        var momentum = (bot.Position - bot.PreviousPosition).normalized;  // XZ only
        var composite = gridManager.GetCompositeDirection(bot.Position, momentum, ...);
        var targetCell = destinationSelector.SelectDestination(grid, currentCell, composite, bot.Position);
        return targetCell?.Center;  // NavMesh-validated position
    }
}
```

---

## 5. Phase 3: Integration with Existing QuestingBots

### 5.1 Changes to Existing Files

#### `LocationData.cs` (Awake)
Add after existing initialization:
```csharp
// Initialize zone movement system (works regardless of questing or spawning config)
Singleton<GameWorld>.Instance.gameObject.GetOrAddComponent<WorldGridManager>();
```

#### `BotObjectiveManager.cs`
Modify `GetNewBotJobAssignment()` fallback path in `BotJobAssignmentFactory.cs`:
```csharp
// After existing quest assignment logic fails to find a quest...
// Fallback: zone-based movement
var gridManager = Singleton<GameWorld>.Instance.GetComponent<WorldGridManager>();
if (gridManager != null)
{
    var destination = zoneObjectiveProvider.GetNextDestination(bot, gridManager);
    if (destination.HasValue)
    {
        return CreateZoneMovementAssignment(bot, destination.Value);
    }
}
```

#### `config/config.json`
Add new section:
```json
{
    "zone_movement": {
        "enabled": true,
        "target_cell_count": 150,
        "convergence_update_interval_sec": 30,
        "convergence_weight": 1.0,
        "advection_weight": 0.5,
        "momentum_weight": 0.5,
        "noise_weight": 0.3,
        "hold_duration_min_sec": 10,
        "hold_duration_max_sec": 60,
        "poi_score_weight": 0.3,
        "crowd_repulsion_strength": 2.0,
        "bounds_padding": 50
    }
}
```

#### `QuestingBotsPluginConfig.cs`
Add F12 menu entries for zone movement toggle and debug visualization.

### 5.2 New Config Model

```csharp
public class ZoneMovementConfig
{
    [JsonProperty("enabled")] public bool Enabled { get; set; } = true;
    [JsonProperty("target_cell_count")] public int TargetCellCount { get; set; } = 150;
    [JsonProperty("convergence_update_interval_sec")] public float ConvergenceUpdateIntervalSec { get; set; } = 30f;
    [JsonProperty("convergence_weight")] public float ConvergenceWeight { get; set; } = 1.0f;
    [JsonProperty("advection_weight")] public float AdvectionWeight { get; set; } = 0.5f;
    [JsonProperty("momentum_weight")] public float MomentumWeight { get; set; } = 0.5f;
    [JsonProperty("noise_weight")] public float NoiseWeight { get; set; } = 0.3f;
    [JsonProperty("hold_duration_min_sec")] public float HoldDurationMinSec { get; set; } = 10f;
    [JsonProperty("hold_duration_max_sec")] public float HoldDurationMaxSec { get; set; } = 60f;
    [JsonProperty("poi_score_weight")] public float PoiScoreWeight { get; set; } = 0.3f;
    [JsonProperty("crowd_repulsion_strength")] public float CrowdRepulsionStrength { get; set; } = 2.0f;
    [JsonProperty("bounds_padding")] public float BoundsPadding { get; set; } = 50f;
}
```

---

## 6. Test Strategy

### 6.1 Unit Tests (Phase 1) — ~30-40 tests

All use the existing `Vector3` test shim (with `sqrMagnitude`, `magnitude`, `operator-`).

| Test Class | Coverage |
|------------|----------|
| `WorldGridTests` | Grid creation, cell count, cell lookup by position, out-of-bounds handling, neighbor enumeration, POI insertion |
| `GridCellTests` | POI management, density calculation, navigability |
| `AdvectionFieldTests` | Single zone attraction, multiple zones, crowd repulsion inverse-square, zero-distance handling, normalization |
| `ConvergenceFieldTests` | Single player attraction, multiple players, sqrt falloff, update interval gating, stale cache |
| `FieldComposerTests` | Weight application, all-zero input, single dominant field, noise rotation, normalization |
| `DestinationSelectorTests` | Best-angle neighbor, no navigable neighbors, edge cells, POI density bonus |
| `CellScorerTests` | Perfect alignment score, opposite direction penalty, POI density bonus, configurable weight |

### 6.2 Integration Tests (Phase 2-3) — compile-verified only

Scene integration classes are thin adapters. Correctness verified by:
1. Pure logic tests (Phase 1 covers all computation)
2. In-game visual debugging (draw grid, POIs, field vectors)
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

### Phase 2: Scene Integration (~5 classes)

| # | Task | Depends On | Est. Size |
|---|------|-----------|-----------|
| 10 | Create `MapBoundsDetector` | 3 | S |
| 11 | Create `PoiScanner` | 1 | S |
| 12 | Create `ZoneDiscovery` | 4 | S |
| 13 | Create `WorldGridManager` (MonoBehaviour orchestrator) | 3, 10, 11, 12 | M |
| 14 | Create `ZoneObjectiveProvider` (BotObjectiveManager adapter) | 8, 13 | M |

### Phase 3: Full Integration

| # | Task | Depends On | Est. Size |
|---|------|-----------|-----------|
| 15 | Add `ZoneMovementConfig` to config model + `config.json` | — | S |
| 16 | Integrate with `BotJobAssignmentFactory` as fallback | 14, 15 | M |
| 17 | Wire `WorldGridManager` into `LocationData.Awake()` | 13 | XS |
| 18 | Add F12 menu config entries | 15 | S |
| 19 | Add debug visualization (grid overlay, field vectors) | 13 | M |

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
