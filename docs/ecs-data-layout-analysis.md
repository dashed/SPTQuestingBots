# ECS Data Layout Analysis for SPTQuestingBots

> Deep investigation of Entity-Component-System patterns as implemented by Phobos,
> compared with QuestingBots' current architecture, with feasibility assessment
> and implementation plan.

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [What is ECS and Why It Matters](#what-is-ecs-and-why-it-matters)
3. [Phobos's ECS-Inspired Architecture](#phoboss-ecs-inspired-architecture)
4. [QuestingBots Current Data Layout](#questingbots-current-data-layout)
5. [Side-by-Side Comparison](#side-by-side-comparison)
6. [Pros and Cons for QuestingBots](#pros-and-cons-for-questingbots)
7. [Feasibility Assessment](#feasibility-assessment)
8. [Recommended Implementation Plan](#recommended-implementation-plan)
9. [Risk Assessment](#risk-assessment)
10. [Conclusion](#conclusion)

---

## Executive Summary

Phobos uses an **"ECS-inspired" architecture** — not pure ECS. Components are
**embedded directly on Agent objects** as class fields (not stored in separate
contiguous arrays). The actual ECS infrastructure (`EntityArray<T>`,
`ComponentArray<T>`) provides entity lifecycle management (ID allocation,
swap-remove, free stack) but component data lives on the entity objects
themselves.

**Key insight**: At SPT's bot count (20–40 bots), the performance argument for
ECS is weak. A 40-bot array fits entirely in L1 cache regardless of layout. The
real benefits of adopting Phobos-style patterns are:

1. **Code organization** — centralized bot state instead of scattered dictionaries
2. **Testability** — static systems with no MonoBehaviour dependency
3. **Pattern alignment** — easier to port behaviors from Phobos
4. **Debuggability** — single place to inspect all bot state

**Recommendation**: Adopt a phased "ECS-Lite" approach — a dense bot registry
with embedded components and static system methods — without attempting a full
ECS rewrite. This gives 80% of the benefits at 20% of the effort.

---

## What is ECS and Why It Matters

### The Pattern

Entity-Component-System (ECS) separates game objects into three concepts:

| Concept | Role | Example |
|---------|------|---------|
| **Entity** | Identity (just an ID) | Bot #7 |
| **Component** | Data (no behavior) | `Movement { Target, Speed, Pose }` |
| **System** | Behavior (no data) | `MovementSystem.Update(agents)` |

### Data Layout: AoS vs SoA

Traditional OOP uses **Array of Structures** (AoS):

```
Bot[] bots;  // each Bot holds Movement, Stuck, Look, etc.
// Memory: [Bot0.Move, Bot0.Stuck, Bot0.Look] [Bot1.Move, Bot1.Stuck, Bot1.Look] ...
```

Pure ECS uses **Structure of Arrays** (SoA):

```
Movement[] movements;  // all movement data packed together
Stuck[] stucks;        // all stuck data packed together
// Memory: [Move0, Move1, Move2, ...] [Stuck0, Stuck1, Stuck2, ...]
```

SoA is cache-friendly when systems only need one component type at a time. For
example, a `StuckSystem` iterating only `Stuck[]` gets perfect cache line
utilization because all the data it needs is packed contiguously.

### When Cache Coherency Matters

Consider a stuck detection system that needs Position (12B) + Speed (4B) = 16B
per bot. With a 60-byte AoS struct that includes unused fields:

| Bot Count | AoS (60B struct) | SoA (16B hot) | Both Fit L1? | Layout Matters? |
|-----------|------------------|---------------|--------------|-----------------|
| 10 | 600B (10 lines) | 160B (3 lines) | Yes | No |
| 40 | 2.4 KB (38 lines) | 640B (10 lines) | Yes | No |
| 100 | 6 KB (94 lines) | 1.6 KB (25 lines) | Yes | Barely |
| 500 | 30 KB (469 lines) | 8 KB (125 lines) | AoS: barely | Yes |
| 1,000 | 60 KB (938 lines) | 16 KB (250 lines) | AoS: NO | Significant |

*(64-byte cache lines, 32 KB L1 data cache)*

SPT operates at 20–40 bots. At this scale, **AoS and SoA perform identically**
because the entire working set fits in L1 cache. The timing difference is
~28ns vs ~10ns per full iteration — negligible against a 16.7ms frame budget.

### Performance Priority Ranking (Mono Runtime)

On Mono's Boehm GC (single-generation, stop-the-world), the optimization
priorities for a 40-bot mod are:

| Priority | Optimization | Per-Frame Savings |
|----------|-------------|-------------------|
| 1 | **Eliminate GC allocations in hot loops** | 1–10 ms per GC pause avoided |
| 2 | **Throttle expensive operations** (NavMesh, raycasts) | 0.5–2 ms per skipped call |
| 3 | **Reduce pointer chasing** (flat data extraction) | ~5–50 µs |
| 4 | **SoA cache layout** | ~0.01–0.1 µs (**negligible**) |

GC avoidance is the #1 performance lever on Mono — orders of magnitude more
impactful than cache layout at SPT's entity count.

### BepInEx Modding Constraints

Several factors limit ECS adoption in BepInEx mods:

- **No Unity DOTS**: Entities, Burst, and Collections packages require
  project-level setup — impossible for runtime-injected BepInEx assemblies.
  Burst requires build-time LLVM compilation, incompatible with Harmony patching.
- **Mono runtime**: `netstandard2.1` target, no `System.Runtime.Intrinsics`
  (no manual SIMD), no Burst auto-vectorization, no `Span<T>` in hot paths
- **BSG API interop**: Must work with `BotOwner`, `Player`, `GameWorld` — all
  OOP-heavy Unity objects managed by BSG's code. Typical accessor chains like
  `bot.Memory.GoalEnemy.EnemyPerson.Position` involve 3–5 pointer dereferences.
- **BigBrain framework**: Expects MonoBehaviour-based layers (`CustomLayer`,
  `CustomLogic`), cannot be replaced with pure systems
- **GC pressure**: Mono's Boehm GC is conservative, single-generation,
  stop-the-world. A single GC pause costs 1–10 ms — dwarfing any cache layout
  optimization. Allocation avoidance is the #1 performance priority.

---

## Phobos's ECS-Inspired Architecture

### Overview

Phobos implements an ECS-inspired pattern with these key design decisions:

1. **Dense entity list** for iteration (no dictionary traversal)
2. **Components embedded on entities** (not in separate arrays)
3. **Systems as static methods** iterating the entity list
4. **Centralized tick orchestration** with deterministic order

### Data Containers

#### `EntityArray<T>` — Dense Entity Storage

```csharp
// Phobos/Phobos/Data/EntityArray.cs
public class EntityArray<T>(int capacity) where T : Entity
{
    public readonly List<T> Values = new(capacity);    // Dense! No gaps
    private readonly List<int?> _idSlots = new(capacity);  // ID → index
    private readonly Stack<int> _freeIds = new();          // Recycled IDs
}
```

Key design:
- `Values` is a **dense** `List<T>` — no null gaps, no tombstones
- **Swap-remove** on entity death: last element fills the gap, keeping the list
  packed for sequential iteration
- `_idSlots` provides O(1) ID → index lookup
- `_freeIds` recycles entity IDs (Stack-based LIFO)
- Default capacity: 32 for agents

#### `ComponentArray<T>` — ID-Indexed Storage

```csharp
// Phobos/Phobos/Data/ComponentArray.cs
public class ComponentArray<T> : IComponentArray where T : class, new()
{
    private readonly List<T> _components = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T this[int id]
    {
        get => _components[id];
        set => _components[id] = value;
    }
}
```

Key design:
- Indexed by **entity ID** (not dense index)
- `[AggressiveInlining]` on the indexer for zero-overhead access
- Add fills gaps with `default(T)` up to the ID
- Remove sets slot to `default(T)` (null tombstone for reference types)

**Critical observation**: `ComponentArray<T>` exists in Phobos's codebase but is
**not used for core Agent components**. Agent's Movement, Stuck, Look, etc. are
**embedded directly on the Agent object** (see below). ComponentArray serves as
an **extensibility mechanism** via the `OnRegisterAgentComponents` delegate,
allowing other plugins to attach additional component data to agents.

#### `Dataset<T, TE>` — Container Composition

```csharp
// Phobos/Phobos/Data/Dataset.cs
public class Dataset<T, TE>(TE entities) where TE : EntityArray<T> where T : Entity
{
    public readonly TE Entities = entities;
    private readonly List<IComponentArray> _components = [];
    private readonly Dictionary<Type, IComponentArray> _componentsTypeMap = new();
}

public class AgentData() : Dataset<Agent, AgentArray>(new AgentArray()) { ... }
public class SquadData() : Dataset<Squad, SquadArray>(new SquadArray()) { ... }
```

### Entities

#### `Agent` — The Core Entity

**This is the most important class in Phobos's architecture:**

```csharp
// Phobos/Phobos/Entities/Agent.cs
public class Agent(int id, BotOwner bot, float[] taskScores) : Entity(id, taskScores)
{
    // Components are EMBEDDED as fields — NOT stored in ComponentArray<T>
    public readonly Movement Movement = new();
    public readonly Stuck Stuck = new();
    public readonly Look Look = new();
    public readonly Objective Objective = new();
    public readonly Guard Guard = new();

    // BSG interop references
    public readonly BotOwner Bot = bot;
    public Player Player => Bot.GetPlayer;

    // Convenience accessors
    public Vector3 Position => Player.Position;
    public Vector3 LookDirection => Player.LookDirection;
    public float Speed => Player.Velocity.magnitude;
    // ... more convenience properties
}
```

**Key insight**: Components are **regular class fields on Agent**, not stored in
separate `ComponentArray<T>` instances. This makes Phobos's architecture
**AoS (Array of Structures)**, not SoA. Each `Agent` object contains all its
component data inline (though components are reference types on the heap, so
"inline" means "one pointer dereference away" — not truly value-embedded).

#### `Squad` — Group Entity

```csharp
// Phobos/Phobos/Entities/Squad.cs
public class Squad(int id, float[] taskScores, int targetMembersCount) : Entity(id, taskScores)
{
    public readonly List<Agent> Members = new(targetMembersCount);
    public readonly SquadObjective Objective = new();
    public Agent Leader;
}
```

### Components

All components are **mutable classes** (not structs):

```csharp
// Phobos/Phobos/Components/Movement.cs
public class Movement
{
    public Vector3 Target;
    public Vector3[] Path = [];
    public MovementStatus Status;
    public float Speed;
    public float Pose;
    public bool Sprint;
    public bool Prone;
    public Urgency Urgency;
    public VoxelUpdatePacing VoxelUpdatePacing = VoxelUpdatePacing.Normal;
}

// Phobos/Phobos/Components/Stuck.cs
public class Stuck
{
    public readonly HardStuck Hard = new();
    public readonly SoftStuck Soft = new();
}

// Phobos/Phobos/Components/Objective.cs
public class Objective
{
    public Vector3 Location;
    public Vector3[] ArrivalPath = [];
}
```

### Systems

Systems are **static classes** with `Update` methods that iterate the dense
agent list:

```csharp
// Phobos/Phobos/Systems/MovementSystem.cs
public static class MovementSystem
{
    public static void Update(List<Agent> agents)
    {
        for (int i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            // Direct field access — no dictionary lookup, no component query
            agent.Movement.Target = ...;
            agent.Stuck.Soft.Update(agent.Speed);
            agent.Bot.Mover.SetTarget(...);
        }
    }
}

// Phobos/Phobos/Systems/LookSystem.cs
public static class LookSystem
{
    public static void Update(List<Agent> agents)
    {
        for (int i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            agent.Look.Target = ...;
        }
    }
}
```

### Orchestration

```csharp
// Phobos/Phobos/Orchestration/PhobosManager.cs
public class PhobosManager : MonoBehaviour
{
    private AgentData _agentData = new();
    private SquadData _squadData = new();

    // Dense list reference for system iteration
    private List<Agent> _liveAgents => _agentData.Entities.Values;

    public void Update()
    {
        // Deterministic tick order
        LocationSystem.Update();
        StrategyManager.Update();
        ActionManager.Update();
        MovementSystem.Update(_liveAgents);   // Dense for-loop
        LookSystem.Update(_liveAgents);       // Dense for-loop
        NavJobExecutor.Update();
    }
}
```

### Additional Patterns

#### `BsgBotRegistry` — BSG ID → Agent Lookup

Phobos uses a **sparse array** (not a dictionary) for O(1) bot lookup by BSG's
integer bot ID:

```csharp
// Phobos/Phobos/Orchestration/BsgBotRegistry.cs
public class BsgBotRegistry
{
    private readonly List<Agent> _agents = [];

    [AggressiveInlining]
    public bool IsPhobosActive(BotOwner bot)
    {
        var bsgId = bot.Id;
        if (bsgId >= _agents.Count) return false;
        var agent = _agents[bsgId];
        return agent != null && agent.IsActive;
    }
}
```

This avoids dictionary hash computation entirely — just an array index.

#### Rate Limiting with `TimePacing`

Phobos rate-limits expensive systems to avoid per-frame cost:

- `LocationSystem` convergence field: every **30 seconds**
- `StrategyManager` (squad-level decisions): every **0.5 seconds**
- `Stuck.Pacing`: every **0.1 seconds** per agent
- `ActionManager`: **every frame** (lightweight utility scoring)

#### `SwapRemoveAt` — O(1) List Removal

```csharp
// Used throughout Phobos for maintaining packed arrays
public static void SwapRemoveAt<T>(this List<T> list, int index)
{
    list[index] = list[list.Count - 1];
    list.RemoveAt(list.Count - 1);
}
```

### Summary of Phobos's Actual Pattern

| Aspect | Phobos's Approach |
|--------|-------------------|
| Entity storage | Dense `List<Agent>` with swap-remove |
| Component storage | Embedded on Agent as class fields |
| Component access | Direct field access: `agent.Movement.Target` |
| System iteration | `for (int i = 0; i < agents.Count; i++)` |
| ID management | `EntityArray` with free stack for ID reuse |
| Layout | AoS (Array of Structures) — NOT SoA |
| BSG interop | Agent holds `BotOwner` and `Player` references |
| MonoBehaviour | Only `PhobosManager` is a MonoBehaviour |

---

## QuestingBots Current Data Layout

### Data Structure Inventory

#### HiveMind (50ms tick)

```
BotHiveMindMonitor.cs:
├── deadBots: List<BotOwner>              — dead bot tracking
├── botBosses: Dictionary<BotOwner, BotOwner>    — bot → boss mapping
├── botFollowers: Dictionary<BotOwner, List<BotOwner>>  — boss → followers
└── sensors: Dictionary<BotHiveMindSensorType, BotHiveMindAbstractSensor>
    └── each sensor holds: Dictionary<BotOwner, bool>  — per-bot state
```

- 5 sensors × 1 dictionary each = 5 dictionaries of `<BotOwner, bool>`
- Update iterates `botBosses.Keys` and `botFollowers.Keys` (dictionary key enumeration)
- Each sensor's `Update()` iterates `botState.Keys` (another dictionary enumeration)
- **Hot path**: 7 dictionary enumerations per 50ms tick

#### Bot Registration

```
BotRegistrationManager.cs:
├── registeredPMCs: HashSet<BotOwner>     — O(1) PMC lookup
├── registeredBosses: HashSet<BotOwner>   — O(1) boss lookup
├── hostileGroups: List<BotsGroup>        — hostile group tracking
└── sleepingBotIds: List<string>          — sleeping bot IDs
```

- `sleepingBotIds` uses `List<string>.Contains()` — O(n) linear scan
- Registration is event-driven (cold path)

#### Job Assignment

```
BotJobAssignmentFactory.cs:
├── allQuests: List<Quest>                — all quest definitions
├── _possibleQuestsBuffer: List<Quest>    — reusable buffer (Phase 1 optimization)
└── botJobAssignments: Dictionary<string, List<BotJobAssignment>>  — per-bot history
```

- `GetAllPossibleQuests()`: uses `_possibleQuestsBuffer` with for-loop (optimized)
- `NumberOfActiveBots()`: nested `foreach` over `botJobAssignments.Keys` + inner
  for-loop — **O(N × A)** where N=bots, A=avg assignments. Called per quest in
  `GetAllPossibleQuests()`, making total complexity **O(Q × N × A)** — quadratic
  in bot count.
- `GetRandomQuest()`: heavy LINQ — 3× `ToDictionary`, `OrderBy`, `.ToArray()`,
  `new System.Random()` — allocates 3 dictionaries + multiple iterators per call
- `RemainingObjectivesForBot()`: LINQ `.Where().Where().NearestToBot()`
- `botJobAssignments` inner lists **grow unbounded** — completed/failed/archived
  assignments are never pruned, only status-changed. Over a long raid, each bot
  accumulates many assignments that must all be scanned.
- **Hot path**: `GetNewBotJobAssignment()` called when bots need new quests

#### Zone Movement

```
WorldGridManager.cs:
├── Grid: WorldGrid                       — 2D cell array
├── advectionField: AdvectionField        — zone attraction
├── convergenceField: ConvergenceField    — player attraction
├── botFieldStates: Dictionary<string, BotFieldState>  — per-bot momentum/noise
├── cachedPlayerPositions: List<Vector3>  — refreshed periodically
└── cachedBotPositions: List<Vector3>     — refreshed periodically
```

- Field computation iterates zone sources and bot/player positions
- Per-bot state looked up via `Dictionary<string, BotFieldState>`
- **Moderate path**: convergence refresh every 30s, destination computation on demand

#### Per-Bot State (Scattered)

Each bot's state is distributed across multiple systems:

```
BotOwner (BSG object)
├── .Profile.Id                    — bot identity (used as dictionary key)
├── .Position                      — current position
├── .GetPlayer                     — Player reference
└── .GetOrAddObjectiveManager()    — MonoBehaviour component on bot's GameObject
    ├── BotMonitor                 — health/combat/extract monitors
    ├── Quest references           — current quest/objective
    └── Timer state                — stuck timers, sprint timers
```

### Iteration Pattern Summary

| System | Data Access | Iteration Pattern |
|--------|-------------|-------------------|
| HiveMind sensors | `Dictionary<BotOwner, bool>` | `foreach` over `.Keys` |
| HiveMind bosses | `Dictionary<BotOwner, BotOwner>` | `foreach` over `.Keys` |
| HiveMind followers | `Dictionary<BotOwner, List<BotOwner>>` | `foreach` over `.Keys` + inner list |
| Bot registration | `HashSet<BotOwner>` | `.Contains()` (O(1)) |
| Quest selection | `List<Quest>` + `_possibleQuestsBuffer` | for-loop (optimized) |
| Active bot count | `Dictionary<string, List<BotJobAssignment>>` | nested foreach + for |
| Zone field state | `Dictionary<string, BotFieldState>` | single lookup per bot |
| Per-bot objective | `BotOwner.GetOrAddObjectiveManager()` | MonoBehaviour on GameObject |

### Allocation Hotspots

| Location | Allocation | Frequency |
|----------|-----------|-----------|
| `GetRandomQuest()` | Multiple `ToDictionary`, `OrderBy`, `.ToArray()` | Per quest selection |
| `GetAllGroupMembers()` | `new List<BotOwner>` + `new ReadOnlyCollection` | Per call |
| `GetFollowers()` | `new ReadOnlyCollection<BotOwner>` | Per call |
| `SeparateBotFromGroup()` | `new List<BotOwner>` for old group members | Per separation |
| `RemainingObjectivesForBot()` | LINQ iterator allocations | Per quest check |
| `NearestToBot()` | `new Dictionary<QuestObjective, float>` | Per objective search |
| `BotFieldState.GetNoiseAngle()` | `new System.Random(seed)` | Per zone destination |

### Hot Path Frequency Map

| Frequency | System | Per-Tick Cost (N bots) |
|-----------|--------|----------------------|
| Every 50 ms | HiveMind sensors | 5 sensors × N dictionary reads + N boss/follower iterations |
| Every 100 ms | BotMonitorController | 8 monitors × N bots (Dictionary\<Type\> lookups) |
| Every 200 ms | BotObjectiveManager | N bots (includes O(N) `sleepingBotIds` scan) |
| On assignment | BotJobAssignmentFactory | **O(Q × N × A)** — quadratic in bot count |
| Every 30 s | WorldGridManager | AllAlivePlayersList scan |

### Worst Offenders (Ranked)

1. **`NumberOfActiveBots()` quadratic blowup** — O(bots × assignments) per quest,
   called Q times during assignment search
2. **`GetRandomQuest()` allocations** — 3 dictionaries + Random + LINQ per call
3. **`deadBots` as `List<BotOwner>`** — O(N) `Contains()` called inside 50ms
   hot loop (`updateBosses`, `updateBossFollowers`)
4. **`GetFollowers()` / `GetAllGroupMembers()`** — allocate ReadOnlyCollection
   every call (called frequently from BigBrain layers)
5. **`sleepingBotIds` as `List<string>`** — O(N) `Contains()` per bot per 200ms
6. **`BotFieldState.GetNoiseAngle()`** — `new System.Random()` per call

---

## Side-by-Side Comparison

| Aspect | Phobos | QuestingBots | Pure ECS |
|--------|--------|-------------|----------|
| **Entity storage** | Dense `List<Agent>` | Scattered dictionaries keyed by `BotOwner` | Dense typed arrays |
| **Component storage** | Embedded on Agent | MonoBehaviour + dictionaries | Separate contiguous arrays |
| **Component access** | `agent.Movement.Target` | `bot.GetOrAddObjectiveManager()` | `movements[entityId]` |
| **System iteration** | `for` over dense list | `foreach` over dictionary keys | `for` over component arrays |
| **ID management** | EntityArray with free stack | Profile.Id strings in dictionaries | Integer IDs, slot arrays |
| **BSG interop** | Agent.Bot / Agent.Player | BotOwner directly | Wrapper required |
| **MonoBehaviours** | 1 (PhobosManager) | Many (per-bot + global) | 0–1 (orchestrator only) |
| **Memory layout** | AoS (coherent per entity) | Scattered (dictionary + heap) | SoA (coherent per component) |
| **GC pressure** | Low (pre-allocated, recycled) | Medium (LINQ, collections) | Lowest (fixed arrays) |
| **Testability** | High (static systems) | Medium (MonoBehaviour coupling) | Highest (pure functions) |

---

## Pros and Cons for QuestingBots

### Pros of Adopting ECS-Lite Patterns

#### 1. Centralized Bot State
**Current**: Bot state scattered across 5+ dictionaries (HiveMind), a
MonoBehaviour (BotObjectiveManager), and a separate dictionary
(WorldGridManager.botFieldStates). Finding "what is bot X doing?" requires
checking multiple systems.

**With ECS-Lite**: All bot state on a single `BotEntity` object. Debugging and
logging become trivial: `entity.Movement`, `entity.Stuck`, `entity.Assignment`.

#### 2. Dense Iteration Eliminates Dictionary Overhead
**Current**: HiveMind iterates `Dictionary<BotOwner, bool>.Keys` — this involves
enumerator allocation, hash bucket traversal, and unpredictable memory access.

**With ECS-Lite**: A `for` loop over `List<BotEntity>` — sequential access,
hardware prefetcher engaged, zero allocations.

#### 3. Elimination of Dictionary Key Problems
**Current**: Several dictionaries use `BotOwner` as key. `BotOwner` is a Unity
object that can become null when despawned, requiring null checks on every
access. Dead bot cleanup requires iterating dictionaries and deferring removal.

**With ECS-Lite**: Swap-remove from dense list on bot death. No null keys, no
deferred removal, no stale references.

#### 4. Pattern Alignment with Phobos
Porting systems from Phobos (e.g., improved look behavior, squad tactics, door
interaction) becomes straightforward when the data layout matches. Currently,
adapting Phobos code requires translating between its `Agent`-centric model and
QuestingBots' dictionary-based model.

#### 5. Better Testability
**Current**: `BotHiveMindMonitor` is a `MonoBehaviour` — cannot be instantiated
in unit tests without Unity runtime.

**With ECS-Lite**: Systems are static methods taking `List<BotEntity>` — trivially
testable with no Unity dependency.

#### 6. Reduced GC Pressure
Replace per-call allocations in `GetAllGroupMembers()`, `GetFollowers()`,
`RemainingObjectivesForBot()` etc. with pre-allocated buffers or direct entity
access.

### Cons of Adopting ECS-Lite Patterns

#### 1. Large Refactoring Effort
Touching HiveMind, BotRegistrationManager, BotJobAssignmentFactory,
BotObjectiveManager, and all Harmony patches that access bot state. Estimated
20+ files affected.

#### 2. Negligible Performance Gain at Current Scale
At 20–40 bots, the working set fits in L1 cache regardless of layout. Dense
iteration over a list vs. dictionary enumeration saves microseconds per tick —
unmeasurable in practice.

#### 3. BSG API Coupling Cannot Be Eliminated
We must keep `BotOwner` and `Player` references because BigBrain layers,
Harmony patches, and BSG's own systems use them. The `BotEntity` would hold
these references (like Phobos's `Agent.Bot`), not replace them.

#### 4. BigBrain Layer Compatibility
BigBrain's `CustomLayer` and `CustomLogic` are MonoBehaviour-based. The
objective manager must remain accessible from these layers. An ECS-Lite approach
would need a thin MonoBehaviour adapter.

#### 5. Risk of Introducing Regressions
The current system is stable, tested, and working. A data layout refactor
touches the core of bot management. Even with comprehensive tests, subtle
ordering bugs or null reference issues could emerge.

#### 6. Two Systems During Migration
During the transition period, some code would use the old dictionary-based
approach while other code uses the new entity-based approach, creating confusion
and potential inconsistencies.

---

## Feasibility Assessment

### Option A: Full Pure ECS — Not Feasible

Requires replacing BSG's `BotOwner` with custom entity references throughout,
rewriting BigBrain layer integration, and maintaining separate component arrays.
The effort vastly exceeds the benefit at SPT's scale.

**Verdict**: ❌ Reject

### Option B: Phobos-Style "ECS-Lite" — Feasible but Large

Adopt Phobos's exact pattern: `BotEntity` with embedded components, dense list,
static systems. This is a proven pattern (Phobos ships and runs) but requires
significant refactoring.

**Verdict**: ⚠️ Feasible, large effort, moderate benefit

### Option C: Targeted Extraction — Best ROI

Extract only the highest-value patterns from Phobos without a full rewrite:
1. Dense bot registry (replace multiple dictionaries)
2. Centralized per-bot state object (reduce scatter)
3. Static system methods where testability matters most

**Verdict**: ✅ Recommended — 80% of benefit at 20% of effort

---

## Recommended Implementation Plan

### Phase 1: BotEntity + BotRegistry (Foundation)

**Goal**: Single source of truth for all registered bots, dense iteration.

**New files**:
- `BotLogic/ECS/BotEntity.cs` — Per-bot data container
- `BotLogic/ECS/BotRegistry.cs` — Dense entity list with swap-remove

```csharp
// BotEntity.cs — All per-bot state centralized
public class BotEntity
{
    public int Id;                    // Slot ID (recycled)
    public BotOwner Bot;              // BSG reference (required)
    public Player Player;             // Cached player reference

    // Embedded components (like Phobos Agent)
    public BotType BotType;           // PMC, Scav, PScav, Boss
    public bool IsSleeping;           // Replaces sleepingBotIds list
    public BotOwner Boss;             // Replaces botBosses dictionary
    public List<BotEntity> Followers; // Replaces botFollowers dictionary
}

// BotRegistry.cs — Dense storage with swap-remove
public class BotRegistry
{
    public readonly List<BotEntity> Entities = new(32);
    private readonly Dictionary<BotOwner, int> _botToIndex = new(32);
    private readonly Stack<int> _freeIds = new();

    public BotEntity Register(BotOwner bot) { ... }
    public void Remove(BotOwner bot) { ... }
    public BotEntity GetEntity(BotOwner bot) { ... }
}
```

**Migration**:
- Replace `BotRegistrationManager.registeredPMCs` / `registeredBosses` with
  `BotRegistry` + `BotEntity.BotType`
- Replace `BotHiveMindMonitor.botBosses` / `botFollowers` with
  `BotEntity.Boss` / `BotEntity.Followers`
- HiveMind sensors iterate `BotRegistry.Entities` instead of dictionary keys

**Tests**: BotEntity creation, BotRegistry add/remove/swap-remove, ID recycling,
dense iteration correctness.

**Estimated effort**: Medium (8–12 files touched)

### Phase 2: Sensor Migration + Embedded State

**Goal**: Move HiveMind sensor data onto BotEntity, eliminate per-sensor dictionaries.

**Changes to BotEntity**:
```csharp
public class BotEntity
{
    // ... Phase 1 fields ...

    // Sensor state (replaces 5 Dictionary<BotOwner, bool>)
    public bool IsInCombat;
    public bool IsSuspicious;
    public bool CanQuest;
    public bool CanSprintToObjective;
    public bool WantsToLoot;

    // Zone movement (replaces Dictionary<string, BotFieldState>)
    public BotFieldState FieldState;
}
```

**Migration**:
- Replace `BotHiveMindAbstractSensor.botState` dictionaries with direct field
  access on `BotEntity`
- Sensor `Update()` methods become static methods iterating `BotRegistry.Entities`
- `WorldGridManager.botFieldStates` dictionary → `BotEntity.FieldState`

**Tests**: Sensor state update via BotEntity, field state lifecycle.

**Estimated effort**: Medium (6–8 files touched)

### Phase 3: System Extraction

**Goal**: Extract hot-path logic into static system methods for testability.

**New files**:
- `BotLogic/ECS/Systems/HiveMindSystem.cs`
- `BotLogic/ECS/Systems/StuckSystem.cs`
- `BotLogic/ECS/Systems/FieldSystem.cs`

```csharp
public static class HiveMindSystem
{
    public static void UpdateBosses(List<BotEntity> entities) { ... }
    public static void UpdateFollowers(List<BotEntity> entities) { ... }
    public static void UpdateSensors(List<BotEntity> entities) { ... }
}

public static class StuckSystem
{
    public static void Update(List<BotEntity> entities, float deltaTime) { ... }
}
```

**Migration**:
- `BotHiveMindMonitor.Update()` delegates to `HiveMindSystem.Update(registry.Entities)`
- Stuck detection logic moves from MonoBehaviour to `StuckSystem`
- BigBrain layers read state from `BotEntity` instead of querying sensors

**Tests**: Full system-level tests with mock BotEntity lists. No Unity dependency.

**Estimated effort**: Medium-High (10–15 files touched)

### Phase 4: Optional — Job Assignment Optimization

**Goal**: Reduce LINQ allocations in quest selection hot path.

**Changes**:
- `BotJobAssignment` list stored on `BotEntity` instead of
  `Dictionary<string, List<BotJobAssignment>>`
- `GetRandomQuest()`: replace `ToDictionary` + `OrderBy` chains with
  pre-allocated arrays and manual sorting
- `NumberOfActiveBots()`: iterate `BotRegistry.Entities` instead of
  dictionary keys

**Estimated effort**: Medium (3–5 files touched)

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Regression in bot behavior | Medium | High | Comprehensive test suite before/after, A/B testing in raids |
| BigBrain layer compatibility | Low | Medium | Thin adapter MonoBehaviour that delegates to BotEntity |
| Null BotOwner during despawn | Medium | Medium | Swap-remove on death event eliminates stale references |
| Performance regression | Low | Low | Profile before/after; at 40 bots, layout doesn't matter |
| Extended migration period | Medium | Medium | Phase the migration; each phase is independently shippable |
| Harmony patch breakage | Medium | High | Patches need updating to find BotEntity via BotRegistry |

### Migration Strategy

Each phase should:
1. Be completed in a single branch
2. Maintain backward compatibility during transition (old + new paths)
3. Include comprehensive tests before removing old code paths
4. Be verified with full `make ci` + in-game testing

---

## Implementation Status

### Completed Phases

| Phase | Description | Status |
|-------|-------------|--------|
| Phase 1 | BotEntity + BotRegistry (Foundation) | ✅ Complete |
| Phase 2 | Sensor Bools + Embedded State | ✅ Complete |
| Phase 3 | HiveMindSystem Static Methods | ✅ Complete |
| Phase 4 | QuestScorer Job Assignment Optimization | ✅ Complete |
| ECS Wiring | BotEntityBridge Dual-Write + Read Layer | ✅ Complete |
| ECS Migration | All External Reads Switched to ECS | ✅ Complete |
| Phase 5A | Close Dual-Write Gaps | ✅ Complete |
| Phase 5B | Migrate Push Sensor Writes (ECS-only) | ✅ Complete |
| Phase 5C | Migrate Pull Sensor Writes (dense iteration) | ✅ Complete |
| Phase 5D | Migrate Boss/Follower Lifecycle Writes | ✅ Complete |
| Phase 5E | Migrate BotRegistrationManager Reads | ✅ Complete |
| Phase 5F | Remove Old Data Structures | ✅ Complete |
| Phase 6 | BotFieldState on BotEntity | ✅ Complete |
| Phase 7A | BsgBotRegistry Sparse Array | ✅ Complete |
| Phase 7B | TimePacing / FramePacing Utilities | ✅ Complete |
| Phase 7C | Deterministic Tick Order | ✅ Complete |
| Phase 7D | Allocation Cleanup | ✅ Complete |
| Phase 8 | Job Assignment State on BotEntity | ⚠️ Field added, not yet wired |

**Current architecture**: ECS is the sole data store. All old dictionaries
(`deadBots`, `botBosses`, `botFollowers`, `sensors`) have been deleted from
`BotHiveMindMonitor`. All 6 sensor subclasses deleted. `sleepingBotIds`
deleted from `BotRegistrationManager`. Push sensors write only to ECS entities.
Pull sensors iterate the dense entity list with zero delegate allocation.
Boss/follower lifecycle uses ECS `entity.IsActive` for O(1) dead checks.
`BotRegistrationManager` retains `registeredPMCs`/`registeredBosses` only for
`GetBotType()` classification during registration and hostility management.
463 client tests, 58 server tests (521 total).

### What Remains: Full Option B Completion

Phases 5A–5F completed the full ECS migration, Phases 7C–7D completed
deterministic tick order documentation and allocation cleanup. What remains:

1. **Phase 8 wiring** — connect `BotEntity.ConsecutiveFailedAssignments` to
   `BotJobAssignmentFactory` (field exists, wiring remains).

---

## Phase 5: Eliminate Dual-Write (Single Source of Truth)

This is the critical remaining phase. It has six sub-phases ordered by
dependency.

### Phase 5A: Close Dual-Write Gaps

**Status: ✅ Complete**

Several write operations were going **only** to old dictionaries, not to ECS.
These gaps were closed to enable eventual dictionary removal.

| Write Operation | File:Line | Gap |
|-----------------|-----------|-----|
| Boss discovery from game API | `BotHiveMindMonitor.cs:233` | `botBosses[bot] = boss` writes to old dict only. `BotEntityBridge.SyncBossFollower()` is called later from `addBossFollower()` for the follower relationship, but the boss assignment itself is not dual-written. |
| Dead-bot cleanup in follower updates | `BotHiveMindMonitor.cs:293-348` | Dead bosses/followers removed from `botFollowers` but `BotEntityBridge.DeactivateBot()` is NOT called. |
| Sleep registration | `BotRegistrationManager.cs:169-190` | `RegisterSleepingBot()`/`UnregisterSleepingBot()` write to `sleepingBotIds` but do NOT call `BotEntityBridge.SetSleeping()`. |

**Changes implemented**:
- In `updateBosses()`: `BotEntityBridge.DeactivateBot(bossBot)` called when
  boss dies (line 282)
- In `updateBossFollowers()`: `BotEntityBridge.DeactivateBot(boss)` and
  `BotEntityBridge.DeactivateBot(follower)` called when adding to `deadBots`
  (lines 345, 381)
- In `RegisterSleepingBot()`/`UnregisterSleepingBot()`:
  `BotEntityBridge.SetSleeping(botOwner, true/false)` called (lines 178, 193)
- `BotHiveMindMonitor.Clear()` calls `BotEntityBridge.Clear()` (line 53)

### Phase 5B: Migrate Push Sensor Writes

**Status: ✅ Complete**

Push sensors (InCombat, IsSuspicious, WantsToLoot) are externally set via
`BotHiveMindMonitor.UpdateValueForBot()`. Previously this dual-wrote to both
old dictionary and ECS:

```
BotHiveMindMonitor.UpdateValueForBot(sensorType, bot, value)
  → sensors[sensorType].UpdateForBot(bot, value)     // WRITE #1: old dict
  → BotEntityBridge.UpdateSensor(sensorType, bot, value)  // WRITE #2: ECS
```

**Implementation**: Old dictionary write removed from `UpdateValueForBot()`.
Now writes only to ECS via `BotEntityBridge.UpdateSensor()`. For `WantsToLoot`,
`BotEntityBridge.UpdateLastLootingTime()` is called directly from
`UpdateValueForBot()` when `value == true` (lines 87-90 in
`BotHiveMindMonitor.cs`).

### Phase 5C: Migrate Pull Sensor Writes

**Status: ✅ Complete**

Pull sensors (CanQuest, CanSprintToObjective) self-update in the 50ms tick by
reading `BotObjectiveManager` state:

```
BotHiveMindCanQuestSensor.Update()
  → iterates botState.Keys (old dictionary)
  → reads bot.GetObjectiveManager().IsQuestingAllowed
  → botState[bot] = value            // WRITE #1: old dict
  → BotEntityBridge.UpdateSensor(...) // WRITE #2: ECS
```

**Implementation**: Instead of `ForEachBot` with a delegate (which would
allocate), the solution iterates `BotEntityBridge.Registry.Entities` directly
in a private `updatePullSensors()` method inlined into the tick:

```csharp
// BotHiveMindMonitor.cs — updatePullSensors()
var entities = ECS.BotEntityBridge.Registry.Entities;
for (int i = 0; i < entities.Count; i++)
{
    var entity = entities[i];
    if (!entity.IsActive) continue;
    var bot = ECS.BotEntityBridge.GetBotOwner(entity);
    if (bot == null || !bot.isActiveAndEnabled || bot.IsDead) continue;
    var objectiveManager = bot.GetObjectiveManager();
    entity.CanQuest = objectiveManager != null && objectiveManager.IsQuestingAllowed;
    entity.CanSprintToObjective = objectiveManager == null || objectiveManager.CanSprintToObjective();
}
```

**Bonus achieved**: Zero delegate allocation per tick — pure for-loop over
dense list.

### Phase 5D: Migrate Boss/Follower Lifecycle Writes

**Status: ✅ Complete**

`BotHiveMindMonitor.updateBosses()` and `updateBossFollowers()` previously:
1. Iterated `botBosses.Keys` and `botFollowers.Keys` (old dictionaries)
2. Used `deadBots.Contains()` — O(n) linear scan
3. Wrote boss/follower changes to old dictionaries
4. Called `BotEntityBridge.SyncBossFollower()` only from `addBossFollower()`

**Implementation**:
- `updateBosses()` iterates `BotEntityBridge.Registry.Entities` (dense list)
- `entity.IsActive` replaces `deadBots.Contains(bot)` — O(1) instead of O(n)
- `entity.HasBoss` used for boss discovery check
- `updateBossFollowers()` calls `HiveMindSystem.CleanupDeadEntities()` first
  for ECS-side boss/follower reference cleanup
- Old dictionary writes retained alongside ECS writes (Phase 5F will remove)

### Phase 5E: Migrate BotRegistrationManager Reads

**Status: ✅ Complete**

Several `BotRegistrationManager` methods read from collections that were
redundant with `BotEntity` fields:

| Old Method | Reads From | ECS Equivalent |
|-----------|-----------|----------------|
| `IsBotSleeping(string botId)` | `sleepingBotIds.Contains(botId)` O(n) | `BotEntity.IsSleeping` O(1) |
| `IsBotAPMC(BotOwner)` | `registeredPMCs.Contains(bot)` | `BotEntity.BotType == PMC` |
| `GetBotType(BotOwner)` | `registeredPMCs`, `registeredBosses` | `BotEntity.BotType` |
| `IsARegisteredPMC(BotOwner)` | `registeredPMCs.Contains(bot)` | `BotEntity.BotType == PMC` |
| `IsARegisteredBoss(BotOwner)` | `registeredBosses.Contains(bot)` | `BotEntity.BotType == Boss` |

**Callers to update** (8 call sites):
- `ItemHelpers.cs:180` — `IsBotAPMC()`
- `BotObjectiveManager.cs:119` — `GetBotType()`
- `BotObjectiveManager.cs:184` — `IsBotSleeping()`
- `BotOwnerBrainActivatePatch.cs:59,98` — `GetBotType()`
- `CheckLookEnemyPatch.cs:31` — `IsBotSleeping()`
- `Quest.cs:273` — `IsBotAPMC()`
- `GoToObjectiveAction.cs:246` — `GetBotType()`

**Implementation**: `BotEntityBridge` now exposes:
- `IsBotSleeping(string profileId)` — O(1) via `_profileIdToEntity` dictionary
  (replaces `sleepingBotIds.Contains()` O(n) list scan)
- `IsBotAPMC(BotOwner)` — reads `entity.BotType == BotType.PMC`
- `GetBotType(BotOwner)` — reads `entity.BotType` with `MapBotTypeReverse()`
- `_profileIdToEntity` dictionary populated in `RegisterBot()` from
  `bot.Profile.Id`, cleared in `Clear()`

**Kept** in `BotRegistrationManager`: `hostileGroups` (game-side hostility,
not per-bot), `SpawnedBotCount` and other spawn statistics, `PMCs` / `Bosses`
collection properties (used for enumeration by spawning code).
`RegisterPMC()`, `RegisterBoss()`, `RegisterSleepingBot()`,
`UnregisterSleepingBot()` still write to old collections in parallel with ECS
(Phase 5F will remove the old collections).

### Phase 5F: Remove Old Data Structures

**Status: ✅ Complete**

Deleted all old data structures now replaced by ECS:

**Deleted from `BotHiveMindMonitor`** (net ~200 lines removed):
- `deadBots: List<BotOwner>` → `BotEntity.IsActive`
- `botBosses: Dictionary<BotOwner, BotOwner>` → `BotEntity.Boss`
- `botFollowers: Dictionary<BotOwner, List<BotOwner>>` → `BotEntity.Followers`
- `sensors: Dictionary<BotHiveMindSensorType, BotHiveMindAbstractSensor>`
- `_deadBossBuffer: List<BotOwner>` (old-dict cleanup helper)
- `RegisterBot()` method (only registered into old dicts/sensors)
- `GetBoss()` method (only called by deleted sensor classes)
- `throwIfSensorNotRegistred()` method
- All old-dict writes in `updateBosses()`, `updateBossFollowers()`,
  `addBossFollower()`, `SeparateBotFromGroup()`, `Clear()`
- `addBossFollower()` now checks ECS `followerEntity.Boss == bossEntity`
  instead of `botFollowers[boss].Contains(bot)`
- `updateBossFollowers()` reduced to single `CleanupDeadEntities()` call

**Deleted 6 sensor files**:
- `BotHiveMindAbstractSensor.cs` (161 lines) — base class with dict + queries
- `BotHiveMindIsInCombatSensor.cs` (15 lines) — empty subclass
- `BotHiveMindIsSuspiciousSensor.cs` (15 lines) — empty subclass
- `BotHiveMindWantsToLootSensor.cs` (39 lines) — `botLastLootingTime` dict
- `BotHiveMindCanQuestSensor.cs` (41 lines) — `Action<>` delegate alloc
- `BotHiveMindCanSprintToObjectiveSensor.cs` (41 lines) — same pattern

**Deleted from `BotRegistrationManager`**:
- `sleepingBotIds: List<string>` → `BotEntity.IsSleeping`
- `IsBotSleeping(string)` → `BotEntityBridge.IsBotSleeping()`
- `PMCs` / `Bosses` properties (no external callers)
- `IsARegisteredPMC` / `IsARegisteredBoss` extension methods (no callers)
- `RegisterSleepingBot` / `UnregisterSleepingBot` now use ECS guard

**Kept in `BotRegistrationManager`** (still needed):
- `registeredPMCs` / `registeredBosses` — used by `GetBotType()` during
  registration and by `updateHostileGroupEnemies()` for hostility management
- `hostileGroups` — game-side hostility, not per-bot state

**Other changes**:
- `BotOwnerBrainActivatePatch`: removed `BotHiveMindMonitor.RegisterBot()` call
- `BotInfoGizmo`: switched to `BotEntityBridge.IsBotSleeping()` / `.GetBoss()`

---

## Phase 6: BotFieldState on BotEntity

**Status: ✅ Complete**

Moved zone movement per-bot state from `WorldGridManager.botFieldStates`
dictionary onto ECS entities via `BotEntityBridge`.

**Fields on `BotEntity`**:
```csharp
public int FieldNoiseSeed;    // Per-bot noise seed (from profile ID hash)
public bool HasFieldState;    // Whether field state is initialized
```

**Implementation**:
- `BotEntityBridge` stores `Dictionary<int, BotFieldState>` keyed by entity ID
- `RegisterBot()` creates `BotFieldState(profileId.GetHashCode())`, sets
  `entity.FieldNoiseSeed` and `entity.HasFieldState = true`
- `GetFieldState(BotOwner)` and `GetFieldState(string profileId)` provide
  O(1) lookups replacing `WorldGridManager.GetOrCreateBotState()`
- `Clear()` clears the field state dictionary
- `WorldGridManager.botFieldStates` dictionary removed
- `WorldGridManager.GetOrCreateBotState()` method removed
- `WorldGridManager.GetRecommendedDestination()` now calls
  `BotEntityBridge.GetFieldState(botProfileId)` — returns null if bot not
  registered (safe: `ZoneObjectiveCycler` already handles null destinations)

---

## Phase 7: Additional Phobos Patterns

High-value patterns from Phobos, ordered by impact (7A and 7B complete):

### 7A: BsgBotRegistry Sparse Array

**Status: ✅ Complete**

Phobos uses a **separate sparse array** (not a dictionary) for O(1)
`BotOwner.Id → Agent` lookup, used in hot-path Harmony patches:

```csharp
// Phobos: BsgBotRegistry.cs
private readonly List<Agent> _agents = [];  // sparse, null-padded

[AggressiveInlining]
public bool IsPhobosActive(BotOwner bot)
{
    var bsgId = bot.Id;
    if (bsgId >= _agents.Count) return false;
    var agent = _agents[bsgId];
    return agent != null && agent.IsActive;
}
```

**Implementation**: Integrated directly into `BotRegistry` rather than a
separate class:
- `BotRegistry.Add(int bsgId)` — registers entity with BSG ID in sparse array
- `BotRegistry.GetByBsgId(int bsgId)` — O(1) lookup, `[AggressiveInlining]`
- `BotRegistry.ClearBsgId(int bsgId)` — nullifies the sparse slot
- `BotEntityBridge.RegisterBot()` calls `_registry.Add(bot.Id)` (line 50)
- `BotEntityBridge.GetEntityByBsgId(int bsgId)` exposes the lookup
- **Note**: `Remove()` does NOT auto-clear the BSG ID mapping — explicit
  `ClearBsgId()` or `Clear()` required (documented by tests)

### 7B: TimePacing / FramePacing Utilities

**Status: ✅ Complete** (classes created, wiring incremental)

Two reusable rate-limiter classes created in `Helpers/`:

- **`TimePacing`** (`Helpers/TimePacing.cs`, 50 lines) — time-based rate
  limiter with `ShouldRun(float currentTime)` + `Reset()`, both
  `[AggressiveInlining]`. Inspired by Phobos pattern.
- **`FramePacing`** (`Helpers/FramePacing.cs`, 50 lines) — frame-based rate
  limiter with `ShouldRun(int currentFrame)` + `Reset()`, both
  `[AggressiveInlining]`.

Both are pure C# with zero Unity dependencies, fully tested (9 + 10 tests).
Wiring to replace existing ad-hoc timer patterns is incremental and optional.

### 7C: Deterministic Tick Order

**Status: ✅ Complete**

Phobos centralizes all system updates in a single `PhobosManager.Update()`:

```csharp
public void Update()
{
    LocationSystem.Update();       // 1. Spatial grid
    StrategyManager.Update();      // 2. Squad strategies
    ActionManager.Update();        // 3. Agent actions
    MovementSystem.Update(agents); // 4. Movement
    LookSystem.Update(agents);     // 5. Look direction
    NavJobExecutor.Update();       // 6. Batched pathfinding
}
```

**For QuestingBots**: `BotHiveMindMonitor.Update()` already implements
deterministic tick order — all ECS system calls execute in a fixed sequence
within the 50ms tick. Documentation was added to make this explicit:

```csharp
/// Deterministic tick order (50ms interval, Phobos-inspired):
///   1. updateBosses()              — discover/validate boss relationships from BSG API
///   2. updateBossFollowers()        — cleanup dead boss/follower references (CleanupDeadEntities)
///   3. updatePullSensors()          — CanQuest + CanSprintToObjective via dense ECS iteration
///   4. ResetInactiveEntitySensors() — clear sensor state on dead/despawned entities
```

Push sensors (InCombat, IsSuspicious, WantsToLoot) are event-driven via
`UpdateValueForBot()` and do not participate in the tick.

**Note**: The existing `MonoBehaviourDelayedUpdate` base class uses a
wall-clock `Stopwatch` timer (not game-time `TimePacing`). Replacing it was
evaluated but rejected: changing the base class would affect all subclasses,
and wall-clock time is appropriate for the 50ms HiveMind tick.

### 7D: Allocation Cleanup

**Status: ✅ Complete**

All remaining allocation hotspots identified by the audit have been fixed:

| Location | Pattern | Fix | Status |
|----------|---------|-----|--------|
| `CanQuest/CanSprintSensor.Update()` | `new Action<BotOwner>()` per 50ms tick | Cache delegate as static field | ✅ Fixed by Phase 5C (dense iteration, no delegate) |
| `BotEntityBridge.GetFollowers()` | `new List<BotOwner>()` + `new ReadOnlyCollection<>()` per call | Static `_followersBuffer` reused per call, returns `IReadOnlyList<BotOwner>` | ✅ Complete |
| `BotEntityBridge.GetAllGroupMembers()` | Same allocation pattern | Static `_groupMembersBuffer` reused per call, returns `IReadOnlyList<BotOwner>` | ✅ Complete |
| `BotJobAssignmentFactory.NearestToBot()` | `new Dictionary<>()` + `.OrderBy().First()` | O(n) min-scan with local variables, zero allocation | ✅ Complete |
| `BotObjectiveManager.SetExfiliationPointForQuesting()` | `.ToDictionary()` + `.OrderBy().Last()` | O(n) max-scan for-loop, zero allocation | ✅ Complete |
| `BotJobAssignmentFactory.TryArchiveRepeatableAssignments()` | `.Where().Where().ToArray()` | Single for-loop with in-place `Archive()` calls | ✅ Complete |

**Additional optimizations**:
- `GetFollowerCount()` method added for count-only callers (2 call sites in
  `BotJobAssignmentFactory`), avoiding static buffer fill when only the count
  is needed
- `GetLocationOfNearestGroupMember()` inlined to iterate boss + followers
  directly, avoiding the intermediate `GetAllGroupMembers()` collection
- All foreach callers of `GetFollowers()` verified safe for static buffer
  reuse (no nested calls that would corrupt the buffer)

---

## Phase 8: Job Assignment Storage on BotEntity

**Status: ⚠️ Field added, wiring not yet done**

Move per-bot job assignment history from dictionary to entity:

```csharp
// Currently in BotJobAssignmentFactory.cs:
private static Dictionary<string, List<BotJobAssignment>> botJobAssignments;

// Move to BotEntity:
public List<BotJobAssignment> JobAssignments;
```

**Field added** (on `BotEntity`):
```csharp
public int ConsecutiveFailedAssignments;  // Replaces dictionary lookup
```

**Remaining wiring**:
- `NumberOfActiveBots()` → iterate `BotRegistry.Entities` (dense)
- `botJobAssignments` dictionary → `BotEntity.JobAssignments`
- Connect `ConsecutiveFailedAssignments` to `BotJobAssignmentFactory`

**Benefits**:
- `NumberOfActiveBots()` iterates `BotRegistry.Entities` (dense) instead of
  dictionary keys
- No dictionary hash computation per lookup
- Assignment history lives with the entity, not in a separate system

**Estimated effort**: Medium (3-5 files, ~40 lines changed)

---

## Phobos Patterns Not Applicable to QuestingBots

For completeness, these Phobos patterns were evaluated but are **not
recommended** for QuestingBots:

| Pattern | Why Not |
|---------|---------|
| **Squad as first-class Entity** | QuestingBots has simpler group model (boss/followers). Squad entity adds complexity without clear benefit — QuestingBots doesn't have squad-level strategy selection. |
| **Two-tier task hierarchy** (Squad strategies + Agent actions) | QuestingBots uses quest selection, not utility scoring. The quest system is fundamentally different from Phobos's strategy/action model. |
| **ComponentArray<T> extensibility** | Plugin-to-plugin extensibility not needed. Components are embedded on BotEntity. |
| **Movement component with full state machine** | QuestingBots delegates movement to BSG's BotMover. Phobos replaces BotMover entirely. |
| **Look component** | Same reason — Phobos replaces BSG's look system. |
| **Guard component with cover points** | QuestingBots doesn't implement cover behavior. |
| **Singleton<T> lifecycle** | QuestingBots uses static fields and MonoBehaviour lifecycle, which is simpler and works for a single-mod context. |

---

## Updated Feasibility Assessment

### Option B Status: In Progress (~96% Complete)

| Component | Status | Remaining |
|-----------|--------|-----------|
| Dense BotRegistry | ✅ Complete | — |
| BotEntity with embedded state | ✅ Complete | — |
| Static system methods | ✅ Complete | — |
| QuestScorer optimization | ✅ Complete | — |
| Dual-write integration layer | ✅ Complete | — |
| All reads from ECS | ✅ Complete | — |
| Push sensor writes (ECS-only) | ✅ Complete | — |
| Pull sensor writes (dense iteration) | ✅ Complete | — |
| Boss/follower lifecycle writes | ✅ Complete | — |
| BotRegistrationManager reads | ✅ Complete | — |
| BsgBotRegistry sparse lookup | ✅ Complete | — |
| TimePacing / FramePacing | ✅ Complete | Wiring incremental |
| Remove old dictionaries | ✅ Complete | Phase 5F done |
| BotFieldState on entity | ✅ Complete | — |
| Job assignment on entity | ⚠️ Field added | Wire to BotJobAssignmentFactory |
| Deterministic tick order | ✅ Complete | Phase 7C (documented) |
| Allocation cleanup | ✅ Complete | Phase 7D (6 hotspots fixed) |

### Revised Verdict

Option B is nearing completion. The ECS is the **sole data store** for all
sensor, sleep, type, and boss/follower data. All old dictionaries
(`deadBots`, `botBosses`, `botFollowers`, `sensors`) and 6 sensor subclasses
have been deleted. Boss/follower lifecycle uses dense ECS entity iteration
with O(1) `IsActive` checks. Phase 6 field state wiring is complete
(WorldGridManager dictionary removed). Phase 7C documents the deterministic
tick order. Phase 7D eliminates all remaining allocation hotspots (static
buffers, O(n) min/max scans, for-loop replacements for LINQ chains).
Phase 8 has a field on `BotEntity` but needs wiring.

**Estimated remaining effort**: Phase 8 wiring (~3 files, 1 session).

---

## Conclusion

Phobos's architecture is **ECS-inspired but pragmatic** — it uses dense entity
lists and static systems for clean iteration, but keeps components embedded on
entity objects rather than in separate contiguous arrays. This is the right
trade-off for SPT's scale.

QuestingBots should adopt the same pragmatic approach:

1. **Dense BotRegistry** replaces scattered dictionaries → cleaner iteration,
   no null-key problems
2. **BotEntity with embedded state** replaces per-sensor dictionaries → single
   source of truth per bot
3. **Static system methods** replace MonoBehaviour update loops → testable,
   deterministic, Phobos-compatible

The primary value is **code quality and maintainability**, not performance. At
20–40 bots, cache layout is irrelevant — but having a single `BotEntity` object
that holds all of a bot's state makes debugging, logging, testing, and feature
development significantly easier.

**Do not pursue pure ECS or SoA layouts** — they add complexity without
measurable benefit at SPT's scale. Phobos proves that AoS with dense iteration
is the right pattern for game AI mods.
