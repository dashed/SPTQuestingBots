# Bot Looting Analysis

Deep investigation of bot looting systems across Phobos PR #1, SPT-LootingBots, and BSG APIs.
Goal: implement native looting in QuestingBots to make raids feel more alive.

---

## 1. Motivation

QuestingBots currently makes bots move purposefully around the map doing quests —
but they never pick anything up. Real players loot constantly: checking containers,
grabbing loose items, stripping corpses. Adding opportunistic looting would make bot
behavior dramatically more realistic and support the mod's core goal of "alive raids."

**Current state**: QuestingBots has zero native looting. It only yields control to
SPT-LootingBots (an external mod) via reflection-based interop when that mod is installed.
Without LootingBots, bots never interact with loot.

---

## 2. Reference Implementations

### 2.1 Phobos PR #1 — "Added Looting"

**Source**: [SleepingPills/Phobos#1](https://github.com/SleepingPills/Phobos/pull/1)
(4669 additions, 34 files, CLOSED — never merged)

**Architecture**: MonoBehaviour composition on bot GameObject.

| Component | Role |
|---|---|
| `LootingLayer` | BigBrain CustomLayer (priority 24-25), replaces BSG "Utility peace" + "LootPatrol" |
| `LootingBrain` | Central coordinator — tracks active loot, ignore lists, looting state machine |
| `LootFinder` | Physics.OverlapSphereNonAlloc scanner — coroutine-based with yield chunking |
| `LootingInventoryController` | Inventory decision logic — equip/pickup/swap/drop |
| `LootingTransactionController` | BSG transaction execution — Move/Merge/ThrowAndEquip |
| `ItemAppraiser` | Item valuation — HandbookClass + RagfairGetPrices |
| `LootingLogic` | Movement + approach — BotOwner.GoToPoint, stuck detection |
| `LootingSystem` | Raid lifecycle — init, cleanup, active bot cache |

**Scanning**: `Physics.OverlapSphereNonAlloc` with `Collider[3000]` via `ArrayPool<Collider>`.
Sorts by distance, processes sequentially with early-exit after 3 failed range checks.
Uses `QueryTriggerInteraction.Collide` (different from LootingBots which uses `.Ignore`).

**Loot types**: Containers, loose items, corpses. Each has independent enable/disable
and detection range (default 80m). LOS check optional per type.

**Inventory operations**: `InteractionsHandlerClass.Move()` + `.Merge()` + `.Sort()`.
Transactions via `InventoryController.TryRunNetworkTransaction()` with async/await wrapping
BSG's `Callback` delegate pattern.

**Gear comparison**: Armor class comparison, container size comparison, weapon value comparison
with mod-stripping fallback. Priority: backpack → helmet → armor vest → tactical rig.

**Navigation**: `BotOwner.GoToPoint()` (BSG's NavMesh pathfinding).

**Performance**: ArrayPool for collider buffer, coroutine chunking, distance-based brain
disable, `ActiveBotCache` limits concurrent looters.

**Standalone**: NOT integrated with Phobos utility AI. Operates as an independent BigBrain
layer. Does not interact with Phobos's `UtilityTaskManager` or squad systems.

**Server component**: Disables BSG DiscardLimits via reflection on database tables.

### 2.2 SPT-LootingBots

**Source**: `/home/alberto/github/SPT-LootingBots/` (community mod by Skwizzy)

**Architecture**: Nearly identical to Phobos PR — same component names, same patterns,
same MonoBehaviour composition. Likely same author or direct port.

| Component | File | Role |
|---|---|---|
| `LootingLayer` | `LootingLayer.cs` | BigBrain CustomLayer (priority 4-5) |
| `LootingBrain` | `Components/LootingBrain.cs` | Central coordinator |
| `LootFinder` | `Components/LootFinder.cs` | OverlapSphere scanner |
| `LootingInventoryController` | `Components/LootingInventoryController.cs` | Inventory decisions |
| `LootingTransactionController` | `Components/LootingTransactionController.cs` | BSG transactions |
| `ItemAppraiser` | `Components/ItemAppraiser.cs` | Handbook + Ragfair valuation |
| `LootingLogic` | `Logic/LootingLogic.cs` | Movement + approach |
| `FindLootLogic` | `Logic/FindLootLogic.cs` | Scan trigger logic |
| `PeacefulLogic` | `Logic/PeacefulLogic.cs` | Idle state |

**Scanning**: Identical pattern — `Physics.OverlapSphereNonAlloc`, `Collider[3000]` ArrayPool,
`ColliderDistanceComparer` sort, early-exit after 3 range failures.
Uses `QueryTriggerInteraction.Ignore` (vs Phobos's `.Collide`).

**Key differences from Phobos PR**:
- Lower BigBrain priority (4-5 vs 24-25) — yields to most other layers
- 5 Harmony patches: EnableWeaponSwitching, LootDataHaveActions, LootDataSetTargetLootCluster,
  CleanCacheOnRaidEnd, RemoveLootingBrain
- More mature error handling and edge cases
- External API (`External.cs`) for mod interop (QuestingBots uses this)
- Separate action classes: `LootingSwapAction`, `LootingEquipAction`, `LootingMoveAction`

**Known limitations** (from both implementations):
- Distance-only targeting (no value priority in scan phase)
- Single loot target at a time
- Simple stuck detection (distance delta < 0.3m threshold)
- No squad coordination (each bot independent)
- No utility scoring (always loot when available)

### 2.3 Comparison Table

| Feature | Phobos PR | LootingBots | QuestingBots (current) |
|---|---|---|---|
| Scanning | OverlapSphereNonAlloc | OverlapSphereNonAlloc | None |
| Buffer size | 3000 (ArrayPool) | 3000 (ArrayPool) | N/A |
| Detection range | 80m default | 80m default | N/A |
| Loot types | Container/Item/Corpse | Container/Item/Corpse | None |
| Navigation | GoToPoint (BSG) | GoToPoint (BSG) | CustomMoverController |
| Inventory ops | InteractionsHandlerClass | InteractionsHandlerClass | ItemHelpers (partial) |
| Item valuation | Handbook + Ragfair | Handbook + Ragfair | None |
| Gear comparison | Yes (armor/weapon/container) | Yes (armor/weapon/container) | None |
| Gear swap | ThrowAndEquip | ThrowAndEquip | None |
| BigBrain layer | Yes (priority 24-25) | Yes (priority 4-5) | None |
| Utility AI integration | No | No | N/A |
| Squad coordination | No | No | WantsToLoot sensor only |
| Stuck detection | Distance delta | Distance delta | SoftStuck + HardStuck |
| Concurrent bot limit | ActiveBotCache | ActiveBotCache | N/A |
| LOD / perf gating | Distance to player | Distance to player | BotLodCalculator |
| Server component | DiscardLimits disable | DiscardLimits disable | None |

---

## 3. BSG API Surface for Looting

### 3.1 Loot Detection

```
Physics.OverlapSphereNonAlloc(position, radius, colliders, layerMask, triggerInteraction)
  LayerMask layers: "Interactive" | "Loot" | "Deadbody"

Loot object types:
  LootableContainer  — world containers (crates, safes, drawers, etc.)
    .DoorState        — None/Open/Shut/Locked
    .ItemOwner        — inventory contents
    .Interact()       — open/close container

  LootItem            — loose items on the ground
    .ItemOwner        — item data
    .RootItem         — underlying Item instance

  Player              — corpses (dead players/bots)
    .InventoryController — corpse inventory
    .Profile           — identity (for filtering)
```

### 3.2 Inventory Operations

```
InteractionsHandlerClass (static methods):
  .Move(item, address, inventoryController, simulate)     — relocate item
  .Merge(item, targetItem, inventoryController, simulate)  — stack merge
  .Sort(gridContents)                                      — optimize grid layout

InventoryController:
  .FindSlotToPickUp(item)          — find equipment slot
  .FindGridToPickUp(item)          — find grid space
  .FindItemToMerge(item)           — find stackable target
  .IsItemEquipped(item)            — ownership check
  .TryRunNetworkTransaction(result, callback)  — execute with network sync
  .ThrowItem(item, callback)       — drop to ground

SearchableItemItemClass              — searchable containers
StashGridClass                       — grid-based storage
CompoundItem.Slots                   — locked slots (armor plates, etc.)
```

### 3.3 Item Properties

```
Item:
  .TemplateId         — price lookup key
  .CalculateCellSize() — W×H grid dimensions

ArmorComponent:
  .ArmorClass         — integer armor rating

Weapon:
  .Mods               — attached modifications
  .GetMagazineSlot()  — magazine compatibility

AmmoItemClass         — ammunition
KeyComponent          — key usage tracking
```

### 3.4 Item Valuation

```
Handbook pricing:
  Singleton<HandbookClass>.Instance.Items  — static Dictionary<templateId, price>

Market pricing:
  ClientApplication.GetClientBackEndSession().RagfairGetPrices()  — dynamic prices

Weapon valuation:
  Sum(weapon.Mods.Select(mod => GetPrice(mod.TemplateId)))
```

### 3.5 Navigation

```
BotOwner.GoToPoint(position)       — BSG NavMesh pathfinding (used by Phobos/LootingBots)
NavMesh.SamplePosition()           — snap to walkable surface
BotOwner.Mover.ComputePathLengthToPoint()  — path distance check

QuestingBots alternative:
  CustomMoverController.SetPath()  — our Phobos-style mover (Player.Move based)
  CustomPathFollower               — pure-logic path following
```

---

## 4. QuestingBots Current State

### 4.1 Existing Loot Interop

QuestingBots has a well-designed **pluggable interop** system for external loot mods:

```
AbstractLootFunction (abstract)
  ├── LootingBotsLootFunction  — reflection bridge to LootingBots.External API
  └── InternalLootFunction     — no-op fallback (all methods return false)

BotLootingMonitor:
  - Tracks IsLooting, IsSearchingForLoot state
  - Time-gated ShouldCheckForLoot() state machine
  - ForcedToSearchForLoot timer

BotQuestingDecisionMonitor:
  - CheckForLoot decision → BotObjectiveLayer goes inactive
  - Lets LootingBots' BigBrain layer take over
  - setLootingHiveMindState() → WantsToLoot sensor

ExternalModHandler:
  - Detects LootingBots via BepInEx Chainloader
  - Creates appropriate loot function per bot

LootingBotsInterop:
  - Caches MethodInfo via reflection
  - ForceBotToScanLoot() / PreventBotFromLooting()
```

### 4.2 Existing Inventory Helpers

`ItemHelpers.cs` already has several inventory operations:

- `GetInventoryController()` — reflection access to `Player._inventoryController`
- `TryTransferItem()` — `InteractionsHandlerClass.Move()` + `TryRunNetworkTransaction()`
- `FindLocationForItem()` — scans backpack/vest/pockets for space
- `CreateFakeStash()` / `TryAddToFakeStash()` — `StashGridClass` wrappers
- `GetEquippedWeapons()` — reads equipment slots

### 4.3 Zone Movement POI Discovery

`PoiScanner` already discovers containers via `LootableContainer` but only uses them
as navigation waypoints for zone-based movement — no loot state tracking.

### 4.4 ECS Infrastructure

The ECS already has relevant fields and systems:

- `BotEntity.WantsToLoot` — sensor bool (currently only for LootingBots yield)
- `BotEntity.LastLootingTime` — timestamp tracking
- `BotEntityBridge.UpdateLastLootingTime()` — bridges to ECS
- Utility AI `UtilityTask` framework — ready for a `LootTask`
- Squad coordination — `SquadEntity`, tactical positions, comm range gating

### 4.5 What's Missing for Native Looting

| Gap | Description |
|---|---|
| Loot scanning | No OverlapSphere or equivalent loot detection |
| Loot target selection | No prioritization logic (value, distance, type) |
| Item valuation | No price lookup or gear comparison |
| Inventory planning | No "should I pick this up?" decision logic |
| Gear swap | No "is this better than what I have?" comparison |
| Approach controller | No movement-to-loot integration with CustomMoverController |
| Container interaction | No LootableContainer.Interact() calls |
| Corpse looting | No corpse inventory access |
| Transaction management | Partial — ItemHelpers has Move but lacks Merge/Sort/ThrowAndEquip |
| Loot claim system | No multi-bot deconfliction (ActiveLootCache equivalent) |
| Utility scoring | No "loot vs quest" trade-off evaluation |
| Server-side config | No DiscardLimits modification |

---

## 5. Architecture Options

### Option A: Standalone BigBrain Layer

**Approach**: Port LootingBots architecture directly as a new BigBrain layer in QuestingBots.

**Pros**:
- Proven pattern — both Phobos PR and LootingBots use this successfully
- Clean separation — looting layer activates independently of questing
- Simple to implement — well-understood MonoBehaviour composition

**Cons**:
- No integration with utility AI — can't balance loot vs quest priority
- No squad coordination — each bot loots independently
- Duplicates existing patterns (our mover, our stuck detection, our LOD)
- Would conflict with LootingBots if both installed

### Option B: Full Utility AI Integration

**Approach**: Looting as just another UtilityTask scored alongside quest actions.

**Pros**:
- Seamless priority balancing — utility scores determine when to loot
- Reuses existing infrastructure (mover, stuck detection, LOD, squad)
- Natural squad coordination via SquadEntity

**Cons**:
- Complex — looting lifecycle (scan → approach → interact → transfer) doesn't map
  cleanly to a single utility task
- Scoring loot value requires scan results before scoring can happen
- Risk of over-engineering the scoring function

### Option C: Hybrid — Utility Scoring + Dedicated Controller (Recommended)

**Approach**: Utility AI decides **when** to loot (scoring opportunity vs quest urgency).
Dedicated loot controller handles **how** to loot (scan, approach, interact, transfer).

```
Utility AI layer:
  LootTask.Score(entity) →
    - Is loot nearby? (cached scan results)
    - How valuable? (estimated value)
    - How far from quest objective?
    - Is bot in combat?
    - Has inventory space?
    → float score (compared against GoToObjective, Ambush, etc.)

Loot controller (when LootTask wins):
  LootScanner      → Physics.OverlapSphereNonAlloc
  LootTargetPicker → distance + value + type priority
  LootApproach     → CustomMoverController path following
  LootInteraction  → container open, item pickup, corpse search
  LootInventory    → equip/pickup/swap/drop decisions
```

**Pros**:
- Best of both worlds — smart scheduling + dedicated execution
- Reuses all existing infrastructure
- Natural squad coordination (boss loot priority, follower spread)
- Pure-logic scoring and planning → fully testable
- Graceful LootingBots compatibility (disable native when external detected)

**Cons**:
- More design work upfront
- Two-phase architecture (scan cache + scoring) adds complexity

---

## 6. Recommended Architecture (Option C)

### 6.1 Pure-Logic Classes (testable, no Unity deps)

#### LootScorer
Scores a potential loot target against current bot state.

```
Inputs: targetType, estimatedValue, distanceSqr, inventorySpaceFree, isInCombat,
        distanceToObjectiveSqr, timeSinceLastLoot
Output: float score (0.0 – 1.0)

Factors:
  - Value weight: higher value → higher score
  - Distance penalty: farther → lower score
  - Inventory penalty: less space → lower score (0 if full)
  - Combat penalty: in combat → 0
  - Cooldown: recently looted → reduced score
  - Quest proximity bonus: near objective → higher (opportunistic)
```

#### LootInventoryPlanner
Decides what to do with a specific item.

```
Actions: Equip, Pickup, Swap, Skip
Logic:
  - Is armor better? (ArmorClass comparison)
  - Is weapon more valuable? (mod-sum comparison)
  - Is container bigger? (grid size comparison)
  - Does it fit in inventory? (grid space check)
  - Should we drop something? (value comparison)
```

#### ItemValueEstimator
Price lookup using HandbookClass data.

```
GetValue(templateId) → int price
GetWeaponValue(weapon) → sum of mod values
ComparativeValue(myGear, candidateGear) → float ratio
```

#### LootTargetSelector
Picks best target from scan results.

```
Inputs: scanResults[], botPosition, botInventoryState
Output: LootTarget? (id, position, type, estimatedValue)

Strategy: Score each candidate → pick highest → claim in LootClaimRegistry
```

#### LootClaimRegistry
Prevents multiple bots from targeting the same loot.

```
TryClaim(botId, lootId) → bool
Release(botId)
IsClaimedByOther(botId, lootId) → bool
```

#### LootUtilityTask
Extends `QuestUtilityTask` for utility AI integration.

```
Score(entity):
  - Check entity.HasLootTarget
  - Check entity.LootTargetValue
  - Apply LootScorer factors
  - Return weighted score with hysteresis (h=0.15)
```

### 6.2 Unity Integration Classes

#### LootScanner
Wraps `Physics.OverlapSphereNonAlloc` with our performance patterns.

```
ScanForLoot(position, radius) → LootScanResult[]
  - ArrayPool<Collider> rental (matching Phobos/LootingBots pattern)
  - LayerMask: Interactive | Loot | Deadbody
  - Sort by distance
  - Filter: ignore list, claimed, locked containers
  - Coroutine chunking via yield
  - Cache results for utility scoring
```

#### LootApproachController
Bridges CustomMoverController for loot approach.

```
StartApproach(lootTarget)
  - Calculate destination (NavMesh snap, offset from loot)
  - Set path via CustomMoverController
  - Sprint gating: walk within 6m

UpdateApproach() → ApproachResult
  - Check distance (< 0.85m + Y < 0.5m → arrived)
  - Stuck detection via existing SoftStuck/HardStuck
  - Timeout handling
```

#### LootInteractionController
Handles BSG interaction APIs.

```
OpenContainer(container) → InteractionsHandlerClass
PickupItem(item) → Move to inventory
SearchCorpse(player) → Access InventoryController
ExecuteTransaction(result) → TryRunNetworkTransaction
```

### 6.3 ECS Integration

#### BotEntity — New Fields

```csharp
// Loot state
public bool HasLootTarget;
public int LootTargetId;
public float LootTargetX, LootTargetY, LootTargetZ;
public byte LootTargetType;       // 0=None, 1=Container, 2=Item, 3=Corpse
public float LootTargetValue;
public float InventorySpaceFree;  // cached grid slots available
public bool IsLooting;            // currently in loot interaction
public bool IsApproachingLoot;    // moving toward loot target
```

#### BotEntityBridge — New Methods

```csharp
SetLootTarget(botOwner, targetId, position, type, value)
ClearLootTarget(botOwner)
SetLootingState(botOwner, isLooting)
SyncInventorySpace(botOwner)     // cache available grid slots
GetLootTarget(botOwner) → (id, pos, type, value)?
```

#### HiveMind Tick Integration

New step in `BotHiveMindMonitor.Update()`:

```
Step 8: updateLootScanning()
  - Rate-limited (every 5s per bot, staggered)
  - For each active entity with CanQuest:
    - Run LootScanner.ScanForLoot
    - Run LootTargetSelector.Pick
    - Update BotEntity loot fields
    - LootClaimRegistry bookkeeping
```

### 6.4 Squad Coordination

#### Boss Priority
- Boss gets first pick on high-value loot within communication range
- Followers defer to boss if targeting same loot

#### Follower Looting
- Followers can loot independently when:
  - Boss is looting (idle time)
  - Boss is in HoldPosition/Ambush (waiting)
  - Bot is near tactical position (already arrived)
- Followers skip looting when:
  - Boss is moving to objective (keep formation)
  - Squad is in combat
  - Out of communication range

#### Shared Scan Results
- Boss scan results shared with followers within comm range
- Reduces redundant OverlapSphere calls
- SquadEntity tracks shared loot targets

---

## 7. Implementation Phases

### Phase 1: Core Scanning + Loose Item Pickup

**Scope**: Bots detect and pick up loose items on the ground.

**New files**:
- `BotLogic/ECS/Systems/LootScanner.cs` — OverlapSphereNonAlloc wrapper
- `BotLogic/ECS/Systems/LootClaimRegistry.cs` — multi-bot deconfliction
- `BotLogic/ECS/Systems/LootTargetSelector.cs` — pure-logic target picking
- `BotLogic/ECS/Systems/ItemValueEstimator.cs` — handbook price lookup
- `Helpers/LootInteractionHelper.cs` — BSG pickup API wrapper

**Modified files**:
- `BotLogic/ECS/BotEntity.cs` — add loot state fields
- `BotLogic/ECS/BotEntityBridge.cs` — add loot methods
- `BotLogic/HiveMind/BotHiveMindMonitor.cs` — add scan step
- `BotLogic/Objective/BotObjectiveLayer.cs` — handle loot action
- `Models/Pathing/GoToPositionAbstractAction.cs` — loot approach variant

**Estimated tests**: ~40 (scanner mock + selector + claim registry + estimator)

### Phase 2: Container + Corpse Looting

**Scope**: Open containers and search corpses.

**New files**:
- `BotLogic/ECS/Systems/LootInventoryPlanner.cs` — what-to-take decisions
- `Helpers/ContainerInteractionHelper.cs` — LootableContainer.Interact
- `Helpers/CorpseSearchHelper.cs` — corpse inventory access

**Modified files**:
- `LootScanner.cs` — add container/corpse detection + filtering
- `LootTargetSelector.cs` — add container/corpse scoring
- `LootInteractionHelper.cs` — add container open, corpse search

**Key BSG APIs**: `LootableContainer.Interact()`, `LootableContainer.DoorState`,
`SearchableItemItemClass`, `Player.InventoryController`

**Estimated tests**: ~30 (inventory planner + container/corpse interaction)

### Phase 3: Item Valuation + Gear Swap

**Scope**: Compare found gear to equipped gear. Swap when upgrade found.

**New files**:
- `BotLogic/ECS/Systems/GearComparer.cs` — armor/weapon/container comparison
- `Helpers/GearSwapHelper.cs` — ThrowAndEquip transactions

**Modified files**:
- `LootInventoryPlanner.cs` — add swap decisions
- `ItemValueEstimator.cs` — add weapon mod summation
- `ItemHelpers.cs` — extend with Merge/Sort operations

**Key logic**:
- Armor: `ArmorComponent.ArmorClass` comparison
- Weapons: mod-sum value comparison with cascading (primary → secondary → holster)
- Containers: grid size comparison (larger backpack = upgrade)
- Tactical rig: special case (armored rig vs separate armor + rig)

**Estimated tests**: ~35 (gear comparer + swap logic)

### Phase 4: Squad Coordination

**Scope**: Boss priority, follower looting windows, shared scan results.

**Modified files**:
- `LootClaimRegistry.cs` — add boss priority claims
- `LootTargetSelector.cs` — add squad-aware filtering
- `BotHiveMindMonitor.cs` — shared scan results, follower loot windows
- `SquadEntity.cs` — shared loot target tracking
- `GotoObjectiveStrategy.cs` — loot window during hold phases

**Key logic**:
- Boss claims first within comm range
- Followers loot during boss idle/hold/ambush phases
- Scan result sharing reduces OverlapSphere calls
- Combat suppresses all looting

**Estimated tests**: ~25 (squad loot coordination)

### Phase 5: Utility AI Scoring

**Scope**: Looting scored alongside quest actions in utility AI.

**New files**:
- `BotLogic/ECS/UtilityAI/QuestTasks/LootTask.cs` — utility task for looting

**Modified files**:
- `QuestTaskFactory.cs` — register LootTask
- `BotEntityBridge.cs` — sync loot state for scoring
- `LootScorer.cs` → pure-logic scoring extracted for utility task

**Key integration**:
- `LootTask.Score(entity)` uses cached scan results
- Hysteresis (h=0.15) prevents flip-flopping between loot and quest
- Score drops to 0 during combat, when inventory full, or cooldown active
- High-value loot can override quest progress (configurable threshold)

**Estimated tests**: ~20 (loot utility task scoring)

### Phase 6: Server-Side Config + Polish

**Scope**: Server component for DiscardLimits, config options, debug visualization.

**New files**:
- Server route for DiscardLimits modification
- `Configuration/LootingConfig.cs` — comprehensive config

**Config options**:
```json
{
  "looting": {
    "enabled": true,
    "detect_container_distance": 60,
    "detect_item_distance": 40,
    "detect_corpse_distance": 50,
    "scan_interval_seconds": 5.0,
    "min_item_value": 5000,
    "max_concurrent_looters": 5,
    "loot_during_combat": false,
    "container_looting_enabled": true,
    "loose_item_looting_enabled": true,
    "corpse_looting_enabled": true,
    "gear_swap_enabled": true,
    "squad_loot_coordination": true,
    "disable_when_lootingbots_detected": true
  }
}
```

**Estimated tests**: ~15 (config deserialization + server route)

---

## 8. LootingBots Compatibility

### Coexistence Strategy

QuestingBots already has the interop infrastructure:

1. **LootingBots detected** → native looting disabled, existing interop continues
   - `ExternalModHandler.CheckForExternalMods()` detects LootingBots
   - `LootingBotsLootFunction` bridges via reflection
   - `BotQuestingDecisionMonitor.CheckForLoot` yields control

2. **LootingBots not detected** → native looting activates
   - `InternalLootFunction` replaced with native loot controller
   - Utility AI scores looting alongside quest actions
   - No BigBrain layer conflict

3. **Config override** → `disable_when_lootingbots_detected: false` allows both
   (not recommended — potential conflicts)

### Migration Path

Users currently running LootingBots + QuestingBots can:
1. Keep both mods — QuestingBots auto-disables native looting
2. Remove LootingBots — QuestingBots native looting activates automatically
3. Use config to tune native looting behavior

---

## 9. Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Inventory corruption | High | Mirror LootingBots' `TryRunNetworkTransaction` pattern exactly |
| Performance regression | Medium | ArrayPool, scan rate limiting, LOD gating, concurrent bot limit |
| Loot duplication (multi-bot) | Medium | LootClaimRegistry with atomic claims |
| NavMesh stuck at loot | Medium | Reuse existing SoftStuck/HardStuck + ignore-list pattern |
| Conflict with LootingBots | Medium | Auto-detect and disable native looting |
| Item loss on swap | Medium | Validate drop location before ThrowAndEquip |
| BSG API changes | Low | Isolate BSG calls in helper classes (same pattern as existing) |
| Score oscillation | Low | Hysteresis on LootTask (h=0.15) prevents flip-flopping |

---

## 10. Key Takeaways

1. **Phobos PR and LootingBots are the same architecture** — MonoBehaviour composition,
   OverlapSphereNonAlloc scanning, InteractionsHandlerClass transactions. Likely same author.

2. **Neither integrates with utility AI** — both are standalone BigBrain layers.
   QuestingBots can improve on this by scoring looting alongside quest actions.

3. **QuestingBots already has most infrastructure** — ECS, utility AI, squad coordination,
   custom mover, stuck detection, LOD. Only the loot-specific logic is missing.

4. **The BSG API surface is well-mapped** — scanning (OverlapSphere), inventory
   (InteractionsHandlerClass), valuation (HandbookClass). Two proven implementations
   provide exact API usage patterns.

5. **Hybrid approach (Option C) is optimal** — utility AI for scheduling, dedicated
   controller for execution. Maximizes reuse of existing QuestingBots architecture.

6. **6-phase rollout minimizes risk** — loose items first (simplest), then containers,
   then gear swap, then squad, then utility scoring, then polish. Each phase is
   independently useful and testable.

7. **LootingBots compatibility is already solved** — existing interop infrastructure
   auto-detects and yields. Native looting just needs the same gate.
