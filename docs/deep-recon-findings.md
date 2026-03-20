# Deep Reverse Engineering Findings

Second-round analysis of Assembly-CSharp.dll (SPT 4.0.13) using decompile, xref, and inspect commands. Focused on movement internals, AI brain architecture, and spawning pipeline.

---

## Movement System

### Architecture
- **BotMover** (abstract) → **GClass494** (concrete) — 50+ fields, core movement pipeline
- Update loop: check paused → re-enable sprint → get corner → compute direction → `Player.Move()`
- Sprint uses animation curve for acceleration ramp; deceleration near target via `remainingDist / SLOW_COEF`

### Shadow Position System (PositionOnWay)
BSG maintains a separate "PositionOnWay" position on the NavMesh, distinct from the player's physics position. GClass494 advances this along path corners each frame via `MovementContext.OnMotionApplied`. Used by:
- Local avoidance (GClass492) — bot-to-bot collision
- NavMesh voxel tracking
- Door proximity detection
- Distance calculations (SDistDestination)

**BUG**: Our custom mover doesn't update PositionOnWay during active periods — stale data breaks local avoidance and door detection.

### Inertia System (GClass497)
BSG has angular momentum for path following:
- Tracks accumulated angular deviation (±85deg clamp)
- Cosine-scaled forward motion reduction during turns
- Perpendicular drift proportional to turn angle
- MAX_INERTION = 0.3m offset cap

Our CustomPathFollower has a simpler "path-deviation spring" without angular momentum.

### Local Avoidance (GClass492)
Bot-to-bot collision avoidance: checks all bots in same NavMesh voxel cell, pushes apart perpendicular to movement if within 2.1m. Max offset 0.3m. Uses PositionOnWay (stale when our mover is active).

### NavMesh Patterns
- `NavMesh.SamplePosition`: 66 callers, tolerances 0.1f-100f
- `NavMesh.CalculatePath`: 63 callers, always AllAreas (-1)
- **`NavMesh.Raycast`: 0 callers** — our corner cutting is a unique optimization BSG doesn't use

### Key APIs Not Used
- `BotMover.TryReplacePathAround(centerAvoid, subAvoids)` — computes left/right detour paths around threat points
- `BotMover.ComputePathLengthToPoint(Vector3)` — actual NavMesh path length (better than straight-line)
- `MovementContext.SpeedLimiter` — centralized speed limiting system
- `MovementContext.CollisionFlags` — physics collision detection

### Door System (BotDoorOpener — 33 fields)
Full door interaction: NavMeshDoorLink with open/close segments, breach mechanics, post-opening room sweep (look left/right 10m), sprint pause 4s within 5.2m, speed reduction to 0.5f near doors.

### Key Constants
| Constant | Value |
|----------|-------|
| MaxSprintSpeed | 2.0 |
| SlowEndDist | 0.5 |
| MAX_DIST_OUT_OF_NAVMESH | 7.0 |
| MAX_INERTION | 0.3 |
| LocalAvoidance range | 2.1m |
| Door sprint pause dist | 5.2m |
| Door sprint pause duration | 4.0s |

---

## AI Brain System

### Architecture
Layered strategy pattern: `AICoreStrategyAbstractClass` iterates `List_0` (sorted by priority descending), first layer where `ShallUseNow()=true` wins. BigBrain patches this to inject our custom layers.

### BotLogicDecision — 94 values
70 real decisions + 24 debug. Key mappings to our utility tasks:

| BSG Decision | Our Equivalent |
|---|---|
| `simplePatrol` / `followerPatrol` | PatrolTask |
| `deadBody` | VultureTask |
| `botTakeItem` / `botDropItem` | LootTask |
| `search` | InvestigateTask |
| `holdPosition` | LingerTask / AmbushTask |
| `goToPoint` | GoToObjectiveTask |
| `moveStealthy` | RoomClearTask analogue |
| `plantMine` | PlantItemTask |

### Default Brain Layer Stack (Assault — GClass355)
Priority descending (first `ShallUseNow()=true` wins):

| Priority | Name | Condition |
|----------|------|-----------|
| 99 | **SleepingLayer** (ours) | Bot sleeping |
| 80 | AvoidDanger | Grenade/BTR/artillery |
| 78 | Malfunction | Weapon jam |
| 70 | CalledForHelp | Group member help request |
| 65 | FightReq | Boss/group combat request |
| 59 | AssaultEnemyFar | Enemy exists but far |
| 58 | PushAndSuppress | Mine awareness (hard only) |
| 55 | **AssaultHaveEnemy** | Main combat (`GoalEnemy != null`) |
| 30 | PeaceReq | Peace requests (door, go to point) |
| 26 | **RegroupLayer** (ours) | Follower regrouping |
| 25 | Pursuit | Enemy pursuit |
| 20 | Simple Target | Has target but no enemy |
| 19 | **FollowerLayer** (ours) | Following boss |
| 18 | **QuestingLayer** (ours) | Questing/utility AI |
| 10 | Leave Map | Bot extraction |
| 8 | StandBy | Standing by |
| 6 | **Utility peace** | Dead body work, item take/drop |
| 4 | LootPatrol | Loot during patrol |
| 2 | **PatrolAssault** | Always-true fallback |

Our Questing layer (18) correctly sits below all combat/danger layers and above extraction/patrol.

### Potential Layer Conflicts
- **BSG "Utility peace" (priority 6)**: Handles deadBody, botTakeItem — overlaps with our LootTask/VultureTask. When our Questing layer returns `IsActive()=false`, BSG's looting kicks in with different logic.
- **BSG "LootPatrol" (priority 4)**: BSG's own loot-during-patrol, could conflict with our Hybrid Looting when our layer isn't active.
- **BSG "PatrolAssault" (priority 2)**: Always-true fallback. When our layer is inactive, bots fall back to BSG patrol with eating, drinking, weapon checking.

### Enemy Memory System
`EnemyInfo` tracks per-enemy state: `IsVisible`, `CanShoot`, `Distance`, `PersonalLastSeenTime`, `PersonalLastPos`, `EnemyLastPosition` (group-shared). Enemy selection uses weighted scoring (distance + visibility + shootability).

### Bot Hearing (BotHearingSensor)
Subscribes to `BotEventHandler.OnSoundPlayed`, calculates hearing distance, adds sound-based `PlaceForCheck` to `BotsGroup.PlacesForCheck` with positional precision.

### Bot Difficulty Settings (134 Mind fields)
Key settings relevant to our systems: `TIME_TO_FORGOR_ABOUT_ENEMY_SEC`, `AMBUSH_WHEN_UNDER_FIRE`, `HOW_WORK_OVER_DEAD_BODY`, `DEADBODYWORK_*`, `CAN_STAND_BY`, `KEEP_ZONE_ON_SPAWN_TIME_SEC`, `DOG_FIGHT_IN/OUT`.

---

## Spawning Pipeline

### Full Pipeline
```
LocalGame.Init()
  → BotsController.SetSettings()
  → BotsController.Init()
    → BotSpawner = new GClass1890()
      → SpawnDelaysService (3s polling loop)
      → BossSpawner
    → SpawnControlScenario (wave respawn tracker)
```

### Two Spawn Scenarios
- **WavesSpawnScenario** (`OldSpawn`): Timer-based wave spawning with MinMaxBots enforcement
- **NonWavesSpawnScenario** (`NewSpawn`): Continuous spawning with on/off phases, per-player limits, chance-based group formation

### Our Spawning vs Game's Spawning
Our PMC/PScav spawning completely bypasses the game's wave system:

| Aspect | Game's System | Our System |
|--------|--------------|------------|
| Zone selection | Per-zone capacity, player proximity | Furthest-from-players |
| Spawn points | ISpawnSystem.SelectAISpawnPoints | LocationData.TryGetFurthest |
| Bot counting | AllBotsCount in BotSpawner | BotDiedPatch skips decrement |
| Groups | ShallBeGroupParams with tracking | BotGroupHelpers.CreateGroup (locked) |
| Wave integration | WavesSpawnScenario timers | Coroutine with RaidETRange |
| Delay/retry | SpawnDelaysService (3s poll) | retrySpawnTimer |

### Key GClass Mappings
| GClass | Purpose |
|--------|---------|
| GClass1890 | BotSpawner concrete implementation |
| GClass1885 | SpawnDelaysService — delayed spawn queue |
| GClass1876 | NonWaveGroupScenario — group formation |
| GClass678 | SpawnControlScenario — wave respawn |
| GClass684 | Profile backup system |
| GClass620 | Global bot settings singleton |

---

## Actionable Items — Prioritized

### Critical (Real Bugs)

1. **Sync PositionOnWay during custom movement** — Local avoidance and door detection break when our custom mover is active. Fix: set `mover.PositionOnWayInner = botPosition` in our Tick().

### High Priority (Significant Improvements)

2. **Add inertia to custom path follower** — BSG's GClass497 angular momentum makes turns natural. Algorithm: track turn angle, cos(angle) forward scaling, perpendicular offset.
3. **Use PlacesForCheck for investigation targets** — `BotOwner.BotsGroup.PlacesForCheck` gives sound-derived positions with precision. Better than our generic "suspicious" state.
4. **Use TryReplacePathAround for threat avoidance** — BSG's API computes detour paths around dangerous positions.

### Medium Priority (Nice-to-Haves)

5. **Read BSG Mind settings for personality calibration** — 134 per-difficulty settings could modulate our personality system.
6. **Use ComputePathLengthToPoint** — NavMesh path length for better distance estimates in quest scoring.
7. **Check zone capacity before spawning** — `BotZone.HaveFreeSpace()` prevents over-crowding.
8. **Integrate with SpawnDelaysService** — account for `WaitCount` when calculating spawn capacity.
9. **Use ISpawnSystem.SelectAISpawnPoints** — game's spawn point system has validation and fallback modes.
10. **Read EnemyInfo for task scoring** — `PersonalLastPos` and `TimeLastSeen` give more precise combat state than our binary IsPostCombat.

### Low Priority (Future Considerations)

11. **Use CollisionFlags for stuck detection** — sustained `Blocked` state → faster escalation.
12. **Integrate with BotSpawnLimiter** — call `IncreaseUsedPlayerSpawns()` for game awareness.
13. **Profile caching via BotsPresets backup** — pre-warm profiles to reduce generation latency.
14. **Coordinate with NonWaves on/off phases** — time our spawns to not conflict with game burst spawning.
