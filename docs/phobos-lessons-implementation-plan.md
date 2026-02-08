# Phobos Lessons: Implementation Plan for QuestingBots

## 1. Executive Summary

After analyzing Phobos's architecture alongside QuestingBots, we identified six areas of potential improvement. Of these, two deliver high impact at low risk and should be done first:

1. **Fix allocation anti-patterns** in hot loops (`ToArray()`, LINQ chains, `List.Contains`). These are straightforward, zero-risk changes that reduce GC pressure every tick.
2. **Port two-tier stuck detection** (soft + hard) with `PositionHistory` and `RollingAverage` ring buffers, including teleport-with-safety. This directly improves bot behavior quality.

A third change -- **batched pathfinding** -- is worth pursuing if profiling confirms `NavMesh.CalculatePath` is a bottleneck with 20+ bots.

The remaining three (custom movement system, BSG mover handoff, utility AI) are **not recommended** unless specific problems emerge that justify their cost and risk.

---

## 2. Current State

| Area | QuestingBots Approach | Key Limitation |
|---|---|---|
| **Movement** | Delegates entirely to BSG's `BotMover` via `BotOwner.FollowPath()` / `BotOwner.Mover.GoToByWay()` | No direct `Player.Move()` control; dependent on BSG mover quality |
| **Pathfinding** | Synchronous `NavMesh.CalculatePath()` in `StaticPathData.CreatePathSegment()`, throttled by 100ms `canUpdate()` timer | All paths computed in a single frame; potential frame spikes with many bots |
| **Stuck Detection** | Single-tier: Stopwatch + 2m distance threshold. Jump at 6s, vault at 8s, change objective at 20s | No speed tracking, no ring buffer history, no teleport capability |
| **BSG Mover Handoff** | Not needed (uses BSG mover throughout) | N/A |
| **Data Layout / Allocations** | Dictionaries with `Keys.ToArray()` in hot loops, `List<BotOwner>.Contains()` for O(n) lookups, heavy LINQ chains | GC allocations every 50ms tick across 5 sensors + boss/follower updates |
| **Decision Architecture** | Priority-based if/else chain in `getSoloDecision()` (15+ conditions), enum switch in `GetNextAction()` | Adding new actions requires editing 3-5 files, but debuggable and working |

---

## 3. Phase 1: Quick Wins (Low Effort, High Impact)

### 3.1 Fix Allocation Anti-Patterns

**Estimated Complexity: S (Small)**

These changes eliminate per-frame/per-tick array allocations with no behavioral change. Every fix is a drop-in replacement.

#### 3.1.1 BotHiveMindAbstractSensor -- Eliminate `botState.Keys.ToArray()`

**File:** `src/SPTQuestingBots.Client/BotLogic/HiveMind/BotHiveMindAbstractSensor.cs:47`

Current code (line 47, called by all 5 sensors every 50ms tick):
```csharp
foreach (BotOwner bot in botState.Keys.ToArray())
```

The `ToArray()` exists because the dictionary may be mutated during iteration (dead bots get their value set). However, the `Update()` method only *sets values* (`botState[bot] = defaultValue`), it never adds or removes keys. Setting a value on an existing key does not invalidate a `Dictionary<K,V>` enumerator in .NET. The fix is safe:

```csharp
foreach (BotOwner bot in botState.Keys)
```

**Impact:** Eliminates 5 array allocations per tick (one per sensor type).

If there is concern about future mutations, an alternative is to maintain a parallel `List<BotOwner> botList` that is updated on `RegisterBot()`, and iterate over that.

#### 3.1.2 BotHiveMindAbstractSensor -- Eliminate `botFollowers[bot].ToArray()`

**File:** `src/SPTQuestingBots.Client/BotLogic/HiveMind/BotHiveMindAbstractSensor.cs:127`

```csharp
foreach (BotOwner follower in BotHiveMindMonitor.botFollowers[bot].ToArray())
```

This iterates a `List<BotOwner>` but never modifies it. Direct iteration is safe:

```csharp
foreach (BotOwner follower in BotHiveMindMonitor.botFollowers[bot])
```

#### 3.1.3 BotHiveMindMonitor.updateBosses() -- Eliminate `botBosses.Keys.ToArray()`

**File:** `src/SPTQuestingBots.Client/BotLogic/HiveMind/BotHiveMindMonitor.cs:349`

```csharp
foreach (BotOwner bot in botBosses.Keys.ToArray())
```

This one is trickier: `botBosses[bot]` is mutated (set to null) but no keys are added/removed during iteration. Setting existing keys is safe:

```csharp
foreach (BotOwner bot in botBosses.Keys)
```

**Caveat:** `addBossFollower()` at line 385 calls `botFollowers.Add(boss, ...)` which modifies a *different* dictionary. This is safe.

#### 3.1.4 BotHiveMindMonitor.updateBossFollowers() -- Eliminate double `ToArray()`

**File:** `src/SPTQuestingBots.Client/BotLogic/HiveMind/BotHiveMindMonitor.cs:418,436`

```csharp
foreach (BotOwner boss in botFollowers.Keys.ToArray())       // line 418 -- allocates
{
    ...
    foreach (BotOwner follower in botFollowers[boss].ToArray())  // line 436 -- allocates
```

Line 418: `botFollowers.Remove(boss)` is called at line 430 during iteration, which *does* invalidate the enumerator. This `ToArray()` is needed OR must be restructured.

**Recommended fix:** Collect removals in a reusable list and apply after the loop:

```csharp
// Add a static reusable list at class level:
private static readonly List<BotOwner> _deadBossBuffer = new List<BotOwner>();

private void updateBossFollowers()
{
    _deadBossBuffer.Clear();

    foreach (BotOwner boss in botFollowers.Keys)
    {
        if ((boss == null) || boss.IsDead)
        {
            if (!deadBots.Contains(boss))
            {
                Controllers.LoggingController.LogDebug("Boss " + boss.GetText() + " is now dead.");
                deadBots.Add(boss);
            }
            _deadBossBuffer.Add(boss);
            continue;
        }

        // Inner loop: Remove is on the List, not during List iteration.
        // Use reverse iteration to allow removal:
        for (int i = botFollowers[boss].Count - 1; i >= 0; i--)
        {
            BotOwner follower = botFollowers[boss][i];
            // ... existing dead-check logic, use RemoveAt(i) instead of Remove(follower)
        }
    }

    for (int i = 0; i < _deadBossBuffer.Count; i++)
    {
        botFollowers.Remove(_deadBossBuffer[i]);
    }
}
```

Line 436: The inner `ToArray()` exists because `botFollowers[boss].Remove(follower)` is called at line 449 during iteration of the same list. Use reverse-index iteration instead (shown above).

**Impact:** Eliminates 2 allocations per tick for the monitor itself.

#### 3.1.5 BotHiveMindMonitor.SeparateBotFromGroup() -- Eliminate `botBosses.Keys.ToArray()` and `botFollowers.Keys.ToArray()`

**File:** `src/SPTQuestingBots.Client/BotLogic/HiveMind/BotHiveMindMonitor.cs:251,265`

```csharp
foreach (BotOwner follower in botBosses.Keys.ToArray())     // line 251
...
foreach (BotOwner boss in botFollowers.Keys.ToArray())       // line 265
```

Line 251: Only mutates values (`botBosses[follower] = null`, `botBosses[bot] = null`), never adds/removes keys. Safe to remove `ToArray()`.

Line 265: Calls `botFollowers[boss].Clear()` and `botFollowers[boss].Remove(bot)` which modify *values* (the lists), not *keys*. Safe to remove `ToArray()`.

#### 3.1.6 BotHiveMindMonitor.GetAllGroupMembers() -- Eliminate LINQ chain allocation

**File:** `src/SPTQuestingBots.Client/BotLogic/HiveMind/BotHiveMindMonitor.cs:177`

```csharp
BotOwner[] allGroupMembers = GetFollowers(boss).AddItem(boss).Where(b => b.Id != bot.Id).ToArray();
```

Replace with pre-allocated list:

```csharp
public static ReadOnlyCollection<BotOwner> GetAllGroupMembers(BotOwner bot)
{
    BotOwner boss = GetBoss(bot) ?? bot;
    ReadOnlyCollection<BotOwner> followers = GetFollowers(boss);

    var result = new List<BotOwner>(followers.Count + 1);
    for (int i = 0; i < followers.Count; i++)
    {
        if (followers[i].Id != bot.Id)
            result.Add(followers[i]);
    }
    if (boss.Id != bot.Id)
        result.Add(boss);

    return new ReadOnlyCollection<BotOwner>(result);
}
```

#### 3.1.7 BotHiveMindMonitor.GetLocationOfNearestGroupMember() -- Eliminate Dictionary + OrderBy

**File:** `src/SPTQuestingBots.Client/BotLogic/HiveMind/BotHiveMindMonitor.cs:229-235`

```csharp
Dictionary<BotOwner, float> distanceToMember = new Dictionary<BotOwner, float>();
foreach (BotOwner member in members) { ... }
BotOwner nearestMember = distanceToMember.OrderBy(x => x.Value).First().Key;
```

Replace with simple min-tracking:

```csharp
BotOwner nearestMember = null;
float nearestDistance = float.MaxValue;
foreach (BotOwner member in members)
{
    float dist = Vector3.Distance(bot.Position, member.Position);
    if (dist < nearestDistance)
    {
        nearestDistance = dist;
        nearestMember = member;
    }
}
return nearestMember.Position;
```

#### 3.1.8 BotRegistrationManager -- Replace `List.Contains()` with `HashSet`

**File:** `src/SPTQuestingBots.Client/Controllers/BotRegistrationManager.cs:33-34,49-50`

```csharp
private static List<BotOwner> registeredPMCs = new List<BotOwner>();
private static List<BotOwner> registeredBosses = new List<BotOwner>();
```

`Contains()` on these lists is O(n). With 15-30 bots and frequent checks (`IsBotAPMC`, `IsBotABoss` called in hot paths), replace with `HashSet<BotOwner>`:

```csharp
private static HashSet<BotOwner> registeredPMCs = new HashSet<BotOwner>();
private static HashSet<BotOwner> registeredBosses = new HashSet<BotOwner>();
```

The public `PMCs` and `Bosses` properties (line 46-47) expose `IReadOnlyList<BotOwner>`. These can be changed to return the HashSet cast to `IReadOnlyCollection<BotOwner>`, or a separate list can be maintained alongside the HashSet for ordered access if needed.

#### 3.1.9 BotJobAssignmentFactory.NumberOfActiveBots() -- Eliminate nested LINQ

**File:** `src/SPTQuestingBots.Client/Controllers/BotJobAssignmentFactory.cs:219-229`

```csharp
num += botJobAssignments[id]
    .Where(a => a.StartTime.HasValue)
    .Where(a => (a.Status == JobAssignmentStatus.Active) || ...)
    .Where(a => a.QuestAssignment == quest)
    .Count();
```

Replace with a simple counting loop:

```csharp
var assignments = botJobAssignments[id];
for (int i = 0; i < assignments.Count; i++)
{
    var a = assignments[i];
    if (a.StartTime.HasValue
        && ((a.Status == JobAssignmentStatus.Active)
            || ((a.Status == JobAssignmentStatus.Pending) && (a.TimeSinceStarted().Value < pendingTimeLimit)))
        && a.QuestAssignment == quest)
    {
        num++;
    }
}
```

#### 3.1.10 BotJobAssignmentFactory.GetAllPossibleQuests() -- Eliminate heavy LINQ chain

**File:** `src/SPTQuestingBots.Client/Controllers/BotJobAssignmentFactory.cs:653-659`

```csharp
return allQuests
    .Where(q => q.Desirability != 0)
    .Where(q => q.NumberOfValidObjectives > 0)
    .Where(q => q.MaxBotsInGroup >= botGroupSize)
    .Where(q => q.CanMoreBotsDoQuest())
    .Where(q => q.CanAssignToBot(bot))
    .ToArray();
```

This is called from `GetRandomQuest()` which is itself in a loop. Replace with a reusable list:

```csharp
// At class level:
private static readonly List<Quest> _possibleQuestsBuffer = new List<Quest>();

public static IReadOnlyList<Quest> GetAllPossibleQuests(this BotOwner bot)
{
    int botGroupSize = BotLogic.HiveMind.BotHiveMindMonitor.GetFollowers(bot).Count + 1;
    _possibleQuestsBuffer.Clear();

    for (int i = 0; i < allQuests.Count; i++)
    {
        var q = allQuests[i];
        if (q.Desirability != 0
            && q.NumberOfValidObjectives > 0
            && q.MaxBotsInGroup >= botGroupSize
            && q.CanMoreBotsDoQuest()
            && q.CanAssignToBot(bot))
        {
            _possibleQuestsBuffer.Add(q);
        }
    }

    return _possibleQuestsBuffer;
}
```

Note: callers that currently call `.ToArray()` on the result should be updated accordingly.

### 3.2 Improve Stuck Detection Thresholds

**Estimated Complexity: S (Small)**

Update the default config values to be more aggressive, matching Phobos's findings:

**File:** `src/SPTQuestingBots.Client/Configuration/StuckBotRemediesConfig.cs`

| Config Key | Current | Proposed | Rationale |
|---|---|---|---|
| `MinTimeBeforeJumping` | 6s | 3s | Phobos jumps at 3s (1.5s vault + 1.5s jump) |
| `MinTimeBeforeVaulting` | 8s | 1.5s | Phobos vaults first at 1.5s -- vaulting is safer than jumping |
| `JumpDebounceTime` | 4s | 2s | Faster retry cycle |
| `VaultDebounceTime` | 4s | 2s | Faster retry cycle |

Also swap the order: try vault *before* jump (Phobos does vault first because it is less disruptive).

**File:** `src/SPTQuestingBots.Client/BehaviorExtensions/GoToPositionAbstractAction.cs:282-327`

Swap the vault and jump blocks in `tryToGetUnstuck()` so vaulting is attempted first.

---

## 4. Phase 2: Medium Effort (Moderate Refactoring)

### 4.1 Port Two-Tier Stuck Detection

**Estimated Complexity: M (Medium)**

Port Phobos's two-tier stuck detection system with speed-aware thresholds, ring buffer history, and safe teleportation.

#### What to Build

1. **`PositionHistory` class** -- Ring buffer tracking bot positions over N samples.
   - Reference: `/home/alberto/github/Phobos/Phobos/Helpers/PositionHistory.cs` (61 lines)
   - Port directly; the implementation is self-contained with no Phobos dependencies.

2. **`RollingAverage` class** -- Circular buffer for speed averaging with drift correction.
   - Reference: `/home/alberto/github/Phobos/Phobos/Helpers/RollingAverage.cs` (65 lines)
   - Port directly; also self-contained.

3. **`SoftStuckDetector`** -- Frame-to-frame stuck detection with asymmetric EWMA speed tracking.
   - Tracks frame-to-frame movement vs. expected distance based on move speed.
   - Ignores Y-axis to filter out jumps.
   - Progressive remediation: vault at 1.5s, jump at 3s, fail at 6s.
   - Reference: `MovementSystem.SoftStuckRemediation` in `/home/alberto/github/Phobos/Phobos/Systems/MovementSystem.cs:397-479`

4. **`HardStuckDetector`** -- Long-term stuck detection using position ring buffer.
   - Uses `PositionHistory` (50 samples) and `RollingAverage` (50 samples).
   - Progressive remediation: path retry at 5s, teleport at 10s (with safety checks), give up at 15s.
   - Reference: `MovementSystem.HardStuckRemediation` in `/home/alberto/github/Phobos/Phobos/Systems/MovementSystem.cs:483-633`

5. **Teleport safety checks** -- Only teleport when:
   - No human player within 10m (squared distance check).
   - No line-of-sight from human player's head to any of 6 bot body parts.
   - Uses `Physics.Linecast` with appropriate layer mask.

#### Integration Points

Replace the current stuck detection in `GoToPositionAbstractAction`:

**Current system** (`GoToPositionAbstractAction.cs:266-340`):
- Single `Stopwatch botIsStuckTimer` + `lastBotPosition` with 2m threshold.
- `updateBotStuckDetection()` resets timer when bot moves 2m.
- `tryToGetUnstuck()` tries jump at 6s, vault at 8s.
- `checkIfBotIsStuck()` returns true at configurable time (default 20s).

**New system:**
- Add `SoftStuckDetector` and `HardStuckDetector` as fields on `GoToPositionAbstractAction`.
- `SoftStuckDetector.Update()` runs every frame/tick, replaces `updateBotStuckDetection()` + `tryToGetUnstuck()`.
- `HardStuckDetector.Update()` runs every frame/tick, provides teleport + path retry.
- `checkIfBotIsStuck()` returns true when either detector reaches `Failed` status.
- Reset both detectors on `Start()` and when the bot successfully moves.

#### Files to Create

| File | Description |
|---|---|
| `src/SPTQuestingBots.Client/Helpers/PositionHistory.cs` | Ring buffer for position tracking |
| `src/SPTQuestingBots.Client/Helpers/RollingAverage.cs` | Circular buffer for speed averaging |
| `src/SPTQuestingBots.Client/Models/SoftStuckDetector.cs` | Frame-to-frame stuck detection |
| `src/SPTQuestingBots.Client/Models/HardStuckDetector.cs` | Long-term stuck detection with teleport |

#### Files to Modify

| File | Change |
|---|---|
| `src/SPTQuestingBots.Client/BehaviorExtensions/GoToPositionAbstractAction.cs` | Replace single-tier with two-tier detection |
| `src/SPTQuestingBots.Client/Configuration/StuckBotDetectionConfig.cs` | Add config for teleport distance, LOS check, position history size |
| `src/SPTQuestingBots.Client/Configuration/StuckBotRemediesConfig.cs` | Add soft/hard tier config values |

#### Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Teleportation visible to player | LOS + proximity checks (ported from Phobos) |
| False positive stuck detection with slow bots | Speed-aware threshold (asymmetric EWMA) prevents this |
| Ring buffer memory | 50 `Vector3` samples per bot = 600 bytes -- negligible |

### 4.2 Implement Batched Pathfinding

**Estimated Complexity: M (Medium)**

Port Phobos's `NavJobExecutor` to spread `NavMesh.CalculatePath` calls across frames.

#### What to Build

1. **`NavJobExecutor` class** -- Queue-based pathfinding dispatcher.
   - Reference: `/home/alberto/github/Phobos/Phobos/Navigation/NavJobExecutor.cs` (49 lines)
   - `Submit(origin, target)` returns a `NavJob` with `IsReady` flag.
   - `Update()` processes `min(ceil(queueSize/2), batchSize)` jobs per frame.
   - Port directly; the implementation is self-contained.

2. **`NavJob` class** -- Simple data holder for pathfinding requests.
   - `Origin`, `Target`, `Status`, `Path`, `IsReady`.

#### Integration Points

The main integration point is `BotPathData.updateCorners()` (line 164 in `BotPathData.cs`), which currently calls `CreatePathSegment()` synchronously:

```csharp
private void updateCorners(Vector3 target, bool ignoreDuplicates = false)
{
    StartPosition = bot.Position;
    Status = CreatePathSegment(bot.Position, target, out Vector3[] corners);
    // ...
}
```

This needs to become asynchronous:
1. `updateCorners()` submits a `NavJob` instead of calling `CreatePathSegment()` directly.
2. The path status is set to a new `Pending` state.
3. On the next `CheckIfUpdateIsNeeded()` call, check if the job is ready and apply the result.

#### Alternative: Simpler Integration

A simpler approach that avoids async complexity: keep the current synchronous path in `BotPathData` but add a frame-level throttle in the executor that limits total `NavMesh.CalculatePath` calls per frame across all bots. This preserves the current call pattern while preventing frame spikes.

```csharp
public static class PathfindingThrottle
{
    private static int _callsThisFrame = 0;
    private const int MaxCallsPerFrame = 5;

    public static void OnFrameStart() => _callsThisFrame = 0;

    public static bool CanCalculatePath()
    {
        if (_callsThisFrame >= MaxCallsPerFrame) return false;
        _callsThisFrame++;
        return true;
    }
}
```

Then guard `CreatePathSegment()`:
```csharp
if (!PathfindingThrottle.CanCalculatePath())
{
    // Defer to next frame
    return;
}
```

This is recommended as the first step. The full async `NavJobExecutor` can be added later if needed.

#### Files to Create

| File | Description |
|---|---|
| `src/SPTQuestingBots.Client/Models/Pathing/NavJob.cs` | Pathfinding job data class |
| `src/SPTQuestingBots.Client/Models/Pathing/NavJobExecutor.cs` | Batched pathfinding executor |

#### Files to Modify

| File | Change |
|---|---|
| `src/SPTQuestingBots.Client/Models/Pathing/StaticPathData.cs` | Integrate throttle or async jobs into `CreatePathSegment()` |
| `src/SPTQuestingBots.Client/Models/Pathing/BotPathData.cs` | Handle pending path state in `CheckIfUpdateIsNeeded()` |

#### Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Bots wait too long for paths | Ramped batch size (Phobos approach) processes more jobs when queue is large |
| Path becomes stale while waiting | Bot position at submission time may differ from position when path is ready; include a staleness check |
| Complexity of async pathfinding | Start with the simpler throttle approach; upgrade to full async only if needed |

### 4.3 Add Teleport-with-Safety for Stuck Bots

This is included in Phase 2.1 (Two-Tier Stuck Detection) above. The teleport logic is part of the `HardStuckDetector`:

- Only triggers after path retry fails (10s of being stuck).
- Checks all human players for proximity (< 10m) and LOS.
- Teleports to the current path corner + 0.25m Y offset.
- Falls back to giving up after 15s total stuck time.

Reference implementation: `MovementSystem.HardStuckRemediation.AttemptTeleport()` at `/home/alberto/github/Phobos/Phobos/Systems/MovementSystem.cs:592-633`.

---

## 5. Phase 3: Major Changes (Longer-Term, Need-Driven)

These changes are **not recommended** for immediate implementation. They are documented here for reference if specific problems emerge.

### 5.1 Custom Movement System

**Estimated Complexity: XL (Extra Large)**

**Only pursue if:** BSG's `BotMover` causes specific, reproducible problems (e.g., bots ignoring paths, getting stuck at corners, failing to navigate specific terrain).

#### What It Would Involve

- Bypass BSG's `BotMover` entirely with direct `Player.Move()` calls.
- Implement per-frame movement: pose, prone, voxel updates, door slowdown, speed control, sprint gating.
- Implement corner following with epsilon thresholds (walk: 0.35^2, sprint: 0.6^2).
- Implement NavMesh.Raycast corner cutting.
- Implement path deviation spring force (closest point on path segment in 2D).
- Reference: `/home/alberto/github/Phobos/Phobos/Systems/MovementSystem.cs` (full file, 635 lines).

#### Why Not Now

- BSG's mover works adequately for QuestingBots' use case.
- Enormous surface area for bugs (every movement edge case must be re-handled).
- Requires Phase 5.2 (BSG Mover Handoff) as a prerequisite.
- No evidence of BSG mover being the root cause of current issues.

### 5.2 BSG Mover Handoff

**Estimated Complexity: L (Large)**

**Only needed if Phase 5.1 is adopted.**

#### What It Would Involve

- Implement `OnLayerChanged` handler to reset BSG mover state when transitioning between custom and BSG movement.
- Reset fields: `LastGoodCastPoint`, `PrevSuccessLinkedFrom_1`, `PrevLinkPos`, `PositionOnWayInner`, `LastGoodCastPointTime`, `PrevPosLinkedTime_1`.
- Call `mover.SetPlayerToNavMesh()` on handoff.
- Reference: `/home/alberto/github/Phobos/Phobos/PhobosLayer.cs:65-93`.

### 5.3 Utility AI Hybrid

**Estimated Complexity: L (Large)**

**Only pursue if:** extensibility becomes a real, pressing need (e.g., frequently adding new action types, or the priority chain becomes unmaintainable).

#### What It Would Involve

- Implement a scoring system for quest-related decisions.
- Keep priority-based logic for combat/survival (it works well for binary decisions).
- Add hysteresis to prevent action thrashing.
- Reference: Phobos `BaseTaskManager`, `ActionManager`, `StrategyManager` architecture.

#### Why Not Now

- The current if/else chain in `getSoloDecision()` is debuggable and correct.
- Most decisions are binary (combat/heal vs. quest).
- Adding a new action currently requires editing 3-5 files, which is manageable.
- Utility AI adds complexity (scoring functions, hysteresis tuning) without clear benefit at current scale.

---

## 6. Per-Item Implementation Details

### 6.1 Allocation Anti-Patterns (Phase 1)

| Attribute | Detail |
|---|---|
| **Complexity** | S (Small) |
| **Dependencies** | None |
| **QuestingBots files** | `BotHiveMindAbstractSensor.cs`, `BotHiveMindMonitor.cs`, `BotRegistrationManager.cs`, `BotJobAssignmentFactory.cs` |
| **Phobos reference** | N/A (general best practice) |
| **Risk** | Very low -- behavioral equivalence is maintained |
| **Testing** | Run existing test suite; verify bot behavior in-game is unchanged |

Specific changes enumerated in Section 3.1 above.

### 6.2 Stuck Detection Thresholds (Phase 1)

| Attribute | Detail |
|---|---|
| **Complexity** | S (Small) |
| **Dependencies** | None |
| **QuestingBots files** | `StuckBotRemediesConfig.cs`, `GoToPositionAbstractAction.cs` |
| **Phobos reference** | `MovementSystem.SoftStuckRemediation` constants |
| **Risk** | Low -- config defaults only; user can override |
| **Testing** | Observe bot behavior with stuck scenarios |

### 6.3 Two-Tier Stuck Detection (Phase 2)

| Attribute | Detail |
|---|---|
| **Complexity** | M (Medium) |
| **Dependencies** | None (standalone) |
| **QuestingBots files** | `GoToPositionAbstractAction.cs` (modify), 4 new files (create) |
| **Phobos reference** | `PositionHistory.cs`, `RollingAverage.cs`, `MovementSystem.cs:380-633` |
| **Risk** | Medium -- new detection logic could have false positives; teleport needs careful safety checks |
| **Testing** | Unit test `PositionHistory` and `RollingAverage`; integration test stuck detection in-game |

### 6.4 Batched Pathfinding (Phase 2)

| Attribute | Detail |
|---|---|
| **Complexity** | M (Medium) |
| **Dependencies** | None (standalone) |
| **QuestingBots files** | `StaticPathData.cs`, `BotPathData.cs` (modify), 2 new files (create) |
| **Phobos reference** | `NavJobExecutor.cs` (49 lines) |
| **Risk** | Medium -- async path handling adds complexity to update loop |
| **Testing** | Profile frame times with 20+ bots before/after; verify path quality unchanged |

### 6.5 Custom Movement System (Phase 3)

| Attribute | Detail |
|---|---|
| **Complexity** | XL (Extra Large) |
| **Dependencies** | Requires 6.6 (BSG Mover Handoff) |
| **QuestingBots files** | Major refactoring of all movement-related code |
| **Phobos reference** | `MovementSystem.cs` (635 lines), plus helper classes |
| **Risk** | Very high -- complete replacement of movement subsystem |
| **Testing** | Extensive in-game testing across all maps |

### 6.6 Utility AI (Phase 3)

| Attribute | Detail |
|---|---|
| **Complexity** | L (Large) |
| **Dependencies** | None |
| **QuestingBots files** | `BotQuestingDecisionMonitor.cs`, `CustomLayerDelayedUpdate.cs` (major refactoring) |
| **Phobos reference** | `BaseTaskManager`, `ActionManager`, `StrategyManager` |
| **Risk** | High -- replaces working decision logic with new scoring system |
| **Testing** | Extensive behavioral testing to ensure decision quality |

---

## 7. Dependency Graph

```
Phase 1 (independent, can be done in parallel):
  [1.1] Fix allocation anti-patterns
  [1.2] Improve stuck detection thresholds

Phase 2 (independent of each other, depend on Phase 1 being done):
  [2.1] Two-tier stuck detection  (builds on 1.2 threshold changes)
  [2.2] Batched pathfinding       (independent)
  [2.3] Teleport-with-safety      (included in 2.1)

Phase 3 (need-driven, not recommended now):
  [3.1] Custom movement system  --requires-->  [3.2] BSG mover handoff
  [3.3] Utility AI hybrid       (independent)
```

No circular dependencies exist. All Phase 1 items are independent of each other. Phase 2 items are independent of each other but benefit from Phase 1 being completed first (cleaner codebase).

---

## 8. Risk Assessment

| Risk | Severity | Likelihood | Mitigation |
|---|---|---|---|
| Allocation fixes break dictionary iteration | Low | Low | Only remove `ToArray()` where no structural mutation occurs; use deferred removal pattern where mutation is needed |
| New stuck detection has false positives | Medium | Medium | Speed-aware thresholds (EWMA) reduce false positives; configurable values allow tuning |
| Teleport visible to player | High | Low | Proximity + LOS checks; configurable enable/disable |
| Batched pathfinding causes delayed movement | Medium | Medium | Start with simple throttle; ramped batch size handles spikes |
| Phase 3 changes introduce regressions | High | High (if attempted) | Defer until specific problems justify the cost |

### Overall Risk Assessment

Phase 1 changes are **very low risk** -- they are mechanical transformations that preserve behavior while reducing allocations.

Phase 2 changes are **medium risk** -- they introduce new subsystems but are well-isolated and can be feature-flagged via config.

Phase 3 changes are **high risk** and should only be pursued with clear justification.

---

## 9. Recommended Implementation Order

1. **[Phase 1.1] Fix allocation anti-patterns** -- Zero-risk GC reduction. Start here.
   - Estimated effort: 2-3 hours
   - Files: 4 existing files modified

2. **[Phase 1.2] Improve stuck detection thresholds** -- Config value updates + vault-before-jump reordering.
   - Estimated effort: 30 minutes
   - Files: 2 existing files modified

3. **[Phase 2.1] Port two-tier stuck detection** -- Major behavioral improvement for stuck bots.
   - Estimated effort: 1-2 days
   - Files: 4 new files, 3 existing files modified

4. **[Phase 2.2] Implement batched pathfinding** -- Only if profiling shows `NavMesh.CalculatePath` is a frame-time bottleneck with 20+ bots.
   - Estimated effort: 1 day
   - Files: 2 new files, 2 existing files modified

5. **[Phase 3] Defer** -- Custom movement, BSG handoff, and utility AI are not recommended at this time.

---

## Appendix: Key File Reference

### QuestingBots Files

| File | Role |
|---|---|
| `src/SPTQuestingBots.Client/BotLogic/HiveMind/BotHiveMindMonitor.cs` | Boss/follower tracking, sensor coordination |
| `src/SPTQuestingBots.Client/BotLogic/HiveMind/BotHiveMindAbstractSensor.cs` | Base sensor with `botState.Keys.ToArray()` pattern |
| `src/SPTQuestingBots.Client/BehaviorExtensions/GoToPositionAbstractAction.cs` | Movement + stuck detection base class |
| `src/SPTQuestingBots.Client/BehaviorExtensions/CustomLayerDelayedUpdate.cs` | Brain layer with action dispatch |
| `src/SPTQuestingBots.Client/Controllers/BotRegistrationManager.cs` | Bot registration with `List.Contains()` |
| `src/SPTQuestingBots.Client/Controllers/BotJobAssignmentFactory.cs` | Quest assignment with LINQ chains |
| `src/SPTQuestingBots.Client/Models/Pathing/StaticPathData.cs` | Synchronous `NavMesh.CalculatePath()` |
| `src/SPTQuestingBots.Client/Models/Pathing/BotPathData.cs` | Per-bot path management |
| `src/SPTQuestingBots.Client/Models/BotSprintingController.cs` | Sprint control via `BotOwner.Mover.Sprint()` |
| `src/SPTQuestingBots.Client/BotLogic/BotMonitor/Monitors/BotQuestingDecisionMonitor.cs` | Decision priority chain |
| `src/SPTQuestingBots.Client/Configuration/StuckBotDetectionConfig.cs` | Stuck detection config |
| `src/SPTQuestingBots.Client/Configuration/StuckBotRemediesConfig.cs` | Stuck remedies config |

### Phobos Reference Files

| File | Role |
|---|---|
| `/home/alberto/github/Phobos/Phobos/Systems/MovementSystem.cs` | Custom movement + stuck remediation |
| `/home/alberto/github/Phobos/Phobos/Navigation/NavJobExecutor.cs` | Batched pathfinding |
| `/home/alberto/github/Phobos/Phobos/Helpers/PositionHistory.cs` | Position ring buffer |
| `/home/alberto/github/Phobos/Phobos/Helpers/RollingAverage.cs` | Speed averaging ring buffer |
| `/home/alberto/github/Phobos/Phobos/PhobosLayer.cs` | BSG mover handoff on layer change |
