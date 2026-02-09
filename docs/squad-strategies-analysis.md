# Squad Strategies Analysis: Coordinated Group Questing

**Date**: 2026-02-09
**Version**: QuestingBots v1.9.0
**Status**: Implemented (Phases 1-4 of Option B complete)

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Phobos Squad Architecture](#2-phobos-squad-architecture)
3. [QuestingBots Current State](#3-questingbots-current-state)
4. [SAIN Patterns](#4-sain-patterns)
5. [Implementation Options](#5-implementation-options)
6. [Recommended Approach: Option B](#6-recommended-approach-option-b)
7. [Integration Points](#7-integration-points)
8. [New Classes](#8-new-classes)
9. [Migration Path](#9-migration-path)
10. [Testing Strategy](#10-testing-strategy)
11. [Phased Implementation Plan](#11-phased-implementation-plan)
12. [Future Work](#12-future-work)

---

## 1. Executive Summary

**Problem**: QuestingBots followers are completely passive. Three hard gates block
followers from performing any quest-related behavior when their boss is questing.
Followers either stand idle, follow their boss blindly, or regroup. This makes
groups of 2-5 bots feel unrealistic — real players coordinate, spread out, and
take tactical positions while their squad leader works on an objective.

**Solution**: Add squad-level strategy coordination so followers derive tactical
positions from their boss's current objective. Instead of following the boss like
ducklings, followers move to cover points, flanking positions, or guard locations
near the boss's objective — creating emergent squad behavior.

**What this adds**:
- Followers get tactical positions derived from the boss's objective
- Quest-type-aware role assignment (sniper overwatch, flanking, guarding)
- Squad-level utility scoring (a new `SquadStrategySystem`)
- Partial gate unlocking so followers can execute tactical behaviors
- Configurable, opt-in, backward-compatible with existing behavior

**What this does NOT change**:
- Boss quest selection remains unchanged (bosses still pick their own quests)
- No new quest types or objectives
- No formation code (emergent from different tactical positions)
- No new BigBrain layers (reuses existing Questing layer + Following layer)

**Key insight from Phobos**: Phobos achieves natural squad behavior without any
explicit formation code. Each squad member receives a different cover point from
the same zone. The variety of positions creates emergent formations. QuestingBots
can adopt the same pattern: derive per-follower tactical positions from the
boss's objective, and let utility scoring handle transitions.

---

## 2. Phobos Squad Architecture

### 2.1 Two-Entity-Type Model

Phobos maintains two entity types in its ECS-inspired architecture:

```
Entity (base)
├── int Id
├── float[] TaskScores
└── TaskAssignment

Squad : Entity
├── List<Agent> Members
├── SquadObjective Objective
├── Agent Leader
└── int TargetMembersCount

Agent : Entity
├── bool IsActive, IsLeader
├── Squad Squad
├── BotOwner Bot
└── Components: Movement, Stuck, Look, Objective, Guard
```

Reference: `Phobos/Phobos/Entities/Squad.cs`, `Phobos/Phobos/Entities/Agent.cs`

### 2.2 Squad Registry

`SquadRegistry` (`Phobos/Phobos/Orchestration/SquadRegistry.cs`) maps BSG's
`BotsGroup.Id` to Phobos squad IDs via `Dictionary<int, int>`:

| Bot Type | Squad Policy |
|----------|-------------|
| PMCs | Share squad by BSG group |
| Bosses + followers | Share squad by BSG group |
| Scavs | Solo squads (configurable via `FormScavSquads`) |

**Leader election**: First registered member becomes leader. On leader death,
the last remaining member is promoted. Empty squads are fully removed from all
registries.

### 2.3 Two-Level Utility AI

Phobos evaluates decisions at two granularities:

```
PhobosManager.Update() tick order:
─────────────────────────────────────────────────
1. LocationSystem.Update()        — convergence field (30s interval)
2. StrategyManager.Update()       — per-squad strategies (0.5s TimePacing)
3. ActionManager.Update()         — per-agent actions (every frame)
4. MovementSystem.Update()        — path following
5. LookSystem.Update()            — look direction
6. NavJobExecutor.Update()        — batched pathfinding
```

Reference: `Phobos/Phobos/Orchestration/PhobosManager.cs:111-120`

Both levels use identical `BaseTaskManager.PickTask()` with additive hysteresis:

| Level | Entity Type | Manager | Pacing | Tasks |
|-------|-------------|---------|--------|-------|
| **Strategy** | Squad | `StrategyManager` | 0.5s `TimePacing` | `GotoObjectiveStrategy` (h=0.25) |
| **Action** | Agent | `ActionManager` | Every frame | `GotoObjectiveAction` (h=0.25), `GuardAction` (h=0.10) |

### 2.4 GotoObjectiveStrategy (Squad-Level)

The only strategy in Phobos. Manages the full squad objective lifecycle:

```
GotoObjectiveStrategy.Update() lifecycle:
─────────────────────────────────────────────────
1. If no objective → AssignNewObjective()
   └── LocationSystem.RequestNear(squad, leaderPos, previous)
       └── Combines 4 force vectors: advection + convergence + momentum + random
       └── Picks neighboring grid cell closest to preferred direction

2. UpdateAgents() — sync squad objective to each member:
   └── agent.Objective.Location = squad.Objective.Location
   └── agent.Guard.CoverPoint = ShufflePickCoverPoints()[memberIndex]

3. Track arrivals:
   └── First agent enters radius → switch to Wait mode
   └── Start Gaussian guard timer (60-180s base, 0.2-0.5x cut if all arrive)

4. Timer expires OR all agents finish → new objective
```

Reference: `Phobos/Phobos/Tasks/Strategies/GotoObjectiveStrategy.cs`

**Guard timers** (Gaussian-sampled for natural variation):

| Timer | Min | Max | Distribution |
|-------|-----|-----|-------------|
| Move timeout | 400s | 600s | Gaussian |
| Guard duration | 60s | 180s | Gaussian |
| Cut factor (all arrive) | 0.2x | 0.5x | Uniform |

**Cover point assignment**: `ShufflePickCoverPoints()` gives each squad member a
different cover point from the Location. This is the key mechanism that creates
emergent formations — no explicit formation code exists.

### 2.5 Agent-Level Actions

**GotoObjectiveAction** (`Phobos/Phobos/Tasks/Actions/GotoObjectiveAction.cs`):
- Score: 0.5 when far, +0.15 distance boost when close, 0 inside objective radius
- Update: sprint to `agent.Objective.Location.Position`
- Hysteresis: 0.25

**GuardAction** (`Phobos/Phobos/Tasks/Actions/GuardAction.cs`):
- Score: 0 when outside radius, ramps to 0.65 at 80% of objective radius
- Update: walk to `agent.Guard.CoverPoint.Position`
- Hysteresis: 0.10

### 2.6 Data Flow (End-to-End)

```
Squad formed (SquadRegistry)
     │
     ▼
StrategyManager.Update() [per-squad, 0.5s]
     │
     ├── GotoObjectiveStrategy.UpdateScore()  →  constant 0.5
     │
     ├── GotoObjectiveStrategy.Update()
     │   ├── AssignNewObjective()  →  LocationSystem.RequestNear()
     │   │                              └── advection + convergence + momentum + random
     │   │                              └── pick best neighboring grid cell
     │   │
     │   ├── UpdateAgents()        →  sync objective + unique CoverPoints to each member
     │   │
     │   └── TrackArrivals()       →  guard timer management
     │
     ▼
ActionManager.Update() [per-agent, every frame]
     │
     ├── GotoObjectiveAction.UpdateScore()  →  0.5-0.65 when traveling, 0 at destination
     │
     ├── GuardAction.UpdateScore()          →  0-0.65 based on proximity to objective
     │
     ▼
MovementSystem.Update()
     │
     └── Player.Move() toward agent.Objective.Position or agent.Guard.CoverPoint.Position
```

**Key takeaway**: Formation is **emergent**, not coded. Each member gets a
different CoverPoint, creating natural spread. The GotoObjective→Guard transition
happens automatically via utility scoring — no state machine needed.

---

## 3. QuestingBots Current State

### 3.1 Existing Group Model

QuestingBots uses a **star topology** on `BotEntity`:

```csharp
// src/SPTQuestingBots.Client/BotLogic/ECS/BotEntity.cs
public BotEntity Boss;                    // null for bosses/solo
public readonly List<BotEntity> Followers; // empty for non-bosses
```

Group queries are zero-allocation:
- `CheckSensorForBoss(BotSensor)` — O(1)
- `CheckSensorForAnyFollower(BotSensor)` — O(n) followers
- `CheckSensorForGroup(BotSensor)` — O(n) group members

Reference: `src/SPTQuestingBots.Client/BotLogic/ECS/BotEntity.cs:235-274`

### 3.2 The Three Gates

Three independent gates prevent followers from performing any quest behavior:

**Gate 1 — `BotObjectiveLayer.IsActive()` (line 53-56)**:

```csharp
// src/SPTQuestingBots.Client/BotLogic/Objective/BotObjectiveLayer.cs:53-56
if (decisionMonitor.HasAQuestingBoss)
{
    return updatePreviousState(false);  // FOLLOWERS BLOCKED FROM QUESTING LAYER
}
```

Effect: The entire questing BigBrain layer returns `false` for followers with a
questing boss. No quest actions can execute.

**Gate 2 — `getFollowerDecision()` (line 102-168)**:

```csharp
// src/SPTQuestingBots.Client/BotLogic/BotMonitor/Monitors/BotQuestingDecisionMonitor.cs:102-104
private BotQuestingDecision getFollowerDecision()
{
    Controllers.BotJobAssignmentFactory.InactivateAllJobAssignmentsForBot(BotOwner.Profile.Id);
    // ALL follower quests killed EVERY TICK
    // Then: Fight > Investigate > Hunt > Heal > StuckTooManyTimes > HelpBoss >
    //        WaitForGroup > CheckForLoot > FollowBoss > None
}
```

Effect: Even if Gate 1 were opened, all follower quest assignments are destroyed
every tick. The follower decision tree never reaches `Quest` — it terminates at
`FollowBoss` or `None`.

**Gate 3 — `BotObjectiveManager.Update()` (line 205-208)**:

```csharp
// src/SPTQuestingBots.Client/Components/BotObjectiveManager.cs:205-208
if (BotLogic.ECS.BotEntityBridge.HasBoss(botOwner))
{
    return;  // FOLLOWERS SKIP OBJECTIVE MONITORING ENTIRELY
}
```

Effect: Followers never update their objective state, never check quest progress,
and never receive new assignments through the normal objective pipeline.

### 3.3 Follower Behavior Layers

| Layer | Class | Priority | Trigger | Behavior |
|-------|-------|----------|---------|----------|
| Sleeping | `SleepingLayer` | 99 | Distance to humans | Disable AI |
| Regrouping | `BotFollowerRegroupLayer` | 26 | Boss in combat + far | Sprint to boss |
| Following | `BotFollowerLayer` | 19 | `FollowBoss` decision | Walk toward boss |
| Questing | `BotObjectiveLayer` | 18 | **BLOCKED** for followers | Never activates |

### 3.4 Decision Flow for Followers

```
BotQuestingDecisionMonitor.Update()
     │
     ├── HasAQuestingBoss = true
     │
     └── getFollowerDecision()
          │
          ├── InactivateAllJobAssignments()   ← kills ALL quest assignments every tick
          │
          ├── Fight?         → BotQuestingDecision.Fight
          ├── Investigate?   → BotQuestingDecision.Investigate
          ├── Hunt?          → BotQuestingDecision.Hunt
          ├── NeedsToHeal?   → BotQuestingDecision.StopToHeal
          ├── StuckTooMany?  → BotQuestingDecision.GetLost
          ├── BossNeedsHelp? → BotQuestingDecision.HelpBoss
          ├── GroupInCombat? → BotQuestingDecision.WaitForGroup
          ├── WantsToLoot?   → BotQuestingDecision.CheckForLoot
          ├── TooFarFromBoss?→ BotQuestingDecision.FollowBoss
          └── else           → BotQuestingDecision.None
               │
               └── BotObjectiveLayer.IsActive() sees HasAQuestingBoss → returns false
```

### 3.5 What Exists and Is Reusable

| Component | Location | Reusable For |
|-----------|----------|-------------|
| Boss/Followers on `BotEntity` | `BotLogic/ECS/BotEntity.cs:25-31` | Star topology = squad model |
| `BotEntityBridge` group methods | `BotLogic/ECS/BotEntityBridge.cs` | All read methods work for squad queries |
| `HiveMindSystem` lifecycle | `BotLogic/ECS/Systems/HiveMindSystem.cs` | AssignBoss/RemoveBoss/Cleanup |
| Group sensor propagation | `BotEntity.CheckSensorForGroup()` | Zero-allocation group state checks |
| Utility AI framework | `BotLogic/ECS/UtilityAI/` | `UtilityTask`/`UtilityTaskManager` with hysteresis |
| `QuestUtilityTask` + `BotActionTypeId` | `BotLogic/ECS/UtilityAI/QuestUtilityTask.cs` | Action mapping for BigBrain dispatch |
| BigBrain layer/action infrastructure | `BehaviorExtensions/` | 13 `CustomLogic` action implementations |
| `BotJobAssignmentFactory` group size filter | `Controllers/BotJobAssignmentFactory.cs:705` | `MaxBotsInGroup >= botGroupSize` already exists |
| Custom movement system | `Models/Pathing/` | Path following + stuck detection for tactical movement |
| `MovementState` on `BotEntity` | `BotLogic/ECS/MovementState.cs` | Per-bot movement tracking |

### 3.6 What Is Missing

| Missing Piece | Why It Matters |
|--------------|---------------|
| Shared objective concept | No way to tell followers what the boss is working on |
| Tactical roles for followers | All followers are identical — no sniper/flanker/guard differentiation |
| Follower independence | All 3 gates completely block follower questing |
| Squad-level scoring | Utility AI is per-entity only — no group-aware scoring |
| Position derivation from boss objective | No mechanism to compute offset/cover positions |
| Squad objective versioning | No way to detect when boss changes objective and followers need new positions |

---

## 4. SAIN Patterns

SAIN (`/home/alberto/github/SAIN`) provides complementary ideas for squad
coordination that go beyond what Phobos implements.

### 4.1 Equipment-Gated Communication

```
Without earpiece: max 35m communication range
With earpiece: unlimited communication range
```

**Application**: Gate whether followers receive tactical positions based on
equipment. A follower without an earpiece beyond 35m would not receive updated
tactical assignments, creating realistic communication breakdowns.

### 4.2 Squad Personality (3-Axis)

SAIN derives emergent squad personality from member composition:

| Axis | Range | Effect |
|------|-------|--------|
| Vocalization | 0-1 | How chatty the squad is (callouts) |
| Coordination | 0-1 | Quality of tactical position derivation |
| Aggression | 0-1 | Offensive vs defensive role preference |

**Application**: Scale tactical offset distances and coordination quality. A
low-coordination squad would have larger position scatter and lower objective
sharing probability.

### 4.3 Probabilistic Objective Sharing

SAIN uses a 25-100% chance per member (scaled by coordination level) for
objective information to propagate. This prevents omniscient squads.

**Application**: Instead of all followers always knowing the boss's exact
objective, scale sharing probability by coordination level. A 50% coordination
squad would have some followers operating on outdated information.

### 4.4 Leader Death Cooldown

SAIN imposes a 60-second cooldown before squad restructuring during combat.
Prevents chaotic mid-fight leader changes.

**Application**: When a boss dies in combat, delay follower re-evaluation for
60 seconds. During the cooldown, followers continue their last tactical
assignment.

### 4.5 Suppress-and-Flank Tactic

SAIN's `PushSuppressedEnemy` has one member suppress while another flanks —
a genuine coordinated tactic.

**Application**: Future enhancement for combat-aware squad behavior. Out of
scope for this analysis (focused on questing coordination), but worth noting
for the future.

### 4.6 What SAIN Does NOT Have

- No zone-based spatial reasoning
- No field/gradient movement
- No squad-level objective sharing for quests
- `Roles` enum exists but is barely used in practice

---

## 5. Implementation Options

### Option A: Tactical Offset (Simplest)

**Concept**: Keep followers as pure followers. Add offset positions so followers
don't stack on top of the boss. No gate unlocking.

**Changes**:
- Add `Vector3 FollowerOffset` to `BotFollowerLayer`
- Compute offset from boss position based on follower index
- Followers walk to `bossPosition + offset` instead of `bossPosition`

**Pros**:
- Minimal code changes (~50 lines)
- No gate unlocking risk
- Easy to understand and debug

**Cons**:
- Followers still look like followers — no tactical behavior
- No quest-type awareness (same offset for Ambush vs PlantItem)
- No utility scoring for tactical transitions
- Doesn't address the core problem: followers are passive

**Estimated effort**: 1-2 days, ~10 tests

### Option B: Squad Strategy Layer (Recommended)

**Concept**: Add `SquadRole` and tactical positions on `BotEntity`, partially
unlock gates for squad-derived objectives, add squad utility tasks for
tactical positioning.

**Changes**:
- New ECS fields on `BotEntity` (SquadRole, TacticalPosition, etc.)
- New pure-logic classes: `SquadRole` enum, `TacticalPositionCalculator`, `SquadStrategySystem`
- New squad utility tasks: `GoToTacticalPositionTask`, `HoldTacticalPositionTask`
- Conditional gate unlocking (all three gates)
- Quest-type-aware role assignment
- Config: `squad_strategy` section under `questing`

**Pros**:
- Natural tactical behavior for followers
- Quest-type-aware positioning (different roles for different objectives)
- Reuses existing utility AI framework
- Incremental — each phase works independently
- Backward-compatible (config-gated, off by default initially)
- Builds on existing ECS architecture

**Cons**:
- Moderate complexity (~500 lines new code)
- Gate unlocking requires careful testing
- Boss objective sync must be reliable

**Estimated effort**: 5 phases, ~120 tests

### Option C: Full Phobos Port (Most Complex)

**Concept**: Introduce a separate `Squad` entity type with its own utility AI
level. Full two-level utility scoring: `StrategyManager` for squads,
`ActionManager` for agents.

**Changes**:
- New `Squad` entity type in BotRegistry
- `SquadRegistry` mapping BSG groups to squad entities
- `StrategyManager` with `TimePacing` (0.5s)
- Separate `ActionManager` with per-frame scoring
- Restructure PhobosManager-style tick orchestration
- Port `GotoObjectiveStrategy` and `GuardAction`

**Pros**:
- Full Phobos feature parity
- Clean two-level architecture
- Proven in Phobos

**Cons**:
- Major restructure of existing architecture
- Duplicates existing `UtilityTaskManager` (would need refactoring to share)
- `Squad` as a separate entity type conflicts with QuestingBots' flat `BotEntity` model
- Risk of breaking existing behavior during migration
- Phobos's two-action model (GotoObjective + Guard) is simpler than QuestingBots' 8-task model
- Overkill: Phobos has one strategy because squads share zones, not quests

**Estimated effort**: 3-4 weeks, major restructure

### Comparison Table

| Aspect | Option A: Offset | **Option B: Strategy Layer** | Option C: Full Port |
|--------|-----------------|-------------------------------|---------------------|
| **Follower behavior** | Offset only | Tactical roles + positions | Full Phobos parity |
| **Quest awareness** | None | Yes (per quest type) | Yes (zone-based) |
| **Gate changes** | None | Partial unlock (3 gates) | Full restructure |
| **Utility AI** | None | 2 new squad tasks | Separate strategy level |
| **New entity types** | None | None (fields on BotEntity) | Squad entity |
| **Breaking risk** | Minimal | Low (config-gated) | High |
| **Code volume** | ~50 lines | ~500 lines | ~1500+ lines |
| **Test count** | ~10 | ~120 | ~200+ |
| **Complexity** | Trivial | Moderate | High |

---

## 6. Recommended Approach: Option B

### 6.1 Design Overview

Option B adds squad strategy as new fields and systems operating on the existing
`BotEntity`, without introducing new entity types. The core idea:

1. Boss picks a quest (existing behavior, unchanged)
2. `SquadStrategySystem` observes boss's objective and computes tactical positions for each follower
3. Followers receive `SquadRole` + `TacticalPosition` on their `BotEntity`
4. Gates are conditionally unlocked for followers with tactical assignments
5. New squad utility tasks (`GoToTacticalPositionTask`, `HoldTacticalPositionTask`) score high for followers with tactical positions
6. BigBrain executes the movement via existing `CustomLogic` actions

```
Boss picks quest (existing)
     │
     ▼
SquadStrategySystem.UpdateSquad(boss, followers) [HiveMind tick, 50ms]
     │
     ├── Read boss objective position + quest type
     │
     ├── Compute per-follower tactical positions
     │   └── TacticalPositionCalculator.ComputePositions(bossPos, questType, followers)
     │       ├── Ambush quest → flanking positions (120° spread)
     │       ├── Snipe quest → elevated overwatch positions
     │       ├── PlantItem quest → guard positions (perimeter)
     │       ├── HoldAtPosition → spread around boss position
     │       └── MoveToPosition → trail positions (behind boss)
     │
     ├── Assign roles based on follower capabilities
     │   └── SquadRole: Guard, Flanker, Overwatch, Escort
     │
     └── Write to each follower's BotEntity:
         ├── entity.SquadRole = computed role
         ├── entity.TacticalPosition = computed position (Vector3-as-floats)
         ├── entity.HasTacticalPosition = true
         └── entity.SquadObjectiveVersion = boss's current version
              │
              ▼
Gate unlock: BotObjectiveLayer.IsActive() allows followers WITH tactical positions
              │
              ▼
UtilityTaskManager.ScoreAndPick(followerEntity)
     │
     ├── GoToTacticalPositionTask scores 0.70 when far from tactical position
     ├── HoldTacticalPositionTask scores 0.65 when at tactical position
     ├── (existing tasks score 0 for followers without active quests)
     │
     ▼
BigBrain dispatches GoToObjective/HoldPosition action
     │
     └── CustomMoverController drives movement to tactical position
```

### 6.2 New ECS Fields on BotEntity

Add these fields to `BotEntity` (`src/SPTQuestingBots.Client/BotLogic/ECS/BotEntity.cs`):

```csharp
// ── Squad Strategy State ────────────────────────────────

/// <summary>
/// This follower's tactical role in the current squad strategy.
/// Default: SquadRole.None (no tactical assignment).
/// </summary>
public SquadRole SquadRole;

/// <summary>
/// X component of the tactical position assigned by SquadStrategySystem.
/// Stored as individual floats to avoid Unity Vector3 dependency.
/// </summary>
public float TacticalPositionX;

/// <summary>
/// Y component of the tactical position.
/// </summary>
public float TacticalPositionY;

/// <summary>
/// Z component of the tactical position.
/// </summary>
public float TacticalPositionZ;

/// <summary>
/// Whether this follower has a valid tactical position to move to.
/// Reset when boss changes objective or follower is separated.
/// </summary>
public bool HasTacticalPosition;

/// <summary>
/// Version counter of the boss's objective that this follower last synced to.
/// When boss.SquadObjectiveVersion > follower.LastSeenObjectiveVersion,
/// the follower needs new tactical positions.
/// </summary>
public int LastSeenObjectiveVersion;

/// <summary>
/// Version counter of this bot's current objective. Incremented when the
/// boss (or solo bot) changes quest objective. Used for squad sync.
/// </summary>
public int SquadObjectiveVersion;
```

**Why individual floats instead of Vector3?**: `BotEntity` is pure C# with zero
Unity dependencies. Using `float` fields preserves testability in net9.0 tests
where `UnityEngine.Vector3` is unavailable. Integration code converts at the
boundary.

### 6.3 SquadRole Enum

New file: `src/SPTQuestingBots.Client/BotLogic/ECS/SquadRole.cs`

```csharp
namespace SPTQuestingBots.BotLogic.ECS
{
    /// <summary>
    /// Tactical role assigned to a follower by SquadStrategySystem.
    /// Determines the type of position derivation and behavior at the position.
    /// </summary>
    public enum SquadRole : byte
    {
        /// <summary>No tactical assignment — default follower behavior.</summary>
        None = 0,

        /// <summary>Hold a guard position near the boss's objective perimeter.</summary>
        Guard = 1,

        /// <summary>Move to a flanking position offset from the boss's approach direction.</summary>
        Flanker = 2,

        /// <summary>Take an elevated or distant overwatch position with line of sight.</summary>
        Overwatch = 3,

        /// <summary>Trail behind the boss at a fixed offset distance.</summary>
        Escort = 4,
    }
}
```

### 6.4 TacticalPositionCalculator

New file: `src/SPTQuestingBots.Client/BotLogic/ECS/Systems/TacticalPositionCalculator.cs`

Pure-logic class with zero Unity dependencies. Takes boss position (as floats),
quest type, follower count, and returns tactical positions.

**Method signatures**:

```csharp
public static class TacticalPositionCalculator
{
    /// <summary>
    /// Compute tactical positions for all followers based on boss objective.
    /// </summary>
    /// <param name="bossX">Boss objective X position.</param>
    /// <param name="bossY">Boss objective Y position.</param>
    /// <param name="bossZ">Boss objective Z position.</param>
    /// <param name="bossApproachX">X direction boss is approaching from.</param>
    /// <param name="bossApproachZ">Z direction boss is approaching from.</param>
    /// <param name="questAction">Current quest action type (QuestActionId constants).</param>
    /// <param name="followerCount">Number of followers to assign positions.</param>
    /// <param name="outPositions">Pre-allocated buffer: [x0,y0,z0, x1,y1,z1, ...]</param>
    /// <param name="outRoles">Pre-allocated buffer: role per follower.</param>
    /// <param name="config">Strategy configuration.</param>
    public static void ComputePositions(
        float bossX, float bossY, float bossZ,
        float bossApproachX, float bossApproachZ,
        int questAction,
        int followerCount,
        float[] outPositions,
        SquadRole[] outRoles,
        SquadStrategyConfig config);

    /// <summary>
    /// Compute a single guard position on the perimeter of the objective.
    /// </summary>
    public static void ComputeGuardPosition(
        float centerX, float centerY, float centerZ,
        float angleRadians, float radius,
        out float outX, out float outY, out float outZ);

    /// <summary>
    /// Compute a flanking position offset from the approach direction.
    /// </summary>
    public static void ComputeFlankPosition(
        float centerX, float centerY, float centerZ,
        float approachX, float approachZ,
        float flankAngleRadians, float distance,
        out float outX, out float outY, out float outZ);

    /// <summary>
    /// Compute an overwatch position behind and elevated from the objective.
    /// </summary>
    public static void ComputeOverwatchPosition(
        float centerX, float centerY, float centerZ,
        float approachX, float approachZ,
        float distance, float elevationOffset,
        out float outX, out float outY, out float outZ);

    /// <summary>
    /// Compute an escort position trailing the boss.
    /// </summary>
    public static void ComputeEscortPosition(
        float bossX, float bossY, float bossZ,
        float approachX, float approachZ,
        float trailDistance, float lateralOffset,
        out float outX, out float outY, out float outZ);
}
```

**Quest-type-aware role assignment table**:

| Quest Action | Role 1 | Role 2 | Role 3 | Role 4+ |
|-------------|--------|--------|--------|---------|
| `MoveToPosition` | Escort | Escort | Flanker | Escort |
| `HoldAtPosition` | Guard (90°) | Guard (180°) | Guard (270°) | Guard (evenly spaced) |
| `Ambush` | Flanker (left) | Flanker (right) | Overwatch | Guard |
| `Snipe` | Overwatch (near) | Guard (left) | Guard (right) | Guard |
| `PlantItem` | Guard (front) | Guard (rear) | Flanker | Guard |
| `ToggleSwitch` | Guard (near) | Escort | Escort | Escort |
| `CloseNearbyDoors` | Escort | Escort | Escort | Escort |

**Position parameters**:

| Role | Distance from Objective | Spread Pattern |
|------|------------------------|----------------|
| Guard | 8-15m | Evenly spaced on perimeter circle |
| Flanker | 10-20m | 60-120° offset from approach direction |
| Overwatch | 15-30m | Behind approach direction, +2m elevation offset |
| Escort | 5-10m | Behind boss, alternating left/right lateral offset |

### 6.5 SquadStrategySystem

New file: `src/SPTQuestingBots.Client/BotLogic/ECS/Systems/SquadStrategySystem.cs`

Static system methods operating on dense entity lists (Phobos pattern).

**Method signatures**:

```csharp
public static class SquadStrategySystem
{
    /// <summary>
    /// Update tactical positions for all squads.
    /// Called from BotHiveMindMonitor.Update() after boss/follower sync.
    /// Iterates dense entity list; only processes active entities with followers.
    /// </summary>
    /// <param name="entities">Dense entity list from BotRegistry.</param>
    /// <param name="config">Squad strategy configuration.</param>
    public static void UpdateSquads(
        List<BotEntity> entities,
        SquadStrategyConfig config);

    /// <summary>
    /// Update tactical positions for a single boss's followers.
    /// Called when boss's objective changes (version mismatch detected).
    /// </summary>
    /// <param name="boss">The boss entity (must have followers).</param>
    /// <param name="config">Squad strategy configuration.</param>
    public static void UpdateSquad(
        BotEntity boss,
        SquadStrategyConfig config);

    /// <summary>
    /// Clear tactical positions for all followers of a boss.
    /// Called when boss dies, loses followers, or changes to an incompatible quest.
    /// </summary>
    public static void ClearSquadPositions(BotEntity boss);

    /// <summary>
    /// Clear tactical position for a single follower.
    /// Called when follower is separated from group or dies.
    /// </summary>
    public static void ClearFollowerPosition(BotEntity follower);
}
```

**Update logic**:

```
SquadStrategySystem.UpdateSquads(entities, config):
    for each entity in entities:
        if not entity.IsActive → skip
        if entity.Followers.Count == 0 → skip
        if entity.Boss != null → skip (only process top-level bosses)

        // Check if boss objective changed
        for each follower in entity.Followers:
            if follower.LastSeenObjectiveVersion < entity.SquadObjectiveVersion:
                UpdateSquad(entity, config)
                break

SquadStrategySystem.UpdateSquad(boss, config):
    // Read boss objective (via BotEntityBridge → BotObjectiveManager)
    bossObjectivePos = BotEntityBridge.GetObjectivePosition(boss)
    if bossObjectivePos is null → ClearSquadPositions(boss); return

    bossPos = BotEntityBridge.GetBotPosition(boss)
    questAction = boss.CurrentQuestAction
    followerCount = boss.Followers.Count

    // Compute approach direction (boss → objective)
    approachX = bossObjectivePos.x - bossPos.x
    approachZ = bossObjectivePos.z - bossPos.z
    // normalize...

    // Compute tactical positions
    TacticalPositionCalculator.ComputePositions(
        bossObjectivePos.x, bossObjectivePos.y, bossObjectivePos.z,
        approachX, approachZ,
        questAction, followerCount,
        positionBuffer, roleBuffer, config)

    // Write to each follower
    for i = 0 to followerCount-1:
        follower = boss.Followers[i]
        follower.SquadRole = roleBuffer[i]
        follower.TacticalPositionX = positionBuffer[i*3]
        follower.TacticalPositionY = positionBuffer[i*3 + 1]
        follower.TacticalPositionZ = positionBuffer[i*3 + 2]
        follower.HasTacticalPosition = true
        follower.LastSeenObjectiveVersion = boss.SquadObjectiveVersion
```

### 6.6 Squad Utility Tasks

Two new tasks extending `QuestUtilityTask`:

**GoToTacticalPositionTask**:

```csharp
// Score: 0.70 when follower has tactical position and is far from it
// Score: 0.0 when at tactical position or no tactical position assigned
// Hysteresis: 0.20
// BotActionTypeId: GoToObjective (reuses existing movement action)
// ActionReason: "GoToTacticalPosition"

public override void ScoreEntity(int ordinal, BotEntity entity)
{
    if (!entity.HasTacticalPosition || !entity.HasBoss)
    {
        entity.TaskScores[ordinal] = 0f;
        return;
    }

    float dx = entity.TacticalPositionX - /* current pos from bridge */;
    float dz = entity.TacticalPositionZ - /* current pos from bridge */;
    float sqrDist = dx * dx + dz * dz;

    // Close threshold: 3m (9 sqrDist)
    if (sqrDist < 9f)
    {
        entity.TaskScores[ordinal] = 0f;
        return;
    }

    entity.TaskScores[ordinal] = 0.70f;
}
```

**HoldTacticalPositionTask**:

```csharp
// Score: 0.65 when follower is at tactical position
// Score: 0.0 when far from tactical position or no assignment
// Hysteresis: 0.10
// BotActionTypeId: HoldPosition (reuses existing hold action)
// ActionReason: "HoldTacticalPosition"

public override void ScoreEntity(int ordinal, BotEntity entity)
{
    if (!entity.HasTacticalPosition || !entity.HasBoss)
    {
        entity.TaskScores[ordinal] = 0f;
        return;
    }

    float dx = entity.TacticalPositionX - /* current pos */;
    float dz = entity.TacticalPositionZ - /* current pos */;
    float sqrDist = dx * dx + dz * dz;

    // Arrival threshold: 3m (9 sqrDist)
    if (sqrDist >= 9f)
    {
        entity.TaskScores[ordinal] = 0f;
        return;
    }

    entity.TaskScores[ordinal] = 0.65f;
}
```

**Task factory update**: `QuestTaskFactory.TaskCount` becomes 10 (add 2 new tasks).

**Scoring comparison with existing tasks**:

| Task | Base Score | Hysteresis | When Active |
|------|-----------|-----------|-------------|
| GoToObjective | 0.65 | 0.25 | Has active quest, far from objective |
| **GoToTacticalPosition** | **0.70** | **0.20** | Follower with tactical pos, far |
| **HoldTacticalPosition** | **0.65** | **0.10** | Follower with tactical pos, at pos |
| HoldPosition | 0.70 | 0.10 | Quest says hold |
| Ambush | 0.65 | 0.15 | Quest says ambush, close |
| Snipe | 0.65 | 0.15 | Quest says snipe, close |

The GoToTacticalPosition score (0.70) is higher than GoToObjective (0.65) because
followers with tactical assignments should prioritize them over any independently
assigned quest objective (which they normally wouldn't have anyway).

### 6.7 Gate Unlocking Strategy

All three gates must be conditionally opened for followers with tactical
positions. The key condition: `entity.HasTacticalPosition == true`.

**Gate 1 — `BotObjectiveLayer.IsActive()`**:

```csharp
// BEFORE (line 53-56):
if (decisionMonitor.HasAQuestingBoss)
{
    return updatePreviousState(false);
}

// AFTER:
if (decisionMonitor.HasAQuestingBoss)
{
    // Allow followers with tactical positions to use the questing layer
    if (QuestingBotsPluginConfig.SquadStrategyEnabled.Value
        && BotEntityBridge.HasTacticalPosition(BotOwner))
    {
        // Fall through to utility AI scoring — tactical tasks will score high
    }
    else
    {
        return updatePreviousState(false);
    }
}
```

**Gate 2 — `getFollowerDecision()`**:

```csharp
// BEFORE (line 102-104):
private BotQuestingDecision getFollowerDecision()
{
    Controllers.BotJobAssignmentFactory.InactivateAllJobAssignmentsForBot(BotOwner.Profile.Id);
    // ... priority chain ending at FollowBoss/None

// AFTER:
private BotQuestingDecision getFollowerDecision()
{
    // Only kill quest assignments if follower has no tactical position
    if (!QuestingBotsPluginConfig.SquadStrategyEnabled.Value
        || !BotEntityBridge.HasTacticalPosition(BotOwner))
    {
        Controllers.BotJobAssignmentFactory.InactivateAllJobAssignmentsForBot(BotOwner.Profile.Id);
    }

    // ... existing priority chain ...

    // Before FollowBoss, check for tactical position
    if (QuestingBotsPluginConfig.SquadStrategyEnabled.Value
        && BotEntityBridge.HasTacticalPosition(BotOwner))
    {
        return BotQuestingDecision.Quest;  // NEW: allows questing layer activation
    }

    // ... existing FollowBoss / None logic
```

**Gate 3 — `BotObjectiveManager.Update()`**:

This gate does NOT need full unlocking. The objective manager monitors the bot's
quest assignment progress, but followers with tactical positions don't use the
standard quest assignment pipeline. Instead, the utility AI tasks
(`GoToTacticalPositionTask`, `HoldTacticalPositionTask`) read directly from
`BotEntity` fields. Gate 3 can remain closed.

However, followers with tactical positions need their `BotEntity` quest state
synced for utility scoring. This is handled by adding a new sync path in
`BotEntityBridge.SyncTacticalState()` that reads from `BotEntity` directly
(no `BotObjectiveManager` dependency).

### 6.8 SquadStrategyConfig

New config section under `questing.squad_strategy` in `config.json`:

```csharp
public class SquadStrategyConfig
{
    /// <summary>Master toggle for squad tactical positioning.</summary>
    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = false;  // Off by default during development

    /// <summary>Base guard distance from objective (meters).</summary>
    [JsonProperty("guard_distance")]
    public float GuardDistance { get; set; } = 12f;

    /// <summary>Flanking offset distance from approach direction (meters).</summary>
    [JsonProperty("flank_distance")]
    public float FlankDistance { get; set; } = 15f;

    /// <summary>Overwatch distance behind objective (meters).</summary>
    [JsonProperty("overwatch_distance")]
    public float OverwatchDistance { get; set; } = 25f;

    /// <summary>Escort trail distance behind boss (meters).</summary>
    [JsonProperty("escort_distance")]
    public float EscortDistance { get; set; } = 8f;

    /// <summary>
    /// Arrival radius — how close a follower must be to their tactical position
    /// to be considered "arrived" (meters). Squared internally.
    /// </summary>
    [JsonProperty("arrival_radius")]
    public float ArrivalRadius { get; set; } = 3f;

    /// <summary>
    /// Maximum time (seconds) a follower will hold a tactical position before
    /// reverting to standard follow behavior. Prevents indefinite camping.
    /// </summary>
    [JsonProperty("max_hold_time")]
    public float MaxHoldTime { get; set; } = 120f;

    /// <summary>
    /// Maximum distance from boss at which tactical positions are valid.
    /// Beyond this range, followers revert to standard follow behavior.
    /// </summary>
    [JsonProperty("max_distance_from_boss")]
    public float MaxDistanceFromBoss { get; set; } = 100f;
}
```

**F12 menu entry**:

| Entry | Default | Description |
|-------|---------|-------------|
| `SquadStrategyEnabled` | `false` | Runtime toggle for squad tactical positioning |

### 6.9 Integration with Existing Systems

**HiveMind tick** — Add `SquadStrategySystem.UpdateSquads()` call:

```
BotHiveMindMonitor.Update() tick order (50ms):
─────────────────────────────────────────────────
1. updateBosses()                    — existing
2. updateBossFollowers()             — existing
3. SquadStrategySystem.UpdateSquads() — NEW (after boss/follower sync)
4. updatePullSensors()               — existing
5. ResetInactiveEntitySensors()      — existing
```

**BotEntityBridge** — New methods:

```csharp
/// <summary>Read whether a bot has a tactical position.</summary>
public static bool HasTacticalPosition(BotOwner bot);

/// <summary>Read the tactical position as a Unity Vector3 (integration layer).</summary>
public static Vector3 GetTacticalPosition(BotOwner bot);

/// <summary>Read the bot's squad role.</summary>
public static SquadRole GetSquadRole(BotOwner bot);

/// <summary>
/// Increment the boss's objective version counter.
/// Called when BotObjectiveManager changes assignments.
/// </summary>
public static void IncrementSquadObjectiveVersion(BotOwner bot);
```

**BotObjectiveManager** — Trigger version increment:

Add `BotEntityBridge.IncrementSquadObjectiveVersion(botOwner)` in:
- `SetObjective()` / assignment changes
- `CompleteObjective()` / `FailObjective()`

**Utility AI** — Update `QuestTaskFactory`:

```csharp
public const int TaskCount = 10;  // was 8

public static UtilityTaskManager Create()
{
    return new UtilityTaskManager(new UtilityTask[]
    {
        new Tasks.GoToObjectiveTask(),
        new Tasks.AmbushTask(),
        new Tasks.SnipeTask(),
        new Tasks.HoldPositionTask(),
        new Tasks.PlantItemTask(),
        new Tasks.UnlockDoorTask(),
        new Tasks.ToggleSwitchTask(),
        new Tasks.CloseDoorsTask(),
        new Tasks.GoToTacticalPositionTask(),   // NEW
        new Tasks.HoldTacticalPositionTask(),   // NEW
    });
}
```

**BigBrain action dispatch** — No changes needed. Both new tasks map to existing
`BotActionTypeId` values (`GoToObjective` and `HoldPosition`), reusing existing
`CustomLogic` implementations. The difference is that the movement target comes
from `BotEntity.TacticalPosition*` instead of `BotObjectiveManager.Position`.

This requires a small modification to `GoToObjectiveAction` and
`HoldPositionAction` to read from tactical position when `HasTacticalPosition`
is true:

```csharp
// In GoToObjectiveAction / GoToPositionAbstractAction:
Vector3 target;
if (BotEntityBridge.HasTacticalPosition(BotOwner))
    target = BotEntityBridge.GetTacticalPosition(BotOwner);
else
    target = objectiveManager.Position.Value;
```

---

## 7. Integration Points

### 7.1 Files to Modify

| File | Change | Risk |
|------|--------|------|
| `BotLogic/ECS/BotEntity.cs` | Add 7 new fields | Low — additive only |
| `BotLogic/ECS/BotEntityBridge.cs` | Add 4-6 new bridge methods | Low — additive |
| `BotLogic/Objective/BotObjectiveLayer.cs:53-56` | Conditional gate unlock | Medium — behavior change |
| `BotLogic/BotMonitor/Monitors/BotQuestingDecisionMonitor.cs:102-168` | Conditional follower quest decision | Medium — behavior change |
| `BotLogic/HiveMind/BotHiveMindMonitor.cs` | Add SquadStrategySystem call in Update() | Low — additive |
| `BotLogic/ECS/UtilityAI/QuestTaskFactory.cs` | Add 2 new tasks, update TaskCount | Low |
| `BehaviorExtensions/GoToPositionAbstractAction.cs` | Read tactical position when available | Medium |
| `Components/BotObjectiveManager.cs` | Trigger version increment on objective change | Low |
| `Configuration/ModConfig.cs` | Add SquadStrategyConfig | Low |
| `Configuration/QuestingBotsPluginConfig.cs` | Add F12 toggle | Low |

### 7.2 Files to Create

| File | Description |
|------|-------------|
| `BotLogic/ECS/SquadRole.cs` | SquadRole enum (5 values) |
| `BotLogic/ECS/Systems/TacticalPositionCalculator.cs` | Pure-logic position computation |
| `BotLogic/ECS/Systems/SquadStrategySystem.cs` | Static system for squad updates |
| `BotLogic/ECS/UtilityAI/Tasks/GoToTacticalPositionTask.cs` | Movement to tactical position |
| `BotLogic/ECS/UtilityAI/Tasks/HoldTacticalPositionTask.cs` | Hold at tactical position |
| `Configuration/SquadStrategyConfig.cs` | Config struct |

### 7.3 Integration with BotObjectiveManager

The `BotObjectiveManager` owns the bot's current quest assignment. For
squad strategy to work, the system needs to know when the boss's objective
changes. The cleanest integration point is to increment
`BotEntity.SquadObjectiveVersion` whenever the boss's assignment changes.

Key locations in `BotObjectiveManager`:
- Assignment starts: increment version
- Assignment completes: increment version
- Assignment fails: increment version

`SquadStrategySystem` detects version mismatches during its update and
recomputes tactical positions only when needed (not every tick).

### 7.4 Integration with Custom Movement System

Followers with tactical positions use the same movement infrastructure as
bosses. The `CustomMoverController` drives movement to the target position.
The only difference is where the target position comes from:

- Bosses: `BotObjectiveManager.Position`
- Followers with tactical positions: `BotEntity.TacticalPosition*`

The `GoToPositionAbstractAction.GetObjectivePosition()` method is the single
point where this fork happens. Both paths use the same path calculation,
smoothing, stuck detection, and Player.Move() execution.

---

## 8. New Classes

### 8.1 SquadRole

```
File: src/SPTQuestingBots.Client/BotLogic/ECS/SquadRole.cs
Type: enum (byte)
Values: None=0, Guard=1, Flanker=2, Overwatch=3, Escort=4
Dependencies: None (pure C#)
Tests: Verified through TacticalPositionCalculator tests
```

### 8.2 TacticalPositionCalculator

```
File: src/SPTQuestingBots.Client/BotLogic/ECS/Systems/TacticalPositionCalculator.cs
Type: static class
Dependencies: None (pure C# — no Unity, no EFT)
Methods:
  - ComputePositions(bossX/Y/Z, approachX/Z, questAction, followerCount, outPositions, outRoles, config)
  - ComputeGuardPosition(centerX/Y/Z, angle, radius, out x/y/z)
  - ComputeFlankPosition(centerX/Y/Z, approachX/Z, flankAngle, distance, out x/y/z)
  - ComputeOverwatchPosition(centerX/Y/Z, approachX/Z, distance, elevation, out x/y/z)
  - ComputeEscortPosition(bossX/Y/Z, approachX/Z, trailDist, lateralOffset, out x/y/z)
Tests: ~25 (all position geometry, quest-type-aware assignment, edge cases)
```

### 8.3 SquadStrategySystem

```
File: src/SPTQuestingBots.Client/BotLogic/ECS/Systems/SquadStrategySystem.cs
Type: static class
Dependencies: BotEntity, TacticalPositionCalculator, SquadStrategyConfig
Methods:
  - UpdateSquads(entities, config) — iterate dense list, update squads with version mismatch
  - UpdateSquad(boss, config) — recompute tactical positions for one boss's followers
  - ClearSquadPositions(boss) — reset tactical positions for all followers
  - ClearFollowerPosition(follower) — reset one follower's tactical position
Tests: ~15 (update logic, version tracking, clear on death, boss without followers)
```

### 8.4 GoToTacticalPositionTask

```
File: src/SPTQuestingBots.Client/BotLogic/ECS/UtilityAI/Tasks/GoToTacticalPositionTask.cs
Type: sealed class : QuestUtilityTask
Hysteresis: 0.20
Base score: 0.70
BotActionTypeId: GoToObjective (reuses existing movement)
ActionReason: "GoToTacticalPosition"
Scoring: 0.70 when HasTacticalPosition && far from position, else 0
Tests: ~10 (scoring logic, no tactical position, at position, no boss)
```

### 8.5 HoldTacticalPositionTask

```
File: src/SPTQuestingBots.Client/BotLogic/ECS/UtilityAI/Tasks/HoldTacticalPositionTask.cs
Type: sealed class : QuestUtilityTask
Hysteresis: 0.10
Base score: 0.65
BotActionTypeId: HoldPosition (reuses existing hold)
ActionReason: "HoldTacticalPosition"
Scoring: 0.65 when HasTacticalPosition && at position, else 0
Tests: ~10 (scoring logic, transitions, hold timeout)
```

### 8.6 SquadStrategyConfig

```
File: src/SPTQuestingBots.Client/Configuration/SquadStrategyConfig.cs
Type: class (JSON-serializable)
Properties: Enabled, GuardDistance, FlankDistance, OverwatchDistance,
            EscortDistance, ArrivalRadius, MaxHoldTime, MaxDistanceFromBoss
Tests: ~5 (deserialization, defaults)
```

---

## 9. Migration Path

### 9.1 Backward Compatibility

The entire feature is config-gated:

1. `squad_strategy.enabled` defaults to `false` in JSON config
2. `SquadStrategyEnabled` BepInEx toggle defaults to `false`
3. Both must be true for any behavioral change

When disabled:
- `SquadStrategySystem.UpdateSquads()` returns immediately
- Gate 1 remains closed (existing behavior)
- Gate 2 kills follower assignments (existing behavior)
- Gate 3 blocks objective monitoring (existing behavior)
- No `BotEntity` fields are written (except defaults)
- New utility tasks score 0 for all entities

### 9.2 Incremental Rollout

Each phase can be deployed independently:

1. **Phase 1 (pure logic)**: Deploy with feature disabled. No behavioral changes.
   Tests verify all position calculations.
2. **Phase 2 (ECS wiring)**: Deploy with feature disabled. New fields exist but
   are never written. Tests verify bridge methods.
3. **Phase 3 (gate unlocking)**: Deploy with feature disabled. Gate logic
   has the conditional paths but they never activate.
4. **Phase 4 (utility tasks)**: Deploy with feature disabled. New tasks exist
   in the factory but score 0 for all entities.
5. **Enable**: Flip config to `true`. All phases activate together.

### 9.3 Rollback

Setting `squad_strategy.enabled` back to `false` immediately reverts all
behavior. No data migration needed — `BotEntity` fields are reset on
`SquadStrategySystem.ClearSquadPositions()` and on entity removal.

### 9.4 Interaction with Existing Features

| Existing Feature | Interaction |
|-----------------|-------------|
| **Sleeping system** | No conflict — sleeping checks happen at priority 99, before any squad logic |
| **Custom movement** | Followers use the same `CustomMoverController` for tactical movement |
| **Zone movement** | No conflict — zone quests are low-priority fallback, and followers don't get zone assignments |
| **Utility AI** | Extends existing framework — 2 new tasks added to the same `UtilityTaskManager` |
| **Follower regroup** | Priority 26 > questing priority 18. Regroup still takes precedence when boss is in combat. |
| **HiveMind sensors** | Squad strategy reads existing sensors (InCombat, IsSuspicious) to decide when to clear tactical positions |

---

## 10. Testing Strategy

### 10.1 Pure Logic Tests (Phases 1)

All position calculation and role assignment logic is pure C# with no Unity
dependencies. Tests run in net9.0 with the existing Vector3 test shim.

**TacticalPositionCalculator tests (~25)**:
- Guard positions: evenly spaced on perimeter circle
- Flank positions: correct angular offset from approach direction
- Overwatch positions: behind approach with elevation offset
- Escort positions: trail behind boss with lateral offset
- Quest-type-aware: Ambush→Flanker, Snipe→Overwatch, PlantItem→Guard
- Edge cases: single follower, max followers (6+), zero approach vector
- Config variation: different distances and radii

### 10.2 System Tests (Phase 2)

**SquadStrategySystem tests (~15)**:
- UpdateSquads: only processes bosses with followers
- UpdateSquad: version mismatch triggers recomputation
- ClearSquadPositions: resets all follower fields
- ClearFollowerPosition: resets single follower
- Boss without objective: clears positions
- Boss death: clears positions
- Follower death: removed from tactical assignment
- Version tracking: no recomputation when versions match

### 10.3 ECS Wiring Tests (Phase 2)

**BotEntityBridge squad methods (~10)**:
- HasTacticalPosition: returns correct state
- GetTacticalPosition: returns position as Vector3
- GetSquadRole: returns correct role
- IncrementSquadObjectiveVersion: increments and propagates
- Integration: register bot → assign tactical → read back

### 10.4 Gate Unlocking Tests (Phase 3)

These tests verify the conditional gate logic. Since they depend on game types
(`BotOwner`, `BotObjectiveLayer`), they may need to be tested via integration
testing or NSubstitute mocks.

**Gate logic tests (~15)**:
- Gate 1: follower with tactical position → layer returns true
- Gate 1: follower without tactical position → layer returns false (existing)
- Gate 1: feature disabled → layer returns false (existing)
- Gate 2: follower with tactical position → assignments not killed
- Gate 2: follower without tactical position → assignments killed (existing)
- Gate 2: follower with tactical position → decision returns Quest
- Gate 2: combat override still takes priority over tactical position
- Gate 3: no changes needed (tested by absence)

### 10.5 Utility Task Tests (Phase 4)

**GoToTacticalPositionTask tests (~10)**:
- Score 0.70 when has tactical position and far
- Score 0.0 when no tactical position
- Score 0.0 when at tactical position (close)
- Score 0.0 when no boss
- Hysteresis prevents flip-flop with GoToObjective
- Transition to HoldTacticalPosition when arriving

**HoldTacticalPositionTask tests (~10)**:
- Score 0.65 when has tactical position and close
- Score 0.0 when no tactical position
- Score 0.0 when far from tactical position
- Score 0.0 when no boss
- Hysteresis prevents flip-flop with GoToTacticalPosition

**QuestTaskFactory tests (~5)**:
- TaskCount == 10
- All 10 tasks created
- New tasks at correct indices

### 10.6 SAIN-Inspired Enhancement Tests (Phase 5)

**Communication range tests (~8)**:
- Follower within range receives tactical position
- Follower beyond range does not receive tactical position
- Range scales with equipment (earpiece vs none)

**Squad personality tests (~7)**:
- Coordination level affects position scatter
- Aggression affects role preference (more flankers vs more guards)
- Personality derived from member composition

### 10.7 Test Summary

| Phase | Test Category | Count |
|-------|-------------|-------|
| 1 | TacticalPositionCalculator | ~25 |
| 1 | SquadStrategySystem | ~15 |
| 2 | BotEntity new fields | ~5 |
| 2 | BotEntityBridge squad methods | ~10 |
| 3 | Gate logic | ~15 |
| 4 | GoToTacticalPositionTask | ~10 |
| 4 | HoldTacticalPositionTask | ~10 |
| 4 | QuestTaskFactory | ~5 |
| 5 | Communication range | ~8 |
| 5 | Squad personality | ~7 |
| **Total** | | **~115** |

---

## 11. Phased Implementation Plan

### Phase 1: Pure Logic (~40 tests)

**Scope**: Create all pure-logic classes with zero Unity dependencies. Everything
is testable in net9.0.

**New files**:
- `src/SPTQuestingBots.Client/BotLogic/ECS/SquadRole.cs`
- `src/SPTQuestingBots.Client/BotLogic/ECS/Systems/TacticalPositionCalculator.cs`
- `src/SPTQuestingBots.Client/BotLogic/ECS/Systems/SquadStrategySystem.cs`
- `src/SPTQuestingBots.Client/Configuration/SquadStrategyConfig.cs`
- `tests/SPTQuestingBots.Client.Tests/ECS/Systems/TacticalPositionCalculatorTests.cs`
- `tests/SPTQuestingBots.Client.Tests/ECS/Systems/SquadStrategySystemTests.cs`
- `tests/SPTQuestingBots.Client.Tests/Configuration/SquadStrategyConfigTests.cs`

**Test count**: ~40 (25 calculator + 15 system)

**Risk**: Low — no behavioral changes, no existing code modified.

**Dependencies**: None.

### Phase 2: ECS Wiring (~25 tests)

**Scope**: Add new fields to `BotEntity`, new bridge methods to
`BotEntityBridge`, wire `SquadStrategySystem` into `BotHiveMindMonitor.Update()`.

**Modified files**:
- `src/SPTQuestingBots.Client/BotLogic/ECS/BotEntity.cs` — add 7 new fields
- `src/SPTQuestingBots.Client/BotLogic/ECS/BotEntityBridge.cs` — add 4-6 methods
- `src/SPTQuestingBots.Client/BotLogic/HiveMind/BotHiveMindMonitor.cs` — add system call
- `src/SPTQuestingBots.Client/Components/BotObjectiveManager.cs` — version increment

**New files**:
- `tests/SPTQuestingBots.Client.Tests/ECS/BotEntitySquadTests.cs`
- `tests/SPTQuestingBots.Client.Tests/ECS/BotEntityBridgeSquadTests.cs`

**Test count**: ~25 (5 entity + 10 bridge + 10 integration)

**Risk**: Low — additive field additions, feature gated, system call skips when disabled.

**Dependencies**: Phase 1.

### Phase 3: Gate Unlocking (~15 tests)

**Scope**: Conditionally open the three gates for followers with tactical
positions.

**Modified files**:
- `src/SPTQuestingBots.Client/BotLogic/Objective/BotObjectiveLayer.cs` — Gate 1
- `src/SPTQuestingBots.Client/BotLogic/BotMonitor/Monitors/BotQuestingDecisionMonitor.cs` — Gate 2
- `src/SPTQuestingBots.Client/Configuration/QuestingBotsPluginConfig.cs` — F12 toggle
- `src/SPTQuestingBots.Client/Configuration/ModConfig.cs` — config loading

**New files**:
- `tests/SPTQuestingBots.Client.Tests/BotLogic/GateUnlockingTests.cs`

**Test count**: ~15

**Risk**: Medium — behavioral change for followers. Mitigated by config gate
(disabled by default) and extensive testing. Combat/heal/investigate decisions
still take priority in the follower decision tree.

**Dependencies**: Phase 2.

### Phase 4: Squad Utility Tasks (~25 tests)

**Scope**: Add the two new utility tasks and wire them into the task factory.
Modify `GoToPositionAbstractAction` to read tactical position.

**New files**:
- `src/SPTQuestingBots.Client/BotLogic/ECS/UtilityAI/Tasks/GoToTacticalPositionTask.cs`
- `src/SPTQuestingBots.Client/BotLogic/ECS/UtilityAI/Tasks/HoldTacticalPositionTask.cs`
- `tests/SPTQuestingBots.Client.Tests/ECS/UtilityAI/Tasks/GoToTacticalPositionTaskTests.cs`
- `tests/SPTQuestingBots.Client.Tests/ECS/UtilityAI/Tasks/HoldTacticalPositionTaskTests.cs`

**Modified files**:
- `src/SPTQuestingBots.Client/BotLogic/ECS/UtilityAI/QuestTaskFactory.cs` — TaskCount=10, add tasks
- `src/SPTQuestingBots.Client/BehaviorExtensions/GoToPositionAbstractAction.cs` — tactical position fork

**Test count**: ~25 (10 + 10 + 5)

**Risk**: Low-Medium — new tasks score 0 when feature disabled. Movement
fork is a single `if` check with fallback to existing behavior.

**Dependencies**: Phase 3.

### Phase 5: SAIN-Inspired Enhancements (~42 tests) ✅ Done

**Scope**: Add communication-range gating, squad personality, and probabilistic
position sharing — inspired by SAIN's `SquadPersonalitySettings` and equipment-
gated communication patterns.

**New files (4 src + 3 test)**:
- `src/.../BotLogic/ECS/SquadPersonalityType.cs` — enum (None, TimmyTeam6, Rats, GigaChads, Elite)
- `src/.../BotLogic/ECS/SquadPersonalitySettings.cs` — readonly struct with CoordinationLevel, AggressionLevel, GetSharingChance()
- `src/.../BotLogic/ECS/Systems/SquadPersonalityCalculator.cs` — majority-vote personality from member BotTypes
- `src/.../BotLogic/ECS/Systems/CommunicationRange.cs` — earpiece-gated range check (AggressiveInlining)
- `tests/.../BotLogic/ECS/SquadPersonalitySettingsTests.cs`
- `tests/.../BotLogic/ECS/Systems/SquadPersonalityCalculatorTests.cs`
- `tests/.../BotLogic/ECS/Systems/CommunicationRangeTests.cs`

**Modified files (6 src + 3 test)**:
- `src/.../BotLogic/ECS/BotEntity.cs` — added `HasEarPiece` field
- `src/.../BotLogic/ECS/SquadEntity.cs` — added PersonalityType, CoordinationLevel, AggressionLevel
- `src/.../Configuration/SquadStrategyConfig.cs` — 4 new config fields
- `src/.../BotLogic/ECS/BotEntityBridge.cs` — SyncEarPiece(), ComputeSquadPersonality()
- `src/.../BotLogic/HiveMind/BotHiveMindMonitor.cs` — wired earpiece sync + personality computation
- `src/.../BotLogic/ECS/UtilityAI/GotoObjectiveStrategy.cs` — comm range + probabilistic sharing gates
- `tests/.../Configuration/SquadStrategyConfigTests.cs` — 4 new config tests
- `tests/.../BotLogic/ECS/UtilityAI/GotoObjectiveStrategyTests.cs` — 12 integration tests
- `tests/.../SPTQuestingBots.Client.Tests.csproj` — Compile Include links

**New config fields**:
- `enable_communication_range`: true (default)
- `communication_range_no_earpiece`: 35 (meters)
- `communication_range_earpiece`: 200 (meters)
- `enable_squad_personality`: true (default)

**Key design decisions**:
- SAIN formula for probabilistic sharing: `25% + CoordinationLevel × 15%`
- Personality determined by majority vote of member BotTypes with higher-enum tie-breaking
- Two gates in GotoObjectiveStrategy.AssignNewObjective(): comm range → probability roll
- Followers failing either gate get `HasTacticalPosition = false` (skip tactical positioning)

**Test count**: ~42 (10 personality settings + 10 personality calculator + 10 comm range + 4 config + 12 integration — total 42, was estimated 15)

**Risk**: Low — layered on top of existing squad strategy. Both features are
independently configurable.

**Dependencies**: Phase 4.

### Phase Summary

| Phase | Scope | New Files | Modified Files | Tests | Risk | Status |
|-------|-------|-----------|---------------|-------|------|--------|
| 1 | Pure logic | 4 src + 3 test | 0 | ~40 | Low | **Done** |
| 2 | ECS wiring | 2 test | 4 | ~25 | Low | **Done** |
| 3 | Gate unlocking | 1 test | 4 | ~15 | Medium | **Done** |
| 4 | Squad utility tasks | 4 src + 2 test | 2 | ~25 | Low-Medium | **Done** |
| 5 | SAIN enhancements | 4 src + 3 test | 6 src + 3 test | ~42 | Low | **Done** |
| **Total** | | **12 src + 11 test** | **16** | **~147** | | |

---

## 12. Future Work

### 12.1 Cover Point System — **Implemented**

Tactical positions are validated through a 3-step pipeline, with sunflower
spiral fallback when positions fail any validation stage:

**Phase 1 — NavMesh Snap**:
- **NavMesh.SamplePosition()** snaps tactical positions to walkable areas via
  `NavMeshPositionValidator.TrySnap` (configurable `navmesh_sample_radius`, default 2m)
- **SunflowerSpiral** fallback: golden-angle Vogel's formula generates candidate
  positions around the objective when the primary snap fails (ported from Phobos
  `CollectSyntheticCoverData`)

**Phase 2 — Reachability Check** (Phobos `IsReachable` pattern):
- **NavMesh.CalculatePath()** verifies a walkable path exists from objective to
  tactical position, with path length budget = directDistance × `max_path_length_multiplier`
- Prevents positions that are nearby by Euclidean distance but require long detours
- `NavMeshPositionValidator.IsReachable` — matches Phobos's `IsReachable` implementation

**Phase 3 — LOS Verification** (Overwatch role only):
- **Physics.Linecast()** checks line-of-sight from tactical position to objective
- Applied only to `SquadRole.Overwatch` — other roles don't need objective visibility
- `NavMeshPositionValidator.HasLineOfSight` — 0.5m Y offset for ground clearance

**Phase 4 — BSG Cover Voxel Integration** (Phobos `CollectBuiltinCoverData` pattern):
- **CoverPositionSource delegate**: `int(objX,objY,objZ, radius, outPositions, maxCount)`
  injected into `GotoObjectiveStrategy` — BSG covers are tried first, geometric
  computation is the fallback when fewer covers than followers are found
- **BsgCoverPointCollector** (`Helpers/BsgCoverPointCollector.cs`): Unity-side static
  class querying `BotsController.CoversData.GetVoxelesExtended()` for neighborhood
  cover data. Two-pass: Wall covers first (hard cover), then foliage/other types.
  Inner radius = 0.75 × search radius (matches Phobos).
- Voxel search range = `CeilToInt(2 × radius / 10)` (BSG voxel grid is 10×5×10 units)

**Architecture**:
- Four delegate types injected into pure-C# `GotoObjectiveStrategy`:
  `PositionValidator`, `ReachabilityValidator`, `LosValidator`, `CoverPositionSource`
- `IsPositionValid()` helper combines reachability + LOS checks, used by both
  primary validation and sunflower fallback
- **NaN sentinel**: positions that fail all validation stages are marked `float.NaN`
  and skipped during distribution (`HasTacticalPosition = false`)

Config: `enable_position_validation` (true), `navmesh_sample_radius` (2.0),
`fallback_candidate_count` (16), `fallback_search_radius` (15.0),
`enable_reachability_check` (true), `max_path_length_multiplier` (2.5),
`enable_los_check` (true), `enable_cover_position_source` (true),
`cover_search_radius` (25.0)

### 12.2 Formation Movement (**Implemented**)

Formation movement coordinates the group's en-route movement when the boss
is traveling toward an objective. Instead of each follower independently
navigating to their tactical position, they maintain formation relative
to the boss's current position and heading.

**Implementation:**
- **FormationSpeedController** (`BotLogic/ECS/Systems/FormationSpeedController.cs`):
  Pure-logic speed decisions based on distance to boss: Sprint (>30m),
  Walk (15-30m), MatchBoss (<15m), SlowApproach (<5m from tactical position).
  `ShouldSprint()` and `SpeedMultiplier()` helpers for callers.
- **FormationPositionUpdater** (`BotLogic/ECS/Systems/FormationPositionUpdater.cs`):
  Pure-logic position computation. Column (trail behind boss) and Spread
  (fan perpendicular to heading) formations. Heading computed from
  previous→current leader position delta.
- **BotEntity**: 4 new fields: `FormationSpeed`, `BossIsSprinting`,
  `DistanceToBossSqr`, `IsEnRouteFormation`.
- **SquadEntity**: 2 new fields: `PreviousLeaderX`, `PreviousLeaderZ`
  for heading computation across ticks.
- **Integration**: `BotHiveMindMonitor.updateFormationMovement()` computes
  column positions and speed decisions after squad strategy manager runs.
  `GoToObjectiveAction.Update()` overrides `CanSprint` for followers in
  en-route formation using `FormationSpeedController.ShouldSprint()`.
- **Config**: 7 new properties under `questing.squad_strategy`:
  `enable_formation_movement` (true), `catch_up_distance` (30),
  `match_speed_distance` (15), `slow_approach_distance` (5),
  `column_spacing` (4), `spread_spacing` (3), `formation_switch_width` (8)
- **Tests**: 22 FormationSpeedController + 21 FormationPositionUpdater +
  6 config/wiring = 49 new tests

**Future enhancements:**
- Spread formation (currently Column only; needs path-width sensor)
- NavMesh-based path-width detection for automatic Column/Spread switching

### 12.3 Squad Voice Commands

SAIN's visible-member tracking could enable gesture/command decisions:

- Boss callouts when reaching objective ("hold position", "spread out")
- Follower acknowledgment when reaching tactical position
- Warning callouts when detecting enemies

### 12.4 Combat-Aware Tactical Positioning

Currently, tactical positions are based on the quest objective only. Future
work could adjust positions based on threat awareness:

- Shift guard positions toward detected enemy directions
- Pull overwatch positions to cover known enemy approaches
- Dynamically reassign roles when combat starts (escort→flanker)

### 12.5 Zone Movement Integration

When a boss is doing zone-based movement (no quest assigned), followers could
receive zone-derived tactical positions instead of simply following:

- Each follower gets a different neighboring grid cell
- Spread across the zone field gradient
- Creates a search-party pattern

### 12.6 Multi-Level Objective Sharing

SAIN's probabilistic sharing could be extended to quest objectives:

- Boss shares objective with 1-2 trusted followers
- Those followers share with their nearest group member
- Information degrades through the chain (position accuracy decreases)
- Creates realistic information asymmetry in larger groups
