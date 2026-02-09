# Utility AI Analysis: Phobos → QuestingBots

**Date**: 2026-02-08
**Version**: QuestingBots v1.8.0 → v1.9.0 (planned)
**Status**: Implementation Plan

---

## 1. Executive Summary

Phobos uses a **utility AI** system for bot decision-making: each possible action computes a score for every bot each frame, and the highest-scoring action wins — with additive hysteresis to prevent flip-flopping. QuestingBots currently uses an **enum-based switch statement** that maps quest steps directly to actions with no scoring or hysteresis.

This document proposes an **incremental convergence** (Option C) that adds Phobos-style utility scoring to QuestingBots in 4 phases, preserving the quest system while enabling smoother transitions, context-sensitive behavior, and easy extensibility.

**Key metrics**:
- Phobos: 2 actions + 1 strategy, ~300 lines of utility infrastructure
- QuestingBots: 12 action types, 8 quest actions, 2 separate selection systems (QuestScorer + ZoneActionSelector)
- Estimated effort: Phase 1 (core) = ~200 lines + ~150 test lines; full migration = ~600 lines + ~400 test lines

---

## 2. Phobos Utility AI Architecture

### 2.1 Two-Level Task System

Phobos organizes decisions at two levels:

| Level | Entity Type | Manager | Scope |
|-------|-------------|---------|-------|
| **Strategy** | `Squad` | `StrategyManager` | Where to go (squad-level objective assignment) |
| **Action** | `Agent` | `ActionManager` | What to do (per-bot behavior at the objective) |

Both levels use the same infrastructure: `Task<T>` base class, `BaseTaskManager<T>` selection, `Entity.TaskScores[]` storage.

Reference: `Phobos/Orchestration/PhobosManager.cs:111-120` — Update order: LocationSystem → StrategyManager → ActionManager → MovementSystem → LookSystem → NavJobExecutor

### 2.2 Core Selection Algorithm

The heart of Phobos's utility AI is `BaseTaskManager<T>.PickTask()` (`Phobos/Orchestration/BaseTaskManager.cs:38-76`):

```csharp
protected void PickTask(TEntity entity)
{
    var assignment = entity.TaskAssignment;
    var highestScore = 0f;
    var nextTaskOrdinal = 0;

    // Seed from current task — including hysteresis
    if (assignment.Task != null)
    {
        nextTaskOrdinal = assignment.Ordinal;
        highestScore = entity.TaskScores[assignment.Ordinal] + assignment.Task.Hysteresis;
    }

    Task<TEntity> nextTask = null;

    for (var j = 0; j < Tasks.Length; j++)
    {
        var task = Tasks[j];
        var score = entity.TaskScores[j];
        if (score <= highestScore) continue;
        highestScore = score;
        nextTaskOrdinal = j;
        nextTask = task;
    }

    if (nextTask == null) return; // Current task still wins

    assignment.Task?.Deactivate(entity);
    nextTask.Activate(entity);
    entity.TaskAssignment = new TaskAssignment(nextTask, nextTaskOrdinal);
}
```

**Key properties**:
- **O(n) scan** over task scores — no sorting, no allocation
- **Hysteresis bonus** added to current task's score — new task must exceed `currentScore + hysteresis`
- **Null guard** — if no task beats the current, `nextTask` stays null and no switch occurs
- **Deactivate/Activate** lifecycle — clean transitions between tasks

### 2.3 Hysteresis Mechanism

Hysteresis is a per-task constant passed at construction:

| Task | Hysteresis |
|------|-----------|
| `GotoObjectiveAction` | 0.25 |
| `GuardAction` | 0.10 |
| `GotoObjectiveStrategy` | 0.25 |

Reference: `Phobos/Orchestration/PhobosManager.cs:144-145`

**Effect**: If GotoObjective is active with score 0.5, Guard must score above 0.75 (0.5 + 0.25) to take over. This prevents oscillation at objective boundaries where both tasks have similar scores.

### 2.4 Score Computation Pattern (Column-Major)

Each `Task<T>` implements `UpdateScore(int ordinal)` which iterates **all entities** and writes their score into `entity.TaskScores[ordinal]`:

```csharp
// GotoObjectiveAction.UpdateScore — Phobos/Tasks/Actions/GotoObjectiveAction.cs:16-47
public override void UpdateScore(int ordinal)
{
    var agents = dataset.Entities.Values;
    for (var i = 0; i < agents.Count; i++)
    {
        var agent = agents[i];
        // ... compute utility based on distance to objective ...
        agent.TaskScores[ordinal] = utilityDecay * (UtilityBase + utilityBoostFactor * UtilityBoost);
    }
}
```

This is **column-major** — one task updates all entities, rather than one entity evaluating all tasks. It's cache-friendly for the task's state but requires iterating entities per task.

### 2.5 Entity Score Storage

`Entity.TaskScores` is a `float[]` sized to the number of registered tasks (`Phobos/Entities/Entity.cs:6-11`):

```csharp
public class Entity(int id, float[] taskScores) : IEquatable<Entity>
{
    public readonly int Id = id;
    public readonly float[] TaskScores = taskScores;
    public TaskAssignment TaskAssignment;
}
```

Allocation: Once at entity creation, sized to `ActionManager.Tasks.Length` (`Phobos/Orchestration/PhobosManager.cs:97`).

### 2.6 BigBrain Integration

Phobos uses a **single BigBrain layer** (`PhobosLayer`, priority 19) with a **DummyAction** (`Phobos/PhobosLayer.cs:12-25`):

```csharp
public override Action GetNextAction()
{
    return new Action(typeof(DummyAction), "Dummy Action");
}

public override bool IsCurrentActionEnding()
{
    return false; // Never ends — Phobos manages behavior internally
}
```

**Key insight**: Phobos doesn't use BigBrain's action dispatch at all. The layer activates → DummyAction runs → all actual behavior happens through `ActionManager.Update()` which calls each active task's `Update()` method.

The layer's `IsActive()` checks combat state: returns false when healing, under fire, or has enemy (`PhobosLayer.cs:105-112`). BigBrain handles the combat→questing transition.

### 2.7 Current Tasks

**Actions** (Agent-level):

| Action | Base Score | Behavior | Hysteresis |
|--------|-----------|----------|-----------|
| `GotoObjectiveAction` | 0.5, boosted to 0.65 when near | Move to squad objective via movement system | 0.25 |
| `GuardAction` | 0 outside radius, up to 0.65 at 80% radius | Move to cover point near objective | 0.10 |

Score dynamics:
- **Far from objective**: GotoObjective=0.5, Guard=0 → bot moves toward objective
- **Entering objective radius**: GotoObjective=0.55, Guard=0.3 → still moving
- **At 80% of radius**: GotoObjective=0.35 (decaying), Guard=0.65 → switches to Guard
- **With hysteresis**: Guard needs to beat GotoObjective by 0.25 to take over; GotoObjective needs to beat Guard by 0.10 to retake

**Strategies** (Squad-level):

| Strategy | Score | Behavior | Hysteresis |
|----------|-------|----------|-----------|
| `GotoObjectiveStrategy` | Flat 0.5 | Assign squad objectives, track arrival, manage timers | 0.25 |

Only one strategy exists — the system is designed for extensibility but currently simple.

### 2.8 Extensibility Model

Phobos supports external plugins registering actions/strategies via delegates (`PhobosManager.cs:20-23`):

```csharp
public static RegisterActionsDelegate OnRegisterActions;
public static RegisterStrategiesDelegate OnRegisterStrategies;
```

This means other mods can add scored tasks without modifying Phobos source.

---

## 3. QuestingBots Current Decision Architecture

### 3.1 BotObjectiveLayer Switch Dispatch

The primary decision point is `BotObjectiveLayer.trySetNextAction()` (`BotLogic/Objective/BotObjectiveLayer.cs:77-149`):

```csharp
private bool trySetNextAction()
{
    switch (objectiveManager.CurrentQuestAction)
    {
        case QuestAction.MoveToPosition:
            if (objectiveManager.MustUnlockDoor)
                setNextAction(BotActionType.UnlockDoor, "UnlockDoor");
            else
                setNextAction(BotActionType.GoToObjective, "GoToObjective");
            return true;

        case QuestAction.Ambush:
            if (!objectiveManager.IsCloseToObjective())
                setNextAction(BotActionType.GoToObjective, "GoToAmbushPosition");
            else
                setNextAction(BotActionType.Ambush, "Ambush");
            return true;

        // ... similar for Snipe, PlantItem, HoldAtPosition, etc.
    }
}
```

**Issues**:
1. **Rigid dispatch**: Adding a new action requires modifying this switch statement
2. **No scoring**: Actions are selected purely based on quest step type and distance checks
3. **No hysteresis**: Action changes every time the quest step changes or distance threshold is crossed
4. **Binary transitions**: Bot either IS or ISN'T close to objective — no smooth scoring gradient

### 3.2 Action Dispatch Chain

```
BotObjectiveLayer.IsActive()
  → decisionMonitor.IsAllowedToQuest()
  → decisionMonitor.CurrentDecision == BotQuestingDecision.Quest
  → trySetNextAction()
    → switch(CurrentQuestAction) → setNextAction(BotActionType, reason)
      → stores nextAction + actionReason

BotObjectiveLayer.IsCurrentActionEnding()
  → nextAction != previousAction (any change triggers ending)

BotObjectiveLayer.GetNextAction() (from CustomLayerDelayedUpdate.cs:55-90)
  → switch(nextAction) → new Action(typeof(ConcreteAction), reason)
```

### 3.3 BotActionType vs QuestAction

Two separate enums exist:

| QuestAction (quest data) | BotActionType (runtime) | BigBrain Action Class |
|--------------------------|------------------------|----------------------|
| MoveToPosition | GoToObjective | `GoToObjectiveAction` |
| HoldAtPosition | HoldPosition | `HoldAtObjectiveAction` |
| Ambush | Ambush | `AmbushAction` |
| Snipe | Snipe | `SnipeAction` |
| PlantItem | PlantItem | `PlantItemAction` |
| ToggleSwitch | ToggleSwitch | `ToggleSwitchAction` |
| CloseNearbyDoors | CloseNearbyDoors | `CloseNearbyDoorsAction` |
| RequestExtract | *(handled inline)* | — |
| — | FollowBoss | `FollowBossAction` |
| — | BossRegroup | `BossRegroupAction` |
| — | FollowerRegroup | `FollowerRegroupAction` |
| — | Sleep | `SleepingAction` |
| — | UnlockDoor | `UnlockDoorAction` |

The mapping is 1:1 for quest actions, but BotActionType includes non-quest actions (Follow, Regroup, Sleep, UnlockDoor) used by other layers.

### 3.4 QuestScorer (ECS)

`BotLogic/ECS/Systems/QuestScorer.cs` — Pure-logic quest scoring that selects **which quest** to assign:

```csharp
public static double ScoreQuest(
    float minDistance, float maxOverallDistance, int maxRandomDistance,
    float desirability, bool isActiveForPlayer, float minExfilAngle,
    in QuestScoringConfig config, Random rng)
{
    double distanceFraction = 1.0 - (minDistance + rng.Next(-max, max)) / maxOverall;
    float desirabilityFraction = (desirability * multiplier + noise) / 100f;
    double exfilAngleFactor = Max(0, angle - maxAngle) / (180 - maxAngle);

    return (distanceFraction * config.DistanceWeighting)
        + (desirabilityFraction * config.DesirabilityWeighting)
        - (exfilAngleFactor * config.ExfilDirectionWeighting);
}
```

**Key insight**: This IS a utility scoring system, but at the quest-selection level, not the action-selection level. It's already Phobos-compatible in concept.

### 3.5 ZoneActionSelector

`ZoneMovement/Selection/ZoneActionSelector.cs` — Weighted-random action selection per POI category:

```csharp
// Container: 60% Ambush, 20% Snipe, 10% HoldAtPosition, 10% PlantItem
// LooseLoot: 50% HoldAtPosition, 30% Ambush, 20% MoveToPosition
// Quest:     70% MoveToPosition, 20% HoldAtPosition, 10% Ambush
// ...
```

**Key insight**: This is a **proto-utility AI** — it selects actions based on context (POI category), but uses weighted random instead of scored utility. Converting this to scored tasks would be a natural evolution.

### 3.6 BotObjectiveManager Lifecycle

`Components/BotObjectiveManager.cs` — Manages the current assignment:

- `CurrentQuestAction`: The current quest step's action type
- `IsCloseToObjective()`: Distance check (within `OjectiveReachedIdeal`)
- `CompleteObjective()` / `FailObjective()`: Lifecycle transitions
- `StartJobAssignment()`: Begin executing the current assignment

The assignment itself comes from `BotJobAssignmentFactory` which uses `QuestScorer` for quest selection.

### 3.7 Available ECS Sensor Data

`BotEntity` (`BotLogic/ECS/BotEntity.cs`) already has sensor bools that could feed utility scoring:

| Sensor | Type | Source | Potential Utility Use |
|--------|------|--------|----------------------|
| `IsInCombat` | bool | Push (event-driven) | Score 0 for all quest actions during combat |
| `IsSuspicious` | bool | Push | Boost Guard/Ambush scores, reduce Move scores |
| `CanQuest` | bool | Pull (polled) | Gate all quest action scores |
| `CanSprintToObjective` | bool | Pull | Boost Move score when far |
| `WantsToLoot` | bool | Push | Boost Move-to-loot score |
| `LastLootingTime` | float | Write | Cooldown for loot-related actions |
| `BotType` | enum | Write | Per-type score adjustments |
| `IsSleeping` | bool | Write | Gate all action scores |

---

## 4. Gap Analysis

| Aspect | Phobos | QuestingBots | Gap |
|--------|--------|--------------|-----|
| **Action selection** | Utility scoring + hysteresis | Enum switch | **HIGH** — no scoring framework |
| **Score storage** | `float[]` on Entity | None | **HIGH** — need TaskScores on BotEntity |
| **Hysteresis** | Additive per-task constant | None | **HIGH** — causes action oscillation |
| **Task registry** | `DefinitionRegistry` + array | Hardcoded enum switches | **MEDIUM** — need registry |
| **Update pattern** | Column-major (task → all entities) | Row-major (per-bot switch) | **MEDIUM** — architectural change |
| **Quest scoring** | N/A | QuestScorer (utility-like) | **NONE** — already compatible |
| **Zone action selection** | N/A | ZoneActionSelector (weighted random) | **LOW** — convert to scored tasks |
| **Lifecycle hooks** | Activate/Deactivate per task | setNextAction (no lifecycle) | **MEDIUM** — need task lifecycle |
| **Debug overlay** | BuildDebugText with utility report | ZoneDebugOverlay (zone fields) | **LOW** — extend debug overlay |
| **Extensibility** | Plugin delegates for external tasks | None | **LOW** — nice-to-have |

---

## 5. Design Options

### 5.1 Option A: Full Phobos-Style Replacement

Replace `BotObjectiveLayer`'s switch with a scored task system. Each `QuestAction` becomes a `Task<BotEntity>` with `UpdateScore`/`Update`/`Activate`/`Deactivate`.

**Pros**: Clean architecture, full convergence with Phobos, maximum extensibility
**Cons**: Major refactor (~500 lines changed), risk of behavioral regression, quest-step sequencing must translate to utility scores

### 5.2 Option B: Hybrid Layer

Keep quest assignment and quest-step sequencing, add a utility scoring layer only for the "which action to execute now" decision within a quest step.

**Pros**: Incremental, preserves quest system, adds utility where it matters most
**Cons**: Two overlapping systems (quest dispatch + utility scoring), might feel bolted-on

### 5.3 Option C: Incremental Convergence (Recommended)

Phased migration from enum dispatch to scored tasks:

1. **Phase 1**: Add utility infrastructure (TaskScores, UtilityTaskManager, hysteresis)
2. **Phase 2**: Convert ZoneActionSelector to scored tasks (validate framework)
3. **Phase 3**: Convert quest actions to scored tasks (main migration)
4. **Phase 4**: Unify zone and quest action selection
5. **Phase 5** (Optional): Add squad-level strategies

**Pros**: Lowest risk per phase, each phase independently testable, gradual behavioral validation
**Cons**: Multiple intermediate states, takes longer

**Recommendation**: Option C. Each phase produces a working system, and we can stop after any phase if the results are satisfactory.

---

## 6. Implementation Plan

### 6.1 Phase 1: Core Utility Infrastructure

**Goal**: Add the scored task framework to QuestingBots with no behavioral changes.

**New files**:
- `BotLogic/ECS/UtilityAI/UtilityTask.cs` — Abstract base class
- `BotLogic/ECS/UtilityAI/UtilityTaskManager.cs` — Score evaluation + task selection with hysteresis
- `BotLogic/ECS/UtilityAI/UtilityTaskAssignment.cs` — Assignment struct

**Modified files**:
- `BotLogic/ECS/BotEntity.cs` — Add `float[] TaskScores` and `UtilityTaskAssignment TaskAssignment`
- `BotLogic/ECS/BotRegistry.cs` — Size `TaskScores` array at entity creation
- `BotLogic/ECS/BotEntityBridge.cs` — Add utility task wiring methods

**Design**:

```csharp
// UtilityTask.cs — mirrors Phobos Task<T>
public abstract class UtilityTask
{
    public readonly float Hysteresis;

    protected UtilityTask(float hysteresis) { Hysteresis = hysteresis; }

    /// <summary>Column-major: compute scores for ALL entities.</summary>
    public abstract void UpdateScores(int ordinal, IReadOnlyList<BotEntity> entities);

    /// <summary>Execute behavior for entities assigned to this task.</summary>
    public abstract void Update();

    /// <summary>Called when an entity switches TO this task.</summary>
    public virtual void Activate(BotEntity entity) { }

    /// <summary>Called when an entity switches AWAY from this task.</summary>
    public virtual void Deactivate(BotEntity entity) { }
}
```

```csharp
// UtilityTaskManager.cs — mirrors Phobos BaseTaskManager<T>
public class UtilityTaskManager
{
    private readonly UtilityTask[] _tasks;

    public void UpdateScores(IReadOnlyList<BotEntity> entities) { ... }
    public void PickTasks(IReadOnlyList<BotEntity> entities) { ... }
    public void UpdateTasks() { ... }

    // Core selection with hysteresis — identical to Phobos
    public void PickTask(BotEntity entity) { ... }
}
```

```csharp
// UtilityTaskAssignment.cs — mirrors Phobos TaskAssignment
public readonly struct UtilityTaskAssignment
{
    public readonly UtilityTask Task;
    public readonly int Ordinal;
}
```

**BotEntity additions**:

```csharp
// Added to BotEntity
public float[] TaskScores;
public UtilityTaskAssignment TaskAssignment;
```

**Key constraint**: All Phase 1 classes are pure logic with zero Unity dependencies — fully testable.

**Tests** (~40-50):
- UtilityTaskManagerTests: PickTask selection, hysteresis, lifecycle
- TaskScores integration: allocation, initialization, bounds
- Scenario tests: multi-entity scoring, task switching with hysteresis

### 6.2 Phase 2: Zone Action Migration

**Goal**: Convert `ZoneActionSelector` from weighted-random to scored utility tasks.

**New files**:
- `ZoneMovement/Selection/Tasks/ZoneMoveTask.cs`
- `ZoneMovement/Selection/Tasks/ZoneHoldTask.cs`
- `ZoneMovement/Selection/Tasks/ZoneAmbushTask.cs`
- `ZoneMovement/Selection/Tasks/ZoneSnipeTask.cs`

**Modified files**:
- `ZoneMovement/Integration/ZoneObjectiveCycler.cs` — Use UtilityTaskManager instead of ZoneActionSelector
- `ZoneMovement/Selection/ZoneActionSelector.cs` — Deprecated (kept for reference)

**Scoring logic** (replacing weighted random):

```csharp
// ZoneAmbushTask example
public override void UpdateScores(int ordinal, IReadOnlyList<BotEntity> entities)
{
    for (int i = 0; i < entities.Count; i++)
    {
        var entity = entities[i];
        if (!entity.HasFieldState) { entity.TaskScores[ordinal] = 0; continue; }

        var field = entity.FieldState;
        float categoryBonus = field.DominantCategory == PoiCategory.Container ? 0.6f
                            : field.DominantCategory == PoiCategory.LooseLoot ? 0.3f
                            : field.DominantCategory == PoiCategory.Exfil ? 0.3f : 0.1f;

        float distanceFactor = InverseLerp(maxDist, 0, field.DistanceToPOI);
        entity.TaskScores[ordinal] = categoryBonus * distanceFactor;
    }
}
```

**Benefits**:
- Validates the utility framework with a simpler domain
- ZoneActionSelector's hardcoded weights become computed scores
- Bot state (sensors) can influence zone action selection
- Hysteresis prevents oscillation between zone actions

**Tests** (~20):
- Per-task scoring under various POI categories
- Task selection with distance gradients
- Hysteresis preventing oscillation

### 6.3 Phase 3: Quest Action Migration

**Goal**: Replace `BotObjectiveLayer.trySetNextAction()` switch with scored utility tasks.

**New files**:
- `BotLogic/Objective/Tasks/QuestMoveTask.cs`
- `BotLogic/Objective/Tasks/QuestHoldTask.cs`
- `BotLogic/Objective/Tasks/QuestAmbushTask.cs`
- `BotLogic/Objective/Tasks/QuestSnipeTask.cs`
- `BotLogic/Objective/Tasks/QuestPlantItemTask.cs`
- `BotLogic/Objective/Tasks/QuestToggleSwitchTask.cs`
- `BotLogic/Objective/Tasks/QuestUnlockDoorTask.cs`
- `BotLogic/Objective/Tasks/QuestCloseDoorsTask.cs`

**Modified files**:
- `BotLogic/Objective/BotObjectiveLayer.cs` — Replace switch with UtilityTaskManager
- `BehaviorExtensions/CustomLayerDelayedUpdate.cs` — Update action dispatch

**Scoring logic** — each task checks quest step eligibility then scores based on context:

```csharp
// QuestAmbushTask example
public override void UpdateScores(int ordinal, IReadOnlyList<BotEntity> entities)
{
    for (int i = 0; i < entities.Count; i++)
    {
        var entity = entities[i];

        // Eligibility gate: only score > 0 when quest step is Ambush
        if (entity.CurrentQuestAction != QuestAction.Ambush)
        {
            entity.TaskScores[ordinal] = 0;
            continue;
        }

        // Score based on distance to objective
        float distToObjective = entity.DistanceToObjective;
        float closeThreshold = entity.ObjectiveReachedIdeal;

        if (distToObjective > closeThreshold)
        {
            // Not close enough — GoToObjective should score higher
            entity.TaskScores[ordinal] = 0.1f;
        }
        else
        {
            // Close enough to ambush — high score
            entity.TaskScores[ordinal] = 0.7f;
        }
    }
}
```

```csharp
// QuestMoveTask — always moderately scored when quest step is Move
public override void UpdateScores(int ordinal, IReadOnlyList<BotEntity> entities)
{
    for (int i = 0; i < entities.Count; i++)
    {
        var entity = entities[i];

        bool isMovementStep = entity.CurrentQuestAction == QuestAction.MoveToPosition
                           || entity.CurrentQuestAction == QuestAction.Ambush  // "go to ambush position" phase
                           || entity.CurrentQuestAction == QuestAction.Snipe
                           || entity.CurrentQuestAction == QuestAction.PlantItem;

        if (!isMovementStep || entity.IsCloseToObjective)
        {
            entity.TaskScores[ordinal] = 0;
            continue;
        }

        // Higher score when farther from objective
        float distFactor = Clamp01(entity.DistanceToObjective / maxDistance);
        entity.TaskScores[ordinal] = 0.5f + 0.15f * distFactor;
    }
}
```

**Key behavioral preservation**:
- Quest step eligibility gates ensure the quest data still drives behavior
- Distance-based scoring replaces binary `IsCloseToObjective()` with smooth gradients
- Hysteresis prevents the "go to position" / "ambush" oscillation at objective boundary
- The "go to X position → do X action" two-phase pattern is preserved via scoring: Move task scores high when far, target action scores high when near

**BotObjectiveLayer simplification**:

```csharp
// Before: big switch statement
private bool trySetNextAction()
{
    switch (objectiveManager.CurrentQuestAction) { ... 70 lines ... }
}

// After: utility evaluation
private bool trySetNextAction()
{
    // Update entity state from BotObjectiveManager
    SyncEntityState(entity, objectiveManager);

    // Utility evaluation happens in UtilityTaskManager (called from PhobosManager-like orchestrator)
    // The result is already in entity.TaskAssignment
    var task = entity.TaskAssignment.Task;
    if (task == null) return false;

    setNextAction(task.ActionType, task.ActionReason);
    return true;
}
```

**Config gating**: `use_utility_ai` (default: false initially, flipped to true when stable)

**Tests** (~30):
- Per-task eligibility: only scores when quest step matches
- Distance-based scoring gradients
- Two-phase transitions (Move → Ambush) with hysteresis
- UnlockDoor priority when MustUnlockDoor is set
- Regression: same action selected as the switch statement for standard scenarios

### 6.4 Phase 4: Unified Action Selection

**Goal**: Merge zone tasks and quest tasks into a single `UtilityTaskManager`.

**Changes**:
- Single task array containing both zone and quest tasks
- Zone tasks score > 0 only when in zone movement mode (no active quest)
- Quest tasks score > 0 only when an active quest step exists
- Both compete in the same evaluation — highest score wins regardless of source

**Benefits**:
- Eliminates parallel zone/quest decision paths
- A bot can seamlessly transition between quest and zone behaviors
- New behaviors (e.g., loot-seeking) can be added as tasks without changing dispatch logic

### 6.5 Phase 5: Squad Strategies (Optional)

**Goal**: Add Phobos-style squad-level strategy scoring.

This would add a `StrategyManager` for boss entities, enabling squad-level objective assignment with cover points. This is a larger change and is optional for v1.9.0.

---

## 7. Per-Phase File Plan

### Phase 1: Core Infrastructure

| File | Action | Lines (est) |
|------|--------|-------------|
| `BotLogic/ECS/UtilityAI/UtilityTask.cs` | NEW | ~40 |
| `BotLogic/ECS/UtilityAI/UtilityTaskManager.cs` | NEW | ~80 |
| `BotLogic/ECS/UtilityAI/UtilityTaskAssignment.cs` | NEW | ~15 |
| `BotLogic/ECS/BotEntity.cs` | MODIFY | +5 |
| `BotLogic/ECS/BotRegistry.cs` | MODIFY | +5 |
| `BotLogic/ECS/BotEntityBridge.cs` | MODIFY | +20 |
| `tests/.../UtilityAI/UtilityTaskManagerTests.cs` | NEW | ~200 |
| `tests/.../UtilityAI/HysteresisTests.cs` | NEW | ~100 |

### Phase 2: Zone Action Migration

| File | Action | Lines (est) |
|------|--------|-------------|
| `ZoneMovement/Selection/Tasks/ZoneMoveTask.cs` | NEW | ~30 |
| `ZoneMovement/Selection/Tasks/ZoneHoldTask.cs` | NEW | ~30 |
| `ZoneMovement/Selection/Tasks/ZoneAmbushTask.cs` | NEW | ~30 |
| `ZoneMovement/Selection/Tasks/ZoneSnipeTask.cs` | NEW | ~30 |
| `ZoneMovement/Integration/ZoneObjectiveCycler.cs` | MODIFY | ~20 |
| `tests/.../ZoneMovement/Tasks/*Tests.cs` | NEW | ~120 |

### Phase 3: Quest Action Migration

| File | Action | Lines (est) |
|------|--------|-------------|
| `BotLogic/Objective/Tasks/Quest*Task.cs` (×8) | NEW | ~240 |
| `BotLogic/Objective/BotObjectiveLayer.cs` | MODIFY | ~-50 |
| `BehaviorExtensions/CustomLayerDelayedUpdate.cs` | MODIFY | ~10 |
| `Configuration/BotQuestingConfig.cs` | MODIFY | +2 |
| `tests/.../Objective/Tasks/*Tests.cs` | NEW | ~200 |

### Phase 4: Unified Selection

| File | Action | Lines (est) |
|------|--------|-------------|
| `BotLogic/Objective/BotObjectiveLayer.cs` | MODIFY | ~30 |
| `ZoneMovement/Integration/ZoneObjectiveCycler.cs` | MODIFY | ~-20 |
| `BotLogic/ECS/BotEntityBridge.cs` | MODIFY | ~10 |
| `tests/.../UtilityAI/UnifiedSelectionTests.cs` | NEW | ~80 |

---

## 8. Testing Strategy

### Approach

All utility AI classes are **pure logic** with zero Unity dependencies, following the established pattern:
- Source files linked via `<Compile Include="..." Link="..."/>` in test csproj
- Tests in `tests/SPTQuestingBots.Client.Tests/BotLogic/ECS/UtilityAI/`
- NUnit 3.x + NSubstitute

### Test Categories

| Category | Tests (est) | Phase |
|----------|-------------|-------|
| Core selection (PickTask, hysteresis, lifecycle) | ~25 | 1 |
| Score storage (BotEntity.TaskScores) | ~10 | 1 |
| Multi-entity scenarios | ~10 | 1 |
| Zone task scoring | ~20 | 2 |
| Quest task eligibility | ~15 | 3 |
| Quest task scoring gradients | ~15 | 3 |
| Behavioral regression (switch parity) | ~10 | 3 |
| Unified selection | ~10 | 4 |
| **Total** | **~115** | — |

### Key Test Scenarios

**Hysteresis prevents oscillation**:
```
1. Bot at objective boundary, GotoObjective active
2. Guard score slightly exceeds GotoObjective score
3. Assert: no switch (hysteresis prevents it)
4. Guard score exceeds GotoObjective + hysteresis
5. Assert: switches to Guard
6. Bot moves slightly, GotoObjective score rises slightly
7. Assert: no switch back (Guard's hysteresis protects it)
```

**Quest step eligibility**:
```
1. Quest step is Ambush, bot is far
2. Assert: QuestMoveTask scores high, QuestAmbushTask scores low
3. Bot approaches objective
4. Assert: QuestAmbushTask score rises, QuestMoveTask score falls
5. At threshold: QuestAmbushTask overtakes QuestMoveTask + hysteresis
6. Assert: switches to QuestAmbushTask
```

**Behavioral regression**:
```
For each QuestAction in {MoveToPosition, HoldAtPosition, Ambush, Snipe, PlantItem, ...}:
1. Set up entity with that quest step
2. Run utility evaluation
3. Assert: selected task matches what the switch statement would have selected
```

---

## 9. Risk Assessment

| Risk | Severity | Likelihood | Mitigation |
|------|----------|-----------|-----------|
| **Behavioral regression** | HIGH | MEDIUM | Config toggle (`use_utility_ai`), regression test suite, phased rollout |
| **Quest step sequencing broken** | HIGH | LOW | Eligibility gates — tasks score 0 when quest step doesn't match |
| **Action oscillation** | MEDIUM | LOW | Hysteresis values tuned to match Phobos (0.10-0.25) |
| **Performance regression** | LOW | LOW | Column-major scoring is O(tasks × entities) — with ~10 tasks and <50 entities, negligible |
| **Score tuning complexity** | MEDIUM | MEDIUM | Start with binary (eligible/ineligible) scoring, add gradients incrementally |
| **ECS BotEntity bloat** | LOW | LOW | Single `float[]` allocation per entity, one `UtilityTaskAssignment` struct |

---

## 10. Configuration

New config entries under `questing.bot_questing`:

| Property | Default | Description |
|----------|---------|-------------|
| `use_utility_ai` | `false` | Enable utility AI action selection; false = legacy switch dispatch |

Utility score tuning (future, Phase 3+):

| Property | Default | Description |
|----------|---------|-------------|
| `utility_goto_hysteresis` | `0.25` | Hysteresis for GoToObjective task |
| `utility_guard_hysteresis` | `0.10` | Hysteresis for Guard/Hold tasks |
| `utility_action_hysteresis` | `0.15` | Hysteresis for Ambush/Snipe/PlantItem tasks |

---

## 11. Appendix: Key File References

### Phobos

| File | Purpose |
|------|---------|
| `Phobos/Orchestration/BaseTaskManager.cs:38-76` | Core PickTask with hysteresis |
| `Phobos/Orchestration/ActionManager.cs` | Agent-level task orchestration |
| `Phobos/Orchestration/StrategyManager.cs` | Squad-level task orchestration |
| `Phobos/Orchestration/PhobosManager.cs:140-162` | Task registration (actions + strategies) |
| `Phobos/Tasks/Task.cs` | Abstract Task<T> with hysteresis |
| `Phobos/Tasks/TaskAssignment.cs` | Assignment struct (Task + Ordinal) |
| `Phobos/Tasks/Actions/GotoObjectiveAction.cs` | GotoObjective scoring (0.5 base, distance boost) |
| `Phobos/Tasks/Actions/GuardAction.cs` | Guard scoring (0 outside, 0.65 at 80% radius) |
| `Phobos/Tasks/Strategies/GotoObjectiveStrategy.cs` | Squad objective lifecycle |
| `Phobos/Entities/Entity.cs` | TaskScores float[] + TaskAssignment |
| `Phobos/Entities/Agent.cs` | Agent entity (Movement, Stuck, Look, Objective, Guard) |
| `Phobos/Entities/Squad.cs` | Squad entity (Members, Objective, Leader) |
| `Phobos/PhobosLayer.cs` | BigBrain integration (DummyAction, IsActive combat check) |
| `Phobos/Orchestration/DefinitionRegistry.cs` | Type-keyed task registry |

### QuestingBots (Current)

| File | Purpose |
|------|---------|
| `BotLogic/Objective/BotObjectiveLayer.cs:77-149` | Switch dispatch (to be replaced) |
| `BehaviorExtensions/CustomLayerDelayedUpdate.cs:55-90` | GetNextAction dispatch |
| `BehaviorExtensions/CustomLayerDelayedUpdate.cs:12-27` | BotActionType enum |
| `Models/Questing/QuestObjectiveStep.cs:16-27` | QuestAction enum |
| `BotLogic/ECS/Systems/QuestScorer.cs` | Quest-level utility scoring |
| `ZoneMovement/Selection/ZoneActionSelector.cs` | Zone action weighted random |
| `Components/BotObjectiveManager.cs` | Objective lifecycle manager |
| `BotLogic/ECS/BotEntity.cs` | Entity (to add TaskScores) |
| `BotLogic/ECS/BotRegistry.cs` | Entity registry (to size TaskScores) |
| `BotLogic/ECS/BotEntityBridge.cs` | Bridge layer (to add utility wiring) |
