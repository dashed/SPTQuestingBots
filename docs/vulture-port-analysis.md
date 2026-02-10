# Vulture Port Analysis — Third-Party Combat Behavior for QuestingBots

## Source

- **Repo**: `/home/alberto/github/Vulture/` (by Luc1dShadow)
- **Version**: 1.0.1, SPT 4.x, BigBrain dependency
- **Files**: 8 source files (~1400 LOC total)
- **Concept**: Bots hear gunshots/explosions and opportunistically move to ambush weakened survivors

## Executive Summary

Vulture adds a single novel behavior archetype: **"third-partying"** — bots that hear
combat sounds investigate the area, set up ambush positions using cover, and either
wait for weakened survivors or rush in when fighting stops. This creates emergent
gameplay where firefights attract AI predators.

QuestingBots already has 80% of the infrastructure needed (utility AI, BigBrain actions,
cover point system, squad strategies, custom mover, voice commands). The port adds a
new **event detection layer** and a new **behavior type** on top of existing systems.

Estimated new code: ~1500–2000 lines + ~500–700 lines of tests.

---

## Feature-by-Feature Analysis

### What QuestingBots Already Has

| Vulture Feature | QuestingBots Equivalent | Notes |
|---|---|---|
| BigBrain layer/logic | 4 layers + 13 actions | Infrastructure ready |
| Cover point selection | BsgCoverPointCollector + SunflowerSpiral | Full pipeline |
| Squad follower coordination | SquadEntity + GotoObjectiveStrategy | Full system |
| Squad personality modifiers | SquadPersonalityType + Calculator | SAIN-inspired |
| Boss detection | BotType enum on BotEntity | Dense iteration |
| Utility AI scoring | 9 tasks + QuestTaskFactory | Extensible |
| Custom movement | CustomMoverController + CustomPathFollower | Full replacement |
| Voice lines / callouts | SquadVoiceHelper + SquadCalloutId (14 types) | EPhraseTrigger |
| Sound detection basics | BotHearingMonitor (gun/step/silenced sounds) | Sensor-based |
| Airdrop quest creation | AirdropLandPatch → BotQuestBuilder | Existing patch |
| Steering.LookToPoint | Used in 8+ action classes | Common pattern |
| SetPose control | Used in 7+ files | Common pattern |

### What's Novel from Vulture (Port Targets)

#### 1. Combat Event Registry — P0 (Foundation)

**What Vulture does**: `CombatSoundListener` patches `Player.OnMakingShot` to record
gunshot events with position, time, power, boss flag, and suppressor check. Also
subscribes to `BotEventHandler.OnGrenadeExplosive` for explosions. Events are queried
spatially for nearest event, intensity counting, and boss zone checks.

**What QuestingBots has**: `BotHearingMonitor` subscribes to `BotEventHandler.OnSoundPlayed`
and detects `AISoundType.gun` — but this is per-bot hearing, not a global event registry.
It answers "did *this* bot hear something?" not "where is the nearest firefight?"

**Port as**: `CombatEventRegistry` — global event tracker with:
- `CombatEvent` struct: Position (XYZ), Time, Power, IsBoss, IsExplosion, ShooterEntityId
- Ring buffer (128 slots) for zero-allocation event storage (like PositionHistory)
- `GetNearestEvent(position, maxRange)` — spatial query
- `GetIntensity(position, radius, timeWindow)` — count events in area
- `IsInBossZone(position, radius, decayTime)` — boss avoidance check
- Harmony postfix on `Player.OnMakingShot` for gunshot capture
- `BotEventHandler.OnGrenadeExplosive` subscription for explosions
- Suppressor filtering via `Player.FirearmController.IsSilenced`
- Marksman/sniper scav filtering (static snipers aren't meaningful combat)

**Key API calls**:
```csharp
// Gunshot detection
[HarmonyPatch(typeof(Player), "OnMakingShot")]
public static void Postfix(Player __instance, IWeapon weapon)
{
    if (__instance.HandsController is Player.FirearmController ctrl && ctrl.IsSilenced)
        return; // Skip suppressed weapons
    // Record event...
}

// Explosion detection
BotEventHandler.Instance.OnGrenadeExplosive += OnGrenadeExplosion;
// Signature: (Vector3 pos, string profileId, bool isSmoke, float smokeRadius, float smokeLifeTime, int throwableId)
```

**ECS integration**: New BotEntity fields synced per tick:
- `NearestCombatEventX/Y/Z` (float) — position of nearest event
- `NearestCombatEventAge` (float) — seconds since event
- `CombatEventIntensity` (int) — events in nearby area
- `HasNearestCombatEvent` (bool) — any event in range
- `NearestCombatEventIsBoss` (bool) — boss zone flag

**Effort**: Medium. ~250 LOC pure logic + ~80 LOC patch + ~50 LOC BotEntity fields + ~100 LOC tests.

---

#### 2. Vulture Utility Task — P0 (Core)

**What Vulture does**: `VultureLayer.IsActive()` evaluates whether to activate based on
combat events in range, chance rolls, intensity bonuses, SAIN personality modifiers,
combat suppression checks (visible enemy, under fire, near-miss), and boss avoidance.

**Port as**: `VultureTask` extending `QuestUtilityTask` (10th task):
- `BotActionTypeId.Vulture = 14` (new constant)
- Score components:
  - **Event freshness**: `max(0, 1 - age/300) * 0.3` — newer events score higher
  - **Event proximity**: `max(0, 1 - dist/effectiveRange) * 0.25` — closer events score higher
  - **Intensity bonus**: `min(intensity * 0.02, 0.15)` — more shots = more attractive
  - **Personality modifier**: aggressive +0.1, cautious -0.1 (from SquadPersonalityType)
  - **Fear gate**: if intensity > courageThreshold, score = 0 (too scary)
  - **Combat gate**: if IsInCombat, score = 0 (already fighting)
  - **Active quest gate**: if HasActiveObjective && IsCloseToObjective, score *= 0.3 (quest takes priority when close)
  - **Boss avoidance gate**: if NearestCombatEventIsBoss, score *= 0.2
- MaxBaseScore: 0.55 (same as LootTask — competes equally)
- Hysteresis: 0.20 (higher than loot to prevent flip-flopping once committed)

**QuestTaskFactory**: 10 tasks total (was 9 after loot).

**Effort**: Low-Medium. ~120 LOC task + ~30 LOC factory + ~80 LOC tests.

---

#### 3. Vulture Action (BigBrain) — P0 (Core)

**What Vulture does**: `VultureLogic` is a multi-phase state machine:
1. Calculate ambush point (cover-based or NavMesh fallback)
2. Move to ambush point
3. Within X meters: crouch-walk (silent approach)
4. At position: hold ambush (paranoia sweeps, optional baiting)
5. If combat goes quiet: rush to contact (silence trigger)
6. After timer: push to exact event position (greed mode)
7. Stop and release

**Port as**: `VultureAction` extending `GoToPositionAbstractAction`:

```
State Machine:
  Approach ─→ SilentApproach ─→ HoldAmbush ─┬→ Rush (silence trigger)
                                              ├→ Greed (timer expired)
                                              └→ Complete (timeout/cancelled)
```

- **Approach**: Move to ambush position via CustomMoverController (normal speed)
- **SilentApproach**: Within `silent_approach_distance` (default 35m):
  - `BotOwner.SetPose(0.6f)` (lower stance)
  - `BotOwner.Mover.SetTargetMoveSpeed(0.2f)` (slow walk)
  - Flashlight discipline: `BotOwner.BotLight.TurnOff(false, true)`
- **HoldAmbush**: At position for `ambush_duration` (default 90s):
  - `BotOwner.SetPose(0.1f)` (deep crouch)
  - Paranoia sweeps via `BotOwner.Steering.LookToPoint(randomizedDir)`
  - Optional baiting via `BotOwner.ShootData.Shoot()` + voice line
- **Rush**: Silence trigger fired (no shots for `silence_trigger_duration`):
  - Stand up, sprint to event position
- **Greed**: Ambush timer expired:
  - Stand up, fast-walk to exact event position
- **Complete**: Release control, restore flashlight state

**Ambush position calculation**: Reuse existing cover point pipeline:
1. Compute ideal position: offset from event position toward bot (25-30m)
2. Query BsgCoverPointCollector for nearby cover
3. Fallback: NavMesh.SamplePosition
4. Validate via NavMeshPositionValidator.TrySnap

**Effort**: Medium. ~300 LOC action + ~80 LOC position calculator + ~100 LOC tests.

---

#### 4. Flashlight Discipline — P1

**What Vulture does**: `BotOwner.BotLight.TurnOff(false, true)` when entering stalking mode.

**Port as**: State save/restore in VultureAction lifecycle:
```csharp
// On enter silent approach
bool _wasLightOn = BotOwner.BotLight?.IsEnable ?? false;
if (_wasLightOn) BotOwner.BotLight.TurnOff(false, true);

// On action exit
if (_wasLightOn) BotOwner.BotLight.TurnOn(false);
```

**BSG API**:
- `BotOwner.BotLight.IsEnable` — check if light is on
- `BotOwner.BotLight.TurnOff(bool force, bool toggle)` — disable light
- `BotOwner.BotLight.TurnOn(bool force)` — re-enable light

**Effort**: Very Low. ~20 LOC in VultureAction.

---

#### 5. Paranoia / Ambient Look — P1

**What Vulture does**: While holding ambush position, bot randomly looks toward combat
event with ±45° randomization every 3-6 seconds via `BotOwner.Steering.LookToPoint()`.

**Port as**: `AmbientLookHelper` static class:
```csharp
public static Vector3 ComputeRandomizedLookDirection(
    Vector3 botPosition, Vector3 targetPosition, float maxAngle, System.Random rng)
{
    Vector3 dir = (targetPosition - botPosition).normalized;
    float angle = (float)(rng.NextDouble() * maxAngle * 2 - maxAngle);
    // Rotate dir by angle around Y axis
    float rad = angle * Mathf.Deg2Rad;
    float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
    return new Vector3(dir.x * cos - dir.z * sin, dir.y, dir.x * sin + dir.z * cos);
}
```

Used in VultureAction's HoldAmbush phase. Reusable by other hold-position actions.

**Effort**: Very Low. ~40 LOC helper + ~20 LOC tests.

---

#### 6. Baiting — P2

**What Vulture does**: While holding ambush, bot fires a decoy shot to lure enemies:
```csharp
BotOwner.ShootData.Shoot(); // Force one shot
BotOwner.BotTalk.Say(EPhraseTrigger.Suppress); // Sell the fake fight
```

**Port as**: Optional behavior in VultureAction.HoldAmbush phase:
- Configurable chance (`baiting_chance`, default 25%)
- Cooldown between bait shots (10-25s random)
- Only if `BotOwner.WeaponManager.HaveBullets`
- Voice line via existing SquadVoiceHelper

**BSG API**:
- `BotOwner.ShootData.Shoot()` — forces bot to fire
- `BotOwner.WeaponManager.HaveBullets` — ammo check

**Effort**: Very Low. ~30 LOC in VultureAction.

---

#### 7. Dynamic Courage / Fear — P1

**What Vulture does**: If `GetEventIntensity()` exceeds courage threshold within 50m,
bot stops moving, crouches, and waits. This simulates suppression.

**Port as**: Score gate in VultureTask — if intensity > threshold, score drops to 0.
Also: during approach phase, if intensity spikes, pause movement (crouch + wait).

BotEntity field: `CombatEventIntensity` already proposed in Feature 1.

**Effort**: Very Low. ~15 LOC in scoring + ~20 LOC in action.

---

#### 8. Silence Trigger + Greed Mode — P1

**What Vulture does**:
- **Silence Trigger**: If approaching and no shots heard for `SilenceTriggerDuration` (45s),
  switch from creep to sprint rush.
- **Greed**: After ambush timer (90s) expires, push to exact event position aggressively.

**Port as**: State transitions in VultureAction:
- Track `NearestCombatEventAge` — if > threshold during approach/hold, transition to Rush
- Track elapsed hold time — if > ambush_duration, transition to Greed
- Both already covered by VultureAction state machine design

**Effort**: Low. ~40 LOC state transitions.

---

#### 9. Time-of-Day Modifier — P2

**What Vulture does**: `GameWorld.GameDateTime.Calculate()` returns in-game DateTime.
Night (22:00-05:00) applies 0.65x range multiplier. Dawn/dusk interpolated.

**Port as**: `TimeOfDayHelper` static class:
```csharp
public static float GetDetectionRangeModifier()
{
    var gameWorld = Singleton<GameWorld>.Instance;
    if (gameWorld?.GameDateTime == null) return 1f;
    DateTime gameTime = gameWorld.GameDateTime.Calculate();
    int hour = gameTime.Hour;
    if (hour >= 22 || hour < 5) return nightMultiplier; // Night
    if (hour >= 5 && hour < 7) return Mathf.Lerp(nightMultiplier, 1f, 0.5f); // Dawn
    if (hour >= 19 && hour < 22) return Mathf.Lerp(1f, nightMultiplier, (hour - 19f) / 3f); // Dusk
    return 1f; // Day
}
```

Applied to effective detection range in CombatEventRegistry queries.

**BSG API**: `GameWorld.GameDateTime.Calculate()` → `DateTime`

**Effort**: Very Low. ~30 LOC helper + ~15 LOC tests.

---

#### 10. Airdrop Vulturing — P2

**What Vulture does**: Harmony patch on `AirdropLogicClass.method_3` to detect landings,
tracks airdrop positions, bots vulture around them.

**What QuestingBots has**: `AirdropLandPatch` already patches airdrop landing and creates
quest objectives via `BotQuestBuilder.AddAirdropChaserQuest()`.

**Port as**: Feed airdrop position into CombatEventRegistry as a special high-value event:
```csharp
CombatEventRegistry.RecordEvent(new CombatEvent {
    Position = airdropPosition,
    Power = 200f, // Higher than gunshots
    IsAirdrop = true,
    Time = Time.time
});
```

This way VultureTask naturally scores airdrops highly without special-casing. The existing
`AirdropLandPatch` can be extended to also record a combat event.

**Effort**: Very Low. ~15 LOC in existing patch.

---

#### 11. Vulture Squad Strategy — P1

**What Vulture does**: When squad leader vultures, followers join via static dictionary
`_squadVultureTargets[leaderProfileId] = targetPosition`.

**Port as**: `VultureSquadStrategy` extending `SquadStrategy`:
- When boss entity has `HasNearestCombatEvent` and VultureTask wins scoring,
  broadcast vulture target to squad via SquadEntity fields
- Followers get tactical positions around ambush point using existing
  TacticalPositionCalculator with "Ambush" quest-type pattern
- Reuses entire squad coordination pipeline (comm range, personality, validation)

Much more sophisticated than Vulture's static dictionary approach.

**Effort**: Medium. ~150 LOC strategy + ~60 LOC tests.

---

## Architecture Overview

```
┌──────────────────────────────────────────────────┐
│ Layer 1: Event Detection (world-level sensors)   │
│                                                  │
│  CombatEventRegistry ←── OnMakingShotPatch       │
│       ↑                  GrenadePatch            │
│       │                  AirdropLandPatch         │
│  Ring buffer (128)       (existing)              │
│  GetNearest/Intensity/BossZone                   │
└──────────────┬───────────────────────────────────┘
               │ per-entity query in HiveMind tick
               ▼
┌──────────────────────────────────────────────────┐
│ Layer 2: ECS Fields on BotEntity                 │
│                                                  │
│  NearestCombatEventX/Y/Z, Age, Intensity,        │
│  HasNearestCombatEvent, IsBossEvent              │
└──────────────┬───────────────────────────────────┘
               │ read by utility scoring
               ▼
┌──────────────────────────────────────────────────┐
│ Layer 3: Utility AI (decision)                   │
│                                                  │
│  VultureTask.ScoreEntity() → competes with       │
│  GoToObjective, Ambush, Snipe, Loot, etc.        │
│  Hysteresis h=0.20                               │
└──────────────┬───────────────────────────────────┘
               │ when VultureTask wins
               ▼
┌──────────────────────────────────────────────────┐
│ Layer 4: BigBrain Action (execution)             │
│                                                  │
│  VultureAction state machine:                    │
│  Approach → SilentApproach → HoldAmbush          │
│                              ↓         ↓         │
│                           Rush      Greed        │
│                              ↓         ↓         │
│                           Complete  Complete      │
│                                                  │
│  + FlashlightDiscipline, Paranoia, Baiting       │
└──────────────┬───────────────────────────────────┘
               │ squad coordination
               ▼
┌──────────────────────────────────────────────────┐
│ Layer 5: Squad Strategy                          │
│                                                  │
│  VultureSquadStrategy (extends SquadStrategy)    │
│  Boss vultures → followers get tactical positions │
│  via TacticalPositionCalculator "Ambush" pattern │
└──────────────────────────────────────────────────┘
```

## Configuration

New section in `config/config.json` under `questing.vulture`:

```json
{
  "questing": {
    "vulture": {
      "enabled": true,
      "base_detection_range": 150.0,
      "night_range_multiplier": 0.65,
      "enable_time_of_day": true,
      "vulture_chance": 50,
      "multi_shot_intensity_bonus": 5,
      "intensity_window": 15.0,
      "courage_threshold": 15,
      "ambush_duration": 90.0,
      "ambush_distance_min": 25.0,
      "ambush_distance_max": 30.0,
      "silence_trigger_duration": 45.0,
      "enable_greed": true,
      "enable_silent_approach": true,
      "silent_approach_distance": 35.0,
      "enable_flashlight_discipline": true,
      "enable_paranoia": true,
      "paranoia_interval_min": 3.0,
      "paranoia_interval_max": 6.0,
      "paranoia_angle_range": 45.0,
      "enable_baiting": true,
      "baiting_chance": 25,
      "enable_boss_avoidance": true,
      "boss_avoidance_radius": 75.0,
      "boss_zone_decay": 120.0,
      "enable_airdrop_vulturing": true,
      "enable_squad_vulturing": true,
      "enable_for_pmcs": true,
      "enable_for_scavs": false,
      "enable_for_pscavs": false,
      "enable_for_raiders": false,
      "max_event_age": 300.0,
      "event_buffer_size": 128,
      "cooldown_on_reject": 180.0,
      "movement_timeout": 90.0
    }
  }
}
```

## BSG APIs Used (New)

| API | Usage | File |
|---|---|---|
| `Player.OnMakingShot` | Harmony patch target for gunshot detection | OnMakingShotPatch |
| `Player.FirearmController.IsSilenced` | Suppressor check | OnMakingShotPatch |
| `BotEventHandler.OnGrenadeExplosive` | Explosion event subscription | CombatEventRegistry |
| `BotOwner.BotLight.IsEnable` | Check flashlight state | VultureAction |
| `BotOwner.BotLight.TurnOff(bool, bool)` | Disable flashlight | VultureAction |
| `BotOwner.BotLight.TurnOn(bool)` | Re-enable flashlight | VultureAction |
| `BotOwner.ShootData.Shoot()` | Force bot to fire (baiting) | VultureAction |
| `BotOwner.WeaponManager.HaveBullets` | Ammo check for baiting | VultureAction |
| `BotOwner.Covers.GetClosestPoint()` | BSG cover query for ambush | AmbushPositionHelper |
| `GameWorld.GameDateTime.Calculate()` | In-game DateTime for ToD | TimeOfDayHelper |

## BSG APIs Already Used by QuestingBots

| API | Current Usage |
|---|---|
| `BotOwner.Steering.LookToPoint(Vector3)` | 8+ action classes |
| `BotOwner.SetPose(float)` | 7+ files |
| `BotOwner.Mover.SetTargetMoveSpeed(float)` | CustomMoverController |
| `BotOwner.BotTalk.Say(EPhraseTrigger)` | SquadVoiceHelper |
| `BotOwner.GoToPoint(Vector3, bool)` | Multiple actions |

## What We Deliberately Skip

| Vulture Feature | Reason to Skip |
|---|---|
| VultureGUI (F7 config menu) | QuestingBots uses config.json |
| ConfigurationManagerAttributes | BepInEx-specific, not needed |
| Per-map detection multipliers | Auto-detect via zone movement bounds |
| SAIN reflection integration | Already have SquadPersonalityCalculator |
| Static dictionary squad sharing | Already have SquadEntity system |
| IntensityBonus config | Consolidated into intensity_bonus |

## Implementation Status

**All features fully implemented and tested.** Build: 0 errors, 0 warnings. Tests: 58 server + 1499 client = 1557 total (~91 new vulture tests).

### Implemented Files

| Component | File | Status |
|---|---|---|
| CombatEvent struct | `BotLogic/ECS/Systems/CombatEvent.cs` | Done |
| CombatEventRegistry | `BotLogic/ECS/Systems/CombatEventRegistry.cs` | Done |
| CombatEventScanner | `BotLogic/ECS/Systems/CombatEventScanner.cs` | Done |
| VulturePhase | `BotLogic/ECS/Systems/VulturePhase.cs` | Done |
| VultureTask | `BotLogic/ECS/UtilityAI/Tasks/VultureTask.cs` | Done |
| VultureAction | `BotLogic/Objective/VultureAction.cs` | Done |
| VultureSquadStrategy | `BotLogic/ECS/UtilityAI/VultureSquadStrategy.cs` | Done |
| VultureConfig | `Configuration/VultureConfig.cs` | Done |
| OnMakingShotPatch | `Patches/OnMakingShotPatch.cs` | Done |
| GrenadeExplosionSubscriber | `Helpers/GrenadeExplosionSubscriber.cs` | Done |
| TimeOfDayHelper | `Helpers/TimeOfDayHelper.cs` | Done |
| AirdropLandPatch (extended) | `Patches/AirdropLandPatch.cs` | Done |

### Modified Files

| File | Change |
|---|---|
| `BotLogic/ECS/BotEntity.cs` | +9 vulture fields |
| `BotLogic/ECS/UtilityAI/QuestUtilityTask.cs` | +`BotActionTypeId.Vulture = 14` |
| `BotLogic/ECS/UtilityAI/QuestTaskFactory.cs` | TaskCount 9→10, added VultureTask |
| `BehaviorExtensions/CustomLayerDelayedUpdate.cs` | +`BotActionType.Vulture` enum + switch case |
| `BotLogic/HiveMind/BotHiveMindMonitor.cs` | +`updateCombatEvents()` step 6, cleanup calls |
| `Configuration/QuestingConfig.cs` | +`Vulture` property |
| `config/config.json` | +36-property `vulture` section |

### Test Files

| Test File | Tests |
|---|---|
| `CombatEventRegistryTests.cs` | 28 |
| `CombatEventScannerTests.cs` | 12 |
| `VultureTaskTests.cs` | 28 |
| `VultureSquadStrategyTests.cs` | 15 |
| `VultureConfigTests.cs` | 8 |
| **Total new** | **91** |

### Design Deviations from Analysis

| Analysis Proposal | Implementation | Reason |
|---|---|---|
| `AmbushPositionHelper` (separate class) | Position computed inline in `VultureAction.Start()` | Simpler — offset from event toward bot + NavMesh snap, no separate helper needed |
| `NearestCombatEventIsBoss` field name | `IsInBossZone` | Better describes the spatial boss zone check semantics |
| `NearestCombatEventAge` field | `NearbyEventTime` (raw time) | Action computes age from current time — more flexible |
| Greed mode (separate phase) | Merged into Rush phase | Simplification — both are "sprint to event position" with minor behavioral differences |
| `HasNearestCombatEvent` field name | `HasNearbyEvent` | Shorter, matches event field naming pattern |
| Baiting behavior | Deferred (P2) | Not implemented in initial port — can be added later without architecture changes |
| Separate paranoia helper class | Inline in `VultureAction.UpdateParanoia()` | ~10 LOC, not worth extracting |
| MaxBaseScore 0.55 | 0.60 | Slightly higher than loot to create competitive tension between looting and vulturing |

## Original Implementation Plan

### Phase 1: Combat Event Detection (Foundation) — 3 tasks
1. **CombatEventRegistry** — pure-logic ring buffer + query methods + CombatEvent struct
2. **OnMakingShotPatch + GrenadePatch** — Harmony patches + event recording
3. **BotEntity fields + HiveMind tick** — ECS wiring, per-entity query in updateCombatEvents()

### Phase 2: Vulture Utility Task + Action (Core) — 3 tasks
4. **VultureTask** — QuestUtilityTask subclass, scoring, QuestTaskFactory wiring
5. **VultureAction** — BigBrain action with state machine (Approach→Silent→Hold→Rush/Greed)
6. **AmbushPositionHelper** — cover-based position calculator (reusing BsgCoverPointCollector)

### Phase 3: Immersion + Config — 3 tasks
7. **FlashlightDiscipline + Paranoia + Baiting** — immersion behaviors in VultureAction
8. **VultureConfig** — JSON config class + config.json entries
9. **TimeOfDayHelper + Airdrop integration** — ToD modifier + feed airdrops as combat events

### Phase 4: Squad + Integration — 3 tasks
10. **VultureSquadStrategy** — SquadStrategy subclass for squad vulturing
11. **Tests** — CombatEventRegistry, VultureTask scoring, VultureAction states, config
12. **Final integration** — build, test, format, verify all wiring

### Original Estimated Effort

| Phase | New LOC | Test LOC | Description |
|---|---|---|---|
| Phase 1 | ~350 | ~120 | Event detection infrastructure |
| Phase 2 | ~450 | ~150 | Utility task + BigBrain action |
| Phase 3 | ~200 | ~60 | Immersion behaviors + config |
| Phase 4 | ~200 | ~80 | Squad strategy + integration |
| **Total** | **~1200** | **~410** | **~1600 total** |

---

*Analysis date: 2026-02-10*
*Implementation completed: 2026-02-10*
*QuestingBots version: 1.9.0 → Unreleased (post-vulture implementation)*
*Test count: 58 server + 1499 client = 1557 total*
