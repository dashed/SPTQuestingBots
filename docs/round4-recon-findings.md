# Round 4 Reverse Engineering Findings

Fourth-round analysis covering interactive objects (doors/switches/keys), vision/perception system, and the navigation graph (AICorePoints, patrol routes, voxel grid).

---

## Interactive Objects

### Door Interaction Flow (BotDoorOpener)
1. **Detection**: Every 0.05s, scans NearDoorData for links within 10.4m
2. **Path intersection**: Tests if current path crosses door plane (line segment intersection)
3. **Approach**: Calculates interaction position (0.3m), rolls breach chance, enters NearDoor mover state
4. **Interaction**: 3s cooldown, sprint pause 4s, movement pause. Rolls breach success.
5. **Post-open room clearing**: Walk 0.75m past door → pause → raycast LEFT/RIGHT 30m → look at clear sightlines 1.2s each

### Key Constants
| Constant | Value |
|----------|-------|
| Interact distance | 0.3m |
| Open detection range | 4m |
| Sprint pause range | 5.2m |
| Sprint pause duration | 4s |
| Forced-open period | 15s |
| Interaction cooldown | 3s |

### Door Sound Events
`DoorState` setter fires `BotEventHandler.InteractObject(Id, position)` on every open — all bots in range hear door sounds.

### Switch Chain System
Single switch can cascade: Switch → Delay → NextSwitches[] → Door unlock/open → ExfiltrationPoint activation → Lamp enable. Our ToggleSwitchAction doesn't account for chains.

### NavMeshDoorLink
Three carvers (Opened/Breached/Closed) with delayed activation — waits until nearest bot >2m before activating to prevent NavMesh invalidation during traversal. Path cost set to 9999 when blocked.

### Actionable Items
1. **Door proximity sprint pause** — BSG pauses 4s within 5.2m. Our sprint limiting doesn't consider doors.
2. **15s forced-open timer awareness** — prevent bots re-closing doors just opened by squadmates
3. **Door sound detection** — react to `BotEventHandler.InteractObject` in combat awareness
4. **Breach chance from bot settings** — use `BREACH_CHANCE_100` from `FileSettings.Move` instead of fixed logic
5. **Post-open room clearing parameters** — BSG uses 0.75m walk-through, 30m raycast, 1.2s look. Tune our implementation.

---

## Vision & Perception

### Progressive Visibility Accumulation
NOT binary see/don't-see. BSG uses a **float visibility level** (0.0→1.0) that accumulates over time:
```
VisibilityLevel += dt × VISIBILITY_CHANGE_SPEED × visibilityChangeSpeedK
```
Enemy becomes Visible when level reaches 1.0. 8 multiplicative factors control detection speed.

### The 8 Detection Speed Factors
1. **Distance**: farther = slower detection
2. **FlarePower**: recently fired = dramatically easier to detect (muzzle flash)
3. **PoseVisibility**: prone = hardest, standing = easiest
4. **RuntimeVisionEffects**: per-difficulty multiplier
5. **Repeat-see bonus**: re-detecting recently-seen enemy is faster
6. **Angle coefficient**: edge-of-vision = slower detection
7. **Foliage coefficient**: inside bush = harder to detect
8. **Weather**: rain + fog reduce detection speed

### Vision Cone
- Normal FOV: 220° (cos(110°) threshold)
- NVG FOV: 120° (cos(60°))
- Flashlight FOV: 120° (cos(60°))
- `FULL_SECTOR_VIEW`: 360° for certain bosses

### Body Part Raycast Priority
- High priority (close): head + arms + legs + body
- Medium (mid-range): head + arms
- Low (far): body only
- Distance thresholds from `FAR_DISTANCE` and `MIDDLE_DIST` settings

### Grass/Foliage Handling
- `NO_GREEN_DIST`: within this, grass is transparent
- `NO_GRASS_DIST`: within this, short grass is transparent
- Obstacle penetration formula: `(1 - MAX_VISION_GRASS_METERS × depth) / (1 + depth × 1.5)`
- Recently hit bots see through grass for `LOOK_THROUGH_PERIOD_BY_HIT` seconds

### LookSensor Throttling
- Default: 0.1s per bot, scales with bot count
- With 60 bots: 0.6s per bot (capped at MaxUpdatePeriod)
- Round-robin scheduling ensures time-correct visibility accumulation

### Actionable Items
6. **Exploit pose visibility** — bots in ambush/snipe should use prone/crouch for concealment
7. **Read VisibleDist** — bot's current effective vision range (weather+time adjusted) for scoring
8. **FlarePower awareness** — post-fire sprint cooldown is validated by game mechanics
9. **NVG enemy awareness** — check if threatening enemy has NVG for night stealth
10. **VisibilityLevel gradient** — read enemy detection progress (0-1) instead of binary IsVisible

---

## Navigation Graph

### AICorePoints — Connectivity Backbone
- Scene objects forming a graph overlay on NavMesh
- Each has `ConnectionGroupId` — bots can ONLY pathfind between same-group points
- `AICorePointHolder.GetClosest(pos)` for nearest point lookup
- Critical for reachability validation

### PatrolWay / PatrolPoint — Rich Route Data
Our `NativePatrolDataProvider` extracts positions only. Missing data:
- `ShallSit` — crouch at waypoint
- `PatrolPointType` — checkPoint (walk through) vs stayPoint (pause and look)
- `PointWithLookSides.Directions` — directional scan vectors
- `ActionData` — reserved way with GoTo + LookShootTo positions
- Sub-points — 4-25 micro-positions per waypoint at 1.7/2.5/3.4m radii
- `CanUseByBoss` flag

### Voxel Grid (NavGraphVoxelSimple)
- Cell size: 10m × 10m × 5m
- Each voxel contains: cover points, door links, loot points, exfil points, bots
- `AIVoxelesData.MinVoxelesValues/MaxVoxelesValues` = **exact map bounds**
- Access: `BotsController.CoversData.Voxels`

### Cover Point Graph
- Pre-baked GroupPoints with wall direction, lean flags, defense level, indoor/outdoor
- A* search via `GClass380`/`GClass381` over NeighbourhoodsWays graph edges
- ConnectionGroup filtering ensures reachability
- Quality scoring: 8-direction raycast, CoverLevel (Stay/Sit/Lay), Special flags

### BotLocationModifier (24 per-zone parameters)
AccuracySpeed, Scattering, GainSight, VisibleDistance, DistToSleep, DistToActivate, LeaveDist, spawn throttling, weather modifiers — per-zone tuning we could use.

### AIPlaceInfo (room/area data)
`IsInside`, `IsDark`, `IsMute`, `BlockGrenade`, `GrenadePlaces`, `AreaId` — rich indoor environment data.

### Actionable Items
11. **Use voxel grid for map bounds** — `MinVoxelesValues/MaxVoxelesValues` is authoritative (replaces spawn-point inference)
12. **Use native cover graph** — query `CoversData.Points` with ConnectionGroup filter instead of NavMesh sampling
13. **Extract richer patrol data** — ShallSit, PatrolPointType, look directions, sub-points
14. **Use BotLocationModifier** — per-zone accuracy, visibility, sleep distance for utility scoring
15. **ConnectionGroup validation** — verify movement targets are reachable before assigning
16. **Use AIPlaceInfo** — IsInside, IsDark for improved room clearing and environment detection
17. **Use AILootPointsCluster** — pre-clustered loot with value scores, better than manual scanning
18. **Access BotZone directly** — `FindObjectsOfType<BotZone>()` for PatrolWays[], Modifier, flags

---

## Access Patterns

| Data | Access Path |
|------|-------------|
| Map bounds | `BotsController.CoversData.Voxels.MinVoxelesValues/MaxVoxelesValues` |
| Cover points | `BotsController.CoversData.Points` |
| Voxel grid | `BotsController.CoversData.Voxels.VoxelesArray[x,y,z]` |
| Core points | `BotsController.CoversData.AICorePointsHolder.CorePoints` |
| Loot clusters | `BotsController.CoversData.Patrols.LootPointClusters` |
| Room/area info | `BotsController.CoversData.AIPlaceInfoHolder.Places` |
| Zone entrances | `BotsController.CoversData.EntranceInfo.EntranceList` |
| Bot zones | `FindObjectsOfType<BotZone>()` |
| Vision range | `BotOwner.LookSensor.VisibleDist` |
| Detection level | `EnemyInfo.VisibilityLevel` |
| Muzzle flash | `IAIData.FlarePower` |

## Combined Priority List

### High Priority
1. Use voxel grid MinMax for authoritative map bounds
2. Use native cover graph with ConnectionGroup filtering
3. Extract richer patrol data (look directions, sit flags, sub-points)
4. Door proximity sprint pause (BSG: 4s within 5.2m)
5. Exploit pose visibility for ambush/snipe concealment

### Medium Priority
6. BotLocationModifier per-zone behavior tuning
7. ConnectionGroup validation for movement targets
8. Door sound detection via BotEventHandler.InteractObject
9. Read VisibleDist for scoring (weather+time adjusted range)
10. Use AIPlaceInfo IsInside/IsDark for room clearing
11. 15s forced-open timer awareness for squad door coordination
12. AILootPointsCluster for pre-valued loot zone selection

### Lower Priority
13. FlarePower awareness in post-fire behavior
14. NVG enemy awareness for night stealth
15. VisibilityLevel gradient instead of binary IsVisible
16. Breach chance from bot settings
17. Switch chain awareness for cascading effects
18. BotZoneEntrance for building entry points
