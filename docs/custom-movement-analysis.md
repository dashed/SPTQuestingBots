# Custom Movement System Analysis

> Deep investigation of custom movement adoption for SPTQuestingBots, informed by Phobos,
> SAIN, BSG API patterns, and the existing codebase. Includes ECS data-layout considerations.

**Version**: 1.7.0
**Date**: February 2026
**Status**: Implemented (Option A — Full Phobos-Style Replacement)

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Phobos Custom Movement Architecture](#2-phobos-custom-movement-architecture)
3. [QuestingBots Current Movement Architecture](#3-questingbots-current-movement-architecture)
4. [BSG Movement API Surface](#4-bsg-movement-api-surface)
5. [Other Mods: SAIN, BigBrain, Waypoints](#5-other-mods-sain-bigbrain-waypoints)
6. [Side-by-Side Comparison](#6-side-by-side-comparison)
7. [Pros and Cons of Adopting Custom Movement](#7-pros-and-cons-of-adopting-custom-movement)
8. [ECS Data-Layout Considerations](#8-ecs-data-layout-considerations)
9. [Implementation Options](#9-implementation-options)
10. [Recommended Implementation Plan](#10-recommended-implementation-plan)
11. [Risk Assessment](#11-risk-assessment)
12. [Testability Analysis](#12-testability-analysis)
13. [Conclusion and Recommendation](#13-conclusion-and-recommendation)

---

## 1. Executive Summary

QuestingBots previously delegated all movement execution to BSG's built-in `BotMover` via
`BotOwner.FollowPath()`. This approach was simple and compatible with other mods, but produced
jerky corner-to-corner navigation, limited sprint intelligence, and no path smoothing.

Phobos takes the opposite approach: it **completely replaces** BSG's `BotMover` with direct
`Player.Move()` calls, custom path following with a path-deviation spring force, and
sophisticated sprint gating based on path smoothness. The result is visibly smoother, more
human-like movement --- but at the cost of significant complexity and fragile BSG state
management during brain-layer transitions.

A key finding: **SAIN also bypasses BSG's mover** with `Player.Move()` and 10 Harmony
patches, making QuestingBots' use of `BotOwner.FollowPath()` the minority pattern. This
reduces the compatibility risk of adopting custom movement.

**Implemented: Option A (Full Phobos-Style Replacement)** --- the full custom movement
system has been built and is enabled by default (`use_custom_mover`, default: true).

| Phase | Feature | Status |
|-------|---------|--------|
| 1 | Sprint angle-jitter analysis (`SprintAngleJitter`) | Complete |
| 2 | Path pre-smoothing — Chaikin + spring force (`PathSmoother`, `PathDeviationForce`) | Complete |
| 3 | Movement state on ECS entities (`MovementState` struct on `BotEntity`) | Complete |
| 4 | Full `Player.Move()` override (`CustomPathFollower` + 3 BSG patches) | Complete |

All phases are implemented, tested (~60 new tests), and wired into the action system.
When `use_custom_mover` is false (default), QuestingBots continues using BSG's
`BotOwner.FollowPath()` — the custom mover is purely opt-in.

---

## 2. Phobos Custom Movement Architecture

Phobos (`/home/alberto/github/Phobos`) implements a ground-up custom movement system that
bypasses BSG's `BotMover` entirely. The relevant files are:

| File | Lines | Purpose |
|------|-------|---------|
| `Systems/MovementSystem.cs` | ~635 | Main movement loop (path following, sprint, doors) |
| `Components/Movement.cs` | ~60 | Per-agent movement state (target, path, sprint, pose) |
| `Components/Stuck.cs` | ~20 | Stuck detection state |
| `Entities/Agent.cs` | ~37 | Entity with Movement, Stuck, Look components |
| `Helpers/PathHelper.cs` | ~120 | Path math (forward point, deviation, distance) |
| `Helpers/VectorHelper.cs` | ~19 | XZ projection helpers |
| `Systems/LookSystem.cs` | ~70 | Look direction from path forward point |

### 2.1 MovementSystem Overview

`MovementSystem.Update(List<Agent> liveAgents)` runs once per frame for all active agents.
For each agent, it executes two phases:

1. **Path following** --- advance along corners using a corner-reaching epsilon
2. **Movement execution** --- compute move direction with spring force, call `Player.Move()`

Key design: Movement is a **static system method** that iterates a dense agent list (the
Phobos EntityArray pattern). No per-agent MonoBehaviour. No virtual dispatch.

### 2.2 Path Following Algorithm

Phobos tracks which path corner the bot is heading toward (`Movement.CurrentCorner` index).
When the bot reaches a corner within an epsilon distance, it advances to the next:

| State | Epsilon (m) | Behavior |
|-------|-------------|----------|
| Walking | 0.35 | Precise corner-to-corner |
| Sprinting | 0.60 | Wider radius to maintain momentum |

This is simpler than BSG's internal path following, which uses `BotCurrentPathAbstractClass`
with waypoint caching, steer targets, and complex door-wait logic.

### 2.3 Path-Deviation Spring Force

The most important Phobos innovation. When a bot moves between corners, it can drift off
the ideal path line (especially after vault/jump or collision). The spring force pulls it
back:

```
Algorithm (2D XZ plane):
1. Project bot position onto XZ plane
2. Find closest point on current path segment via dot-product
3. Compute deviation vector = closestPoint - botPosition (XZ only)
4. Blend: moveDirection = normalize(cornerDirection + deviationForce)
5. Apply as Player.Move(moveDirection)
```

Why XZ only: terrain height changes would cause vertical oscillation. The spring operates
purely in the horizontal plane, allowing Unity's character controller to handle Y-axis.

### 2.4 Sprint Decision Logic

Phobos gates sprinting on path smoothness, not just stamina:

```
AngleJitter = sum of absolute angle changes at upcoming N corners
CanSprint = (isOutdoor) AND (canPhysicallySprint) AND (angleJitter < threshold)
```

Thresholds vary by urgency level:

| Urgency | Max Angle Jitter |
|---------|-----------------|
| High | 45 degrees |
| Medium | 30 degrees |
| Low | 20 degrees |

This means bots slow down before sharp turns and sprint on straight paths --- much more
human-like than QuestingBots' current stamina-only check.

### 2.5 Door Handling

Phobos takes an aggressive approach to doors:

1. **Construction time**: Ignore collision between bot and ALL doors (`Physics.IgnoreCollision`)
2. **Runtime**: If a door is within 3m and not open, open it
3. **Speed**: Slow to 25% movement speed near doors
4. **NavMesh carvers**: Shrink NavMesh obstacle carvers on doors to prevent path invalidation

This is simpler than QuestingBots' approach (which handles locked doors, key checks, and
specific door IDs) but prevents the common "bot stuck at door" failure mode.

### 2.6 BSG Mover Patches

Phobos applies three critical Harmony patches:

| Patch | Target | Effect |
|-------|--------|--------|
| `BotMover.ManualFixedUpdate` prefix | Returns false | Disables BSG's movement entirely |
| `MovementContext.IsAI` getter | Returns false | Enables human-like movement params |
| `Player.InitVaultingComponent` | Enables for bots | Allows AI to vault obstacles |

**`ManualFixedUpdate` disable**: BSG's `BotMover` runs its own path-following logic in
`ManualFixedUpdate`. Phobos must disable this to prevent conflicting movement commands.
Without this patch, the bot would jitter between Phobos's `Player.Move()` and BSG's
internal movement.

**`IsAI` false**: BSG uses `MovementContext.IsAI` to apply different movement parameters
for AI vs humans. Returning false gives bots the same movement feel as human players.

### 2.7 Layer Handoff Protocol

When the Phobos brain layer deactivates (e.g., combat takes over via SAIN), BSG's mover
resumes. BSG remembers old position state, which can cause teleporting. Phobos syncs 6
state variables on layer exit:

```csharp
// Synced on deactivation:
botMover.LastGoodCastPoint = currentPosition;
botMover.PrevSuccessLinkedFrom_1 = currentPosition;
botMover.PrevLinkPos = currentPosition;
botMover.PositionOnWayInner = currentPosition;
botMover.SetPlayerToNavMesh();
botMover.ManualFixedUpdate();  // one final BSG tick to "catch up"
```

This is arguably the most fragile part of Phobos's approach --- these field names are
decompiled from obfuscated code and may change with game updates.

---

## 3. QuestingBots Current Movement Architecture

QuestingBots delegates to BSG's mover. The relevant files:

| File | Lines | Purpose |
|------|-------|---------|
| `BehaviorExtensions/GoToPositionAbstractAction.cs` | 367 | Base movement action |
| `Models/Pathing/BotPathData.cs` | 224 | Path calculation + management |
| `Models/Pathing/StaticPathData.cs` | ~177 | Pre-computed static paths |
| `Models/BotSprintingController.cs` | 90 | Sprint control with stamina |
| `Models/SoftStuckDetector.cs` | 121 | EWMA-based soft stuck detection |
| `Models/HardStuckDetector.cs` | 131 | Position-history hard stuck detection |
| `BotLogic/Objective/GoToObjectiveAction.cs` | 312 | Main "go to quest" action |
| `Components/BotObjectiveManager.cs` | 496 | Objective lifecycle management |
| `Components/QuestPathFinder.cs` | 229 | Static path pre-computation |

### 3.1 Movement Flow

```
GoToObjectiveAction.Update()
  → RecalculatePath(position)
      → BotPathData.CheckIfUpdateIsNeeded()
          → updateCorners() via NavMesh.CalculatePath()
          → [fallback] merge with static pre-computed paths
      → BotOwner.FollowPath(botPath, true, false)  ← BSG takes over here
  → checkIfBotIsStuck()
      → SoftStuckDetector.Update()  → vault / jump / fail
      → HardStuckDetector.Update()  → retry / teleport / fail
```

Key observation: After `BotOwner.FollowPath()`, BSG's `BotMover` handles all actual
movement execution. QuestingBots has **zero control** over how the bot physically moves
between corners.

### 3.2 Path Management (BotPathData)

`BotPathData.CheckIfUpdateIsNeeded()` determines when to recalculate:
- **Force**: External request (e.g., after vault/jump)
- **NewTarget**: Target position changed by more than 0.2m
- **IncompletePath**: Partial path, retry after configured interval
- **RefreshNeededPath**: Another mod changed the bot's active path

Path calculation uses `NavMesh.CalculatePath()` gated by `PathfindingThrottle` (max 5/frame).
For partial paths, it tries pre-computed static paths as a fallback.

### 3.3 Sprint Control (BotSprintingController)

Current sprint logic checks only:
1. `BotOwner.Mover.NoSprint` --- BSG's sprint block flag
2. `Physical.CanSprint` --- physical ability
3. `Stamina.NormalValue` vs configured min/max thresholds

No path-smoothness analysis. Bots will sprint into sharp corners and abruptly stop.

### 3.4 Stuck Detection (Already Ported from Phobos)

QuestingBots already has a two-tier stuck detection system inspired by Phobos:

| Detector | Technique | Escalation |
|----------|-----------|------------|
| `SoftStuckDetector` | EWMA speed + Y-axis filtering | Vault (1.5s) → Jump (3s) → Fail (6s) |
| `HardStuckDetector` | PositionHistory ring buffer | Retry (5s) → Teleport (10s) → Fail (15s) |

Safe teleportation includes proximity and line-of-sight checks against human players.

### 3.5 Door Handling

QuestingBots has more sophisticated door handling than Phobos:
- `GoToObjectiveAction`: Detects locked doors blocking path, triggers unlock flow
- `UnlockDoorAction`: Dedicated action for key-based door unlocking
- `CloseNearbyDoorsAction`: Closes doors after passing through (for quest realism)
- `BotDoorOpener`: BSG's built-in door interaction API
- Per-bot-type door permissions (PMC, Scav, PScav, Boss)

---

## 4. BSG Movement API Surface

BSG provides several layers of movement control:

### 4.1 High-Level (What QuestingBots Uses)

```csharp
BotOwner.FollowPath(BotPathData path, bool smoothPath, bool useNavMesh)
BotOwner.Mover.Sprint(bool value, bool withDebugCallback)
BotOwner.Mover.Stop()
BotOwner.Mover.SetPose(float pose)        // 0=crouch, 1=stand
BotOwner.Mover.GetCurrentPath()            // Vector3[]
BotOwner.Mover.IsPathComplete(Vector3, float)
BotOwner.SetPose(float pose)
BotOwner.DoorOpener.Interact(door, interactionType)
```

### 4.2 Mid-Level (Available but Unused)

```csharp
BotOwner.Mover.ManualFixedUpdate()         // Internal movement tick
BotOwner.Mover.GoToPoint(Vector3 point)    // Go to single point
BotOwner.Mover.MoveTo(Vector3 pos)         // Simple move command
BotOwner.Steering.LookToPoint(Vector3, float)
BotOwner.Steering.LookToDirection(Vector3, float)
```

### 4.3 Low-Level (What Phobos Uses)

```csharp
Player.Move(Vector2 direction)              // Direct input-level movement
Player.Physical.Sprint(bool)                // Direct sprint toggle
MovementContext.CharacterMovementSpeed       // Current speed
MovementContext.IsAI                        // AI vs human movement params
MovementContext.TryJump()
MovementContext.TryVaulting()
MovementContext.PlayerAnimatorIsJumpSetted()
MovementContext.PlayerAnimatorGetIsVaulting()
NavMesh.CalculatePath(start, end, mask, path)
```

### 4.4 Internal State (Fragile, Obfuscated)

```csharp
BotMover.LastGoodCastPoint                 // Last known good position
BotMover.PrevSuccessLinkedFrom_1           // Previous waypoint link
BotMover.PrevLinkPos                       // Previous link position
BotMover.PositionOnWayInner                // Position on current way
BotMover.SetPlayerToNavMesh()              // Sync player to NavMesh
BotCurrentPathAbstractClass                // Internal path data
```

---

## 5. Other Mods: SAIN, BigBrain, Waypoints

### 5.1 SAIN

SAIN (`/home/alberto/github/SAIN`) is a comprehensive AI combat overhaul. Movement-relevant
patterns:

- **Also bypasses BSG mover** --- uses `Player.Move(Vector2)` via `PlayerMovementController`,
  with `BotOwner.Mover.Stop()` + `BotOwner.Mover.Pause = true` every frame
- Custom path following via `BotPathDataManual` (ping-pong double-buffer pattern)
- Sprint control via `Player.EnableSprint()` with urgency-dependent stamina thresholds
  (Low starts at 90% stamina, High at 40%)
- Steering via `BotOwner.Steering.LookToPoint()` / `LookToDirection()`
- Custom door opener with raycasting (`DoorOpener.cs`)
- Lean/tilt via `MovementContext.SetTilt()`, blindfire via `MovementContext.SetBlindFire()`
- Stuck detection with coroutine-based vault/jump/teleport recovery
- 10 Harmony patches on BSG movement classes (including `ManualFixedUpdate` skip,
  `IsAI` → false, sprint direction gating, pose stamina bypass)
- Layer handoff: calls `BotOwner.Mover.SetPlayerToNavMesh()` on `SAINLayer.OnLayerChanged()`
- **Key insight**: SAIN already uses `Player.Move()` just like Phobos, which means
  QuestingBots' use of `BotOwner.FollowPath()` is the exception, not the norm.
  Adopting custom movement would actually **improve** compatibility with SAIN by
  using the same movement paradigm.

### 5.2 BigBrain

BigBrain (`/home/alberto/github/SPT-BigBrain`) manages brain layer activation/deactivation:

- Layers have priority: highest active layer wins
- `CustomLayer.Start()` / `Stop()` lifecycle methods
- **No built-in state save/restore** for movement --- each layer is responsible for its own
  cleanup
- QuestingBots layers: Sleeping (99), Regrouping (26), Following (19), Questing (18)
- SAIN combat layers typically run at higher priority (30--90)

### 5.3 Waypoints

Waypoints (`/home/alberto/github/SPT-Waypoints`) provides custom NavMesh data:

- Generates custom NavMesh for each map
- Exposes `DoorNavMeshBlocker` for door-aware pathfinding
- QuestingBots depends on Waypoints for reliable `NavMesh.CalculatePath()` results
- No custom movement execution --- purely a NavMesh data provider

---

## 6. Side-by-Side Comparison

| Aspect | QuestingBots (Current) | Phobos | SAIN |
|--------|----------------------|--------|------|
| **Movement execution** | `BotOwner.FollowPath()` (BSG) | `Player.Move()` (custom) | `Player.Move()` (custom) |
| **Path following** | BSG internal (no control) | Custom corner-reaching with epsilon | Custom `BotPathDataManual` |
| **Path smoothing** | None | Path-deviation spring force | None observed |
| **Sprint gating** | Stamina only | Angle jitter + outdoor + stamina | Urgency + angle + stamina |
| **Corner epsilon** | N/A (BSG handles) | Walk=0.35m, Sprint=0.6m | Custom (NavMesh.Raycast skip) |
| **Door handling** | Key-aware, per-type permissions | Collision bypass + auto-open | Custom DoorOpener (raycasting) |
| **Stuck detection** | Two-tier (from Phobos) | Two-tier (original source) | Coroutine-based + teleport |
| **Layer handoff** | Clean (uses BSG natively) | Fragile (6 state vars to sync) | `SetPlayerToNavMesh()` |
| **BSG patches** | 1 (Sprint intercept) | 5 (ManualFixedUpdate, IsAI, Vault, etc.) | 10 (ManualFixedUpdate, IsAI, etc.) |
| **Mod compat approach** | Delegates to BSG mover | Conditional `ManualFixedUpdate` skip | Conditional `ManualFixedUpdate` skip |
| **Look direction** | BSG steering | Custom LookSystem from path | BSG steering + custom |
| **Movement data** | Scattered (MonoBehaviour fields) | Dense entity list | Per-bot class instances |
| **Testability** | Stuck detectors testable | Path math testable | Limited (tightly coupled) |

---

## 7. Pros and Cons of Adopting Custom Movement

### 7.1 Full Replacement (Phobos-Style)

**Pros:**
- Smoothest possible movement (path-deviation spring force)
- Full control over sprint gating, pose, speed
- Eliminates BSG mover quirks (overshooting corners, jerky turns)
- Human-like movement via `IsAI=false` patch
- AI vaulting via `InitVaultingComponent` patch
- **Same paradigm as SAIN**: both SAIN and Phobos use `Player.Move()` with BSG mover
  disabled --- QuestingBots using `BotOwner.FollowPath()` is the outlier

**Cons:**
- **Medium risk**: SAIN already disables `ManualFixedUpdate` for its own bots, so
  the patch must be coordinated (both mods check their own active state)
- **Fragile handoff**: 6 obfuscated fields must be synced on layer exit
- **Maintenance**: Obfuscated field names change with game updates
- **Complexity**: ~635 lines of movement code to maintain
- **Looting Bots**: May expect BSG mover to work during loot scanning

### 7.2 Hybrid Enhancement (Recommended)

**Pros:**
- **Low risk**: BSG mover continues to work for all layers
- **Full compatibility**: SAIN, Looting Bots, BigBrain all work unchanged
- **Incremental**: Ship improvements in phases, each independently valuable
- **No patches**: No Harmony patches needed for movement
- **Testable**: Sprint decision and path smoothing are pure math

**Cons:**
- Movement smoothness limited by BSG's path-following quality
- Cannot fully eliminate BSG mover quirks (corner overshooting)
- Sprint gating improvement bounded by BSG's sprint implementation
- Path pre-smoothing may not be as effective as runtime spring force

---

## 8. ECS Data-Layout Considerations

### 8.1 Current ECS State

QuestingBots' ECS-Lite system already tracks per-bot state:

```
BotEntity
├── Id, IsActive, BotType, IsSleeping
├── Boss/Followers hierarchy
├── Sensor bools (IsInCombat, IsSuspicious, CanQuest, CanSprintToObjective, WantsToLoot)
├── LastLootingTime, ConsecutiveFailedAssignments
├── BotFieldState (zone movement: Momentum, FieldNoiseSeed)
└── Job assignments (List<BotJobAssignment>)
```

### 8.2 What Could Move to ECS

**MovementState** (new struct on BotEntity):
```csharp
public struct MovementState
{
    public Vector3 CurrentTarget;
    public PathStatus Status;           // None, Computing, Complete, Partial, Invalid
    public bool IsMoving;
    public bool IsSprinting;
    public float CurrentPose;           // 0=crouch, 1=stand
    public StuckPhase StuckStatus;      // None, Soft, Hard, Failed
    public float LastPathUpdateTime;
    public float SprintAngleJitter;     // Computed from path corners
}
```

**Benefits:**
- Dense iteration for batch queries ("how many bots are stuck?", "how many sprinting?")
- Deterministic processing order (already established in BotHiveMindMonitor.Update)
- O(1) movement state lookup by entity ID (no dictionary hash)
- Batch sprint decisions: iterate dense list, compute angle jitter, set CanSprintToObjective

**What should NOT move to ECS:**
- Actual `Player.Move()` calls (must happen per-bot in Unity context)
- Door interaction state (tied to Unity physics/colliders)
- NavMesh pathfinding (Unity API, inherently per-request)
- Path corners array (variable-length, better as reference in BotPathData)

### 8.3 ECS Integration Approach

Movement state on entities serves the **decision** layer, while MonoBehaviours handle
**execution**:

```
ECS Decision Layer              Unity Execution Layer
─────────────────              ──────────────────────
SprintDecisionSystem           BotSprintingController
  → reads path corners           → calls BotOwner.Mover.Sprint()
  → computes angle jitter
  → sets CanSprintToObjective

PathSmoothingSystem            BotPathData
  → reads raw corners             → stores smoothed corners
  → inserts intermediate points   → feeds to BotOwner.FollowPath()
  → writes smoothed corners

MovementStateTracker           GoToPositionAbstractAction
  → reads bot position            → calls RecalculatePath()
  → updates IsMoving/IsSprinting  → calls checkIfBotIsStuck()
  → tracks StuckStatus            → handles stuck remedies
```

---

## 9. Implementation Options

### Option A: Full Phobos-Style Replacement ← IMPLEMENTED

Replace BSG mover entirely with `Player.Move()`, custom path following, spring force.

- **Risk**: Medium (SAIN uses same approach; config-gated for safety)
- **Status**: **Implemented** — all components built and wired
- **Compatibility**: SAIN-compatible (same paradigm); enabled by default (can disable)
- **Activation**: Set `use_custom_mover: true` in `config.json` → `questing.bot_pathing`

### Option B: Hybrid Enhancement

Keep BSG mover. Add sprint intelligence, path pre-smoothing, ECS movement state.

- **Risk**: Low to medium
- **Status**: Superseded by Option A (which includes all Option B components)
- **Compatibility**: Full
- **Note**: Option A is now enabled by default; can be disabled via config for fallback

### Option C: Selective Override with Fallback

Custom `Player.Move()` ONLY during QuestingBots layers. BSG mover resumes for other layers
via handoff protocol.

- **Risk**: High
- **Status**: Superseded — Option A's implementation uses this exact pattern
  (custom mover active only during QuestingBots layers, BSG mover resumes on `Stop()`)
- **Note**: The implemented system IS a selective override with fallback

---

## 10. Recommended Implementation Plan

### Phase 1: Sprint Decision Enhancement ← IMPLEMENTED

**Goal**: Bots slow down before sharp turns and sprint on straight paths.

**New classes:**
- `Models/SprintAngleJitter.cs` --- pure-logic angle-jitter calculator
  - `float ComputeAngleJitter(Vector3[] corners, int startIndex, int lookAhead)`
  - `bool CanSprint(float angleJitter, SprintUrgency urgency)`
  - Input: path corners + bot position → Output: angle jitter in degrees
  - All math in XZ plane (ignore Y for terrain)

**Urgency thresholds** (from Phobos, configurable):
| Urgency | Max Jitter | Use Case |
|---------|-----------|----------|
| High | 45 degrees | Fleeing combat, rushing objective |
| Medium | 30 degrees | Normal questing movement |
| Low | 20 degrees | Cautious movement, near objective |

**Wiring:**
- `BotSprintingController.ManualUpdate()` adds angle-jitter gate before `trySprint()`
- `GoToObjectiveAction.Update()` computes urgency from objective distance + combat state
- Config: Add `SprintSmoothnessThresholds` to `config.json` questing section

**ECS integration (minimal):**
- Add `SprintUrgency` field to `BotEntity` (set by objective manager)
- `CanSprintToObjective` sensor updated via angle-jitter result

**Tests (15--20):**
- Straight path → angle jitter = 0 → can sprint
- 90-degree turn → angle jitter > all thresholds → cannot sprint
- Multiple gentle turns → cumulative jitter → threshold-dependent
- Single corner at various angles → expected jitter values
- Look-ahead window: 2 corners vs 5 corners
- Edge cases: empty path, single corner, bot at last corner

### Phase 2: Path Pre-Smoothing ← IMPLEMENTED

**Goal**: Smoother paths fed to BSG mover, reducing jerky corner-to-corner movement.

**New classes:**
- `Models/Pathing/PathSmoother.cs` --- pure-logic path corner smoother
  - `Vector3[] SmoothCorners(Vector3[] corners, float maxDeviation)` --- Chaikin subdivision
  - `Vector3[] InsertIntermediatePoints(Vector3[] corners, float minSegmentLength)` ---
    insert midpoints on long segments
  - All operations on Vector3 arrays, no Unity dependencies beyond Vector3

- `Models/Pathing/PathDeviationForce.cs` --- spring force calculator (future use)
  - `Vector3 ComputeDeviation(Vector3 botPos, Vector3 segStart, Vector3 segEnd)`
  - Pure XZ-plane math via dot-product projection
  - Returns deviation vector toward nearest point on path segment

**Algorithm (Chaikin corner-cutting):**
```
For each pair of adjacent corners:
  1. Insert point at 25% along segment
  2. Insert point at 75% along segment
  3. Replace original corners with new points
  4. Repeat 1-2 iterations
  5. Ensure start and end points are preserved
```

**Wiring:**
- `BotPathData.updateCorners()` calls `PathSmoother.SmoothCorners()` after
  `NavMesh.CalculatePath()` and before `SetCorners()`
- Config: Add `PathSmoothing.Enabled`, `PathSmoothing.MaxDeviation`,
  `PathSmoothing.Iterations` to questing config

**Tests (20--30):**
- Straight path → unchanged after smoothing
- Right-angle turn → smoothed to curve
- Chaikin iteration count: 0, 1, 2, 3
- Start/end point preservation
- MaxDeviation constraint
- Path-deviation spring force: dot-product projection correctness
- Spring force perpendicularity to path segment
- Bot exactly on path → zero deviation
- Bot far from path → deviation capped

### Phase 3: Movement State on ECS ← IMPLEMENTED

**Goal**: Dense movement state for batch queries and deterministic processing.

**New types:**
- `BotLogic/ECS/MovementState.cs` --- struct with IsMoving, IsSprinting, StuckPhase,
  SprintAngleJitter, LastPathUpdateTime

**BotEntity additions:**
- `MovementState Movement` field (value type, inline on entity)

**BotEntityBridge additions:**
- `UpdateMovementState(BotOwner, ...)` --- push movement state to ECS
- `GetMovementState(BotOwner)` → `MovementState`
- `CountStuckBots()` → dense iteration count
- `CountSprintingBots()` → dense iteration count

**HiveMindSystem additions:**
- `ResetMovementForInactiveEntities(List<BotEntity>)` --- clear movement state for dead/inactive bots

**Wiring:**
- `GoToPositionAbstractAction.checkIfBotIsStuck()` pushes stuck status to entity
- `BotSprintingController` pushes sprint state to entity
- `BotHiveMindMonitor.Update()` includes movement state reset in deterministic tick

**Tests (15--20):**
- Movement state round-trip via BotEntityBridge
- Batch count queries (stuck, sprinting)
- Inactive entity reset
- Dense iteration correctness

### Phase 4: Custom Path Following ← IMPLEMENTED

**Goal**: Full `Player.Move()` override during QuestingBots layers only.

**Implemented classes:**

| File | Lines | Purpose |
|------|-------|---------|
| `Models/Pathing/CustomPathFollower.cs` | ~365 | Pure-logic path engine: corner-reaching, spring force, sprint gating |
| `Models/Pathing/CustomMoverController.cs` | ~180 | Unity integration: bridges CustomPathFollower to `Player.Move()` |
| `Models/Pathing/CustomMoverConfig.cs` | ~60 | Config struct with Phobos-matching defaults |
| `Helpers/CustomMoverHandoff.cs` | ~55 | BSG state sync during mover transitions (6-field protocol) |
| `Patches/Movement/BotMoverFixedUpdatePatch.cs` | ~30 | Prefix: skips `ManualFixedUpdate` when custom mover active |
| `Patches/Movement/MovementContextIsAIPatch.cs` | ~25 | Prefix: `IsAI` → false for human-like movement params |
| `Patches/Movement/EnableVaultPatch.cs` | ~25 | Prefix: enables vaulting for AI bots |

**Action system wiring:**
- `GoToPositionAbstractAction`: Creates/activates `CustomMoverController` in `Start()`,
  deactivates in `Stop()`. `RecalculatePath()` feeds smoothed corners to custom follower
  instead of `BotOwner.FollowPath()`. `TickCustomMover()` method for per-frame execution.
- `GoToObjectiveAction`: Calls `TickCustomMover(CanSprint)` instead of `UpdateBotMovement()`
  when custom mover is active. Directly manages pose/mine/door to avoid BSG sprint conflicts.

**Layer handoff (implemented in `CustomMoverHandoff`):**
- On `Activate()`: `BotOwner.Mover.Stop()` + set ECS flag
- On `Deactivate()`: Sync 6 BSG state fields + `SetPlayerToNavMesh()` + clear ECS flag

**Movement execution (Phobos `CalcMoveDirection` pattern):**
```csharp
Vector2 dir2d = new Vector2(direction.x, direction.z);
Vector3 rotated = Quaternion.Euler(0f, 0f, rotation.x) * dir2d;
Player.Move(new Vector2(rotated.x, rotated.y));
```

**Config activation:**
- `config.json` → `questing.bot_pathing.use_custom_mover` (default: true, set false to disable)
- Patches registered conditionally in `QuestingBotsPlugin.Awake()`

**Tests (~60 total across all phases):**
- `SprintAngleJitter`: 22 tests (angle computation, urgency thresholds, edge cases)
- `PathDeviationForce`: 15 tests (spring force, projection, perpendicularity)
- `CustomPathFollower`: 12 tests (corner reaching, path completion, sprint gating)
- `MovementState wiring`: 11 tests (ECS activation, deactivation, batch queries)

---

## 11. Risk Assessment

### Phase 1 (Sprint Decision) --- LOW RISK

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Angle jitter math bug | Medium | Low | Extensive unit tests |
| Sprint feels "wrong" | Low | Low | Configurable thresholds |
| Performance impact | Very low | Very low | Pure math, one pass per update |

### Phase 2 (Path Smoothing) --- MEDIUM RISK

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Smoothed path leaves NavMesh | Medium | Medium | Validate points on NavMesh after smoothing |
| BSG mover confused by extra corners | Low | Medium | Test with BSG's FollowPath extensively |
| Over-smoothing causes path divergence | Low | Low | MaxDeviation constraint |
| Static path merging conflicts | Medium | Medium | Apply smoothing after merge step |

### Phase 3 (ECS Movement State) --- MEDIUM RISK

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Stale state if push is missed | Medium | Low | Defensive defaults (IsMoving=false) |
| Thread safety with dense list | Low | Medium | All updates in main thread |
| Memory increase per entity | Very low | Very low | Struct is ~40 bytes |

### Phase 4 (Custom Path Following) --- MEDIUM-HIGH RISK

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| SAIN conflict | Low | High | Same paradigm --- both use Player.Move() with conditional ManualFixedUpdate skip |
| Looting Bots conflict | Medium | Medium | Verify loot scanning doesn't depend on BSG mover |
| Layer handoff teleport bugs | High | High | Extensive integration testing; follow SAIN's handoff pattern |
| BSG field names change | High | Medium | Abstract behind wrapper, update per patch |
| Door collision bugs | Medium | Medium | Test on each map |
| Vaulting inconsistencies | Medium | Medium | Fall back to BSG if vault fails |

---

## 12. Testability Analysis

### Fully Testable (Pure Logic, No Unity Runtime)

| Component | Technique | Coverage |
|-----------|-----------|----------|
| `SprintAngleJitter` | NUnit with Vector3 shim | 15--20 tests |
| `PathSmoother` (Chaikin) | NUnit with Vector3 shim | 10--15 tests |
| `PathDeviationForce` | NUnit with Vector3 shim | 10--15 tests |
| `MovementState` struct | NUnit | 5--10 tests |
| ECS batch queries | BotEntityBridge scenario tests | 10--15 tests |

### Partially Testable

| Component | Testable Part | Untestable Part |
|-----------|--------------|-----------------|
| `BotSprintingController` | Angle jitter decision | `BotOwner.Mover.Sprint()` call |
| `BotPathData` | Corner update logic | `NavMesh.CalculatePath()` |
| Stuck detectors | Detection math (already tested) | Vault/jump/teleport execution |

### Integration-Only (Requires Game Runtime)

| Component | Why |
|-----------|-----|
| `Player.Move()` path following | Requires Unity character controller |
| BSG mover patches | Requires Harmony + game assemblies |
| Layer handoff state sync | Requires BSG `BotMover` internals |
| Door collision bypass | Requires Unity physics |
| NavMesh pathfinding | Requires Unity NavMesh system |

**Estimated test counts (Phases 1--3):**
| Phase | New Tests | Running Total |
|-------|-----------|---------------|
| Phase 1 | 15--20 | 15--20 |
| Phase 2 | 20--30 | 35--50 |
| Phase 3 | 15--20 | 50--70 |
| Phase 4 | 10--15 | 60--85 |

---

## 13. Conclusion

**Implemented: Option A (Full Phobos-Style Replacement), all 4 phases complete.**

The full custom movement system is built and config-gated behind `use_custom_mover`
(default: true). QuestingBots now uses the same `Player.Move()` paradigm as
both Phobos and SAIN, with:

- **Sprint angle-jitter analysis** — bots slow before sharp turns, sprint on straight paths
- **Chaikin path smoothing** — NavMesh corners are subdivided for smoother trajectories
- **Path-deviation spring force** — XZ-plane spring pulls bots back toward ideal path line
- **Custom path follower** — pure-logic engine with configurable corner-reaching epsilon
- **3 BSG patches** — `ManualFixedUpdate` skip, `IsAI` → false, vault enable
- **Layer handoff** — 6-field BSG state sync via `CustomMoverHandoff` on layer exit
- **ECS movement state** — `MovementState` struct on `BotEntity` for batch queries

**Risk mitigations:**
1. **Config-gated**: Can be disabled via `use_custom_mover: false` to fall back to BSG mover
2. **Layer-scoped**: Custom mover active ONLY during QuestingBots layers; BSG resumes
   on `Stop()` with full state sync
3. **SAIN-compatible**: Uses the same `Player.Move()` paradigm with conditional
   `ManualFixedUpdate` skip (checked per-bot via ECS entity lookup)
4. **Extensively tested**: ~60 new unit tests across all components

**Remaining risks:**
1. **BSG field names**: 6 obfuscated fields in `CustomMoverHandoff.SyncBsgMoverState()`
   may change with game updates — abstract behind wrapper
2. **Looting Bots**: May expect BSG mover during loot scanning — needs runtime testing
3. **Door handling**: Current implementation uses existing QuestingBots door system;
   Phobos-style collision bypass not yet implemented

**Total new tests**: ~60 (22 SprintAngleJitter + 15 PathDeviationForce + 12 CustomPathFollower + 11 MovementState wiring)

---

## Appendix A: File Reference

### Phobos Source Files

| File | Key Content |
|------|-------------|
| `Systems/MovementSystem.cs` | Path following, sprint, doors, move execution |
| `Components/Movement.cs` | Target, Path, Status, Sprint, Pose, Urgency |
| `Components/Stuck.cs` | IsStuck, StuckTimer, StuckPosition |
| `Components/Look.cs` | LookTarget, LookType |
| `Entities/Agent.cs` | Movement + Stuck + Look components |
| `Entities/Entity.cs` | Base entity with Id, task scores |
| `Entities/Squad.cs` | Squad grouping |
| `Systems/LookSystem.cs` | Path forward point look direction |
| `Helpers/PathHelper.cs` | CalcForwardPoint, path distance math |
| `Helpers/VectorHelper.cs` | ToVector2 (XZ projection), ToVector3 |
| `Helpers/Pacing.cs` | TimePacing, FramePacing rate limiters |
| `Helpers/PositionHistory.cs` | Ring buffer for position tracking |
| `Helpers/RollingAverage.cs` | Window-based rolling average |

### QuestingBots Source Files (Existing)

| File | Key Content |
|------|-------------|
| `BehaviorExtensions/GoToPositionAbstractAction.cs` | RecalculatePath, stuck detection, custom mover lifecycle |
| `Models/Pathing/BotPathData.cs` | CheckIfUpdateIsNeeded, updateCorners |
| `Models/Pathing/StaticPathData.cs` | Pre-computed paths, Append, Prepend |
| `Models/BotSprintingController.cs` | Stamina-based sprint control |
| `Models/SoftStuckDetector.cs` | EWMA speed, vault→jump→fail |
| `Models/HardStuckDetector.cs` | PositionHistory, retry→teleport→fail |
| `BotLogic/Objective/GoToObjectiveAction.cs` | Main movement action, custom mover tick |
| `BotLogic/Objective/UnlockDoorAction.cs` | Key-based door unlocking |
| `BotLogic/Objective/CloseNearbyDoorsAction.cs` | Close doors after passing |
| `Components/BotObjectiveManager.cs` | Objective lifecycle |
| `Components/QuestPathFinder.cs` | Static path pre-computation |
| `Helpers/PathfindingThrottle.cs` | Max 5 NavMesh.CalculatePath per frame |

### QuestingBots Custom Movement Files (New)

| File | Lines | Purpose |
|------|-------|---------|
| `Models/Pathing/CustomPathFollower.cs` | ~365 | Pure-logic path engine: corner-reaching, spring force, sprint gating |
| `Models/Pathing/CustomMoverController.cs` | ~180 | Unity integration: bridges CustomPathFollower to Player.Move() |
| `Models/Pathing/CustomMoverConfig.cs` | ~60 | Config struct (corner epsilon, spring strength, sprint thresholds) |
| `Models/Pathing/PathSmoother.cs` | ~80 | Chaikin corner-cutting subdivision algorithm |
| `Models/Pathing/SprintAngleJitter.cs` | ~70 | XZ-plane angle jitter calculator for sprint gating |
| `Models/Pathing/PathDeviationForce.cs` | ~60 | XZ-plane spring force toward ideal path line |
| `Models/Pathing/MovementState.cs` | ~50 | ECS struct: path status, sprint, stuck, corner progress |
| `Helpers/CustomMoverHandoff.cs` | ~55 | BSG state sync (6-field protocol) during mover transitions |
| `Patches/Movement/BotMoverFixedUpdatePatch.cs` | ~30 | Prefix: skip ManualFixedUpdate when custom mover active |
| `Patches/Movement/MovementContextIsAIPatch.cs` | ~25 | Prefix: IsAI → false for human-like movement params |
| `Patches/Movement/EnableVaultPatch.cs` | ~25 | Prefix: enables vaulting for AI bots |
| `Configuration/BotPathingConfig.cs` | ~23 | Added `UseCustomMover` config property |
