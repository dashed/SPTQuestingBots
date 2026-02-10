# Making Raids Feel Alive — Deep Analysis

> **Goal**: Make bots move around the map in a way that makes raids feel much more alive and dynamic.

This document synthesizes research across 12 reference codebases (Phobos, SAIN, SPT-Waypoints, SPT-BigBrain, Vulture, LootingBots, and more) to identify what works, what's missing, and what to build next.

---

## Table of Contents

1. [Current State — What We Have](#1-current-state)
2. [Reference Mod Techniques](#2-reference-mod-techniques)
3. [Gap Analysis — Where Bots Feel Static](#3-gap-analysis)
4. [Proposed Improvements](#4-proposed-improvements)
5. [Implementation Roadmap](#5-implementation-roadmap)

---

## 1. Current State

### What Our Mod Does Today

SPTQuestingBots makes bots complete quests — they spawn, receive a quest objective, navigate to it, perform the action, wait 5 seconds, then get the next objective. This creates movement across the map but the movement is robotic: point-to-point GPS navigation with no organic variation.

### Brain Layer Stack

When SAIN is installed alongside QuestingBots, this is the full priority stack:

| Priority | Layer | Source | Purpose |
|----------|-------|--------|---------|
| 99 | SleepingLayer | QuestingBots | Disable distant bots (>1000m from players) |
| 99 | DebugLayer | SAIN | Debug only |
| 80 | SAINAvoidThreat | SAIN | Grenade/DogFight avoidance |
| 26 | BotFollowerRegroup | QuestingBots | Followers regroup to boss |
| 24 | ExtractLayer | SAIN | Move to extract (disabled when QuestingBots loaded) |
| 22 | CombatSquadLayer | SAIN | Squad combat decisions |
| 20 | CombatSoloLayer | SAIN | Solo combat decisions |
| 19 | BotFollowerLayer | QuestingBots | Followers track boss |
| 18 | BotObjectiveLayer | QuestingBots | **Core questing — the main movement driver** |
| — | BSG default patrol | BSG | Fallback: stand and pace in zone |

**Key insight**: When no layer above priority 18 is active, QuestingBots drives movement. When no QuestingBots layer is active (quest selection fails, between objectives), bots fall through to BSG's terrible default patrol — standing around and pacing.

### Utility AI Tasks (12 scored tasks)

| Task | Max Score | Behavior |
|------|-----------|----------|
| GoToObjective | 0.65 | Travel to quest objective (continuous distance-based decay) |
| Ambush | 0.65 | Hold ambush position |
| Snipe | 0.65 | Hold snipe position |
| HoldPosition | 0.65 | Hold at position |
| PlantItem | 0.65 | Plant quest item |
| UnlockDoor | 0.70 | Unlock blocking door |
| ToggleSwitch | 0.65 | Toggle quest switch |
| CloseDoors | 0.65 | Close nearby doors |
| Loot | 0.55 | Value-based loot scoring |
| Vulture | 0.60 | Multi-phase combat scavenging |
| Linger | 0.45 | Post-objective idle with linear decay |
| Investigate | 0.40 | Lightweight gunfire response |

All task scores are modified by personality (aggression float) and raid time progression multipliers via `ScoringModifiers.CombinedModifier()`.

**~~Problem~~**: ~~Most tasks score exactly 0 or 0.65 — binary, no gradient. Only Loot has genuine continuous scoring.~~ **Resolved**: GoToObjective now uses exponential distance decay, Linger uses linear time decay, all tasks have personality + raid time modifiers.

### Zone Movement System

We have a grid-based vector field system (inspired by Phobos):
- `WorldGrid`: Cell partitioning of the map
- `FieldComposer`: 4 field components — convergence (1.0), advection (0.5), momentum (0.5), noise (0.3)
- `DestinationSelector`: Picks best neighbor cell
- `ZoneObjectiveCycler`: Integrates as a "Zone Movement" quest type

**Problem**: Zone movement is a secondary quest type competing with specific quest objectives, not the default movement paradigm. The config has `spawn_point_wander.desirability: 0` — completely disabled.

### Movement Execution

- Custom `Player.Move()` path-following (Phobos-style) with sprint/walk, corner smoothing, and stuck detection
- Two movement speeds only: sprint or walk. No jog, slow walk, or creep.
- No posture variation — bots stand fully upright while moving
- No look variance — bots look where they're going, never checking flanks or scanning

---

## 2. Reference Mod Techniques

### 2.1 Phobos — Vector Field Zone Movement

Phobos is the gold standard for making bots move organically. Its core innovation is the **vector field system** that creates emergent movement without scripted routes.

**Grid System**:
- Map divided into 2D cells (25m for Factory, 75m for Customs, 125m for Woods)
- 5 location categories: ContainerLoot, LooseLoot, Quest, Synthetic, Exfil
- Empty cells get synthetic locations via NavMesh sampling
- Cover points collected from BSG voxels + sunflower spiral generation

**Advection Field** (static per-raid, Gaussian-sampled):
- Per-map zone definitions: BuiltinZones (from BotZone spawn points) + CustomZones (manual positions)
- Each zone has Force (can be negative = repulsor), Radius, Decay
- Gaussian sampling per raid = every raid has slightly different attraction patterns
- Example: Customs dorms has force range (-0.75, 1.5) — sometimes attracts, sometimes repels

**Convergence Field** (dynamic, 30-second updates):
- Pulls bots toward human player positions
- Per-map tuning: Customs 200-400m radius, Woods 500-1250m, Factory disabled
- `sqrt(1 - dist/normRadius)` falloff

**Congestion Anti-Clustering**:
- When a bot takes a cell, repulsive force `0.5/distance²` propagated to cells within range=3
- Dynamically prevents bot clumping

**Direction Selection** (`RequestNear`):
- Combines: advection + convergence + 0.5x momentum + 0.5x random
- Picks neighbor cell closest to preferential direction
- Random location within chosen cell

**Movement Lifecycle**:
- Single layer: PhobosLayer (priority 19)
- Two actions: GotoObjective (travel) + Guard (hold at cover point)
- Squad shares objective location, members get different cover points
- Guard durations: 60-180s normally, 3.5-6.5s for synthetic locations

**What Phobos does NOT do**: No patrol routes, no time-based behavior, no audio reaction, no stealth modes, no extract behavior, no idle animations, no formation movement.

### 2.2 SAIN — Combat Movement + Personality

SAIN has **no patrol system** — it relies on BSG defaults for peaceful movement. Its strength is combat and personality-driven behavior.

**Hearing-Based Investigation**:
- Gunshot/footstep detection with range modifiers (environment, occlusion, headphones)
- Per-personality reactions: Freeze (stand still 10-120s), Charge (rush source), SearchNow (investigate)
- `WillChaseDistantGunshots` personality toggle
- Sneaky bots: move at 0.25-0.5 speed, reduced pose when heard from peace

**Combat Movement Actions**:
- StandAndShoot, MoveToEngage, RushEnemy, Search, SeekCover, ShiftCover, DogFight
- Search: move to enemy's last known position, wait at corners, variable speed/pose
- Personality controls everything: aggression multiplier divides search/freeze time

**Extract Decisions**:
- Time-based (percentage of raid remaining vs. personality threshold) — **disabled when QuestingBots loaded**
- Injury-based (dying + no healing items)
- External (ForceExtract flag from other mods)

**Squad Movement**:
- GroupSearch: leader searches, followers follow within 2m
- Regroup: move to leader when too far (50-125m depending on threat)
- Suppress: fire at enemy while teammate retreats
- Radio comms: >1200m apart without earpiece = cannot coordinate

**Key SAIN Insight**: Personality is the master knob. A Wreckless bot rushes everything and sprints during search. A Coward freezes when hearing sounds and moves cautiously. This single axis creates massive behavioral variety.

### 2.3 Vulture — Multi-Phase Reactive Movement

The Vulture mod creates the most "alive" feeling through multi-phase approach behavior:

**Combat Event Detection**:
- Harmony patch on `Player.OnMakingShot` + grenade explosion subscription
- Per-map range multipliers: Factory 75m, Customs 300m, Woods 450m
- Time-of-day modifier: night 0.65x, day 1.0x
- Intensity scoring: events within radius + time window, explosions count 3x

**Multi-Phase Behavior** (the most interesting part):
1. **Approach**: Move to ambush point 25-30m from event, using cover if available
2. **Silent Approach** (last 35m): Speed 0.2, crouched pose 0.6
3. **Ambush Hold** (90s): Deep crouch (0.1), paranoid head swivels every 3-6s, 25% chance of decoy shots
4. **Greed** (60s): Push to event position, stand up, fast walk
5. **Dynamic Courage**: If intensity > threshold, stop and crouch until it drops

**Immersion Details**: Voice lines on approach, flashlight discipline while stalking, baiting with suppressive fire.

### 2.4 LootingBots — Environment-Driven Movement

LootingBots creates movement by pulling bots toward loot:

- `Physics.OverlapSphere` scan (3000 colliders, 80m range, 10s interval)
- **NavMesh path distance** for range checks, not straight-line
- Value-based filtering: 12K roubles min for PMCs, 5K for scavs
- Approach behavior: full speed until 6m, then slow down, crouch at arrival
- Squad coordination: `ActiveLootCache` prevents double-targeting

### 2.5 Waypoints — NavMesh Infrastructure

SPT-Waypoints provides the foundation all movement depends on:
- Custom NavMesh data per map (replaces BSG defaults)
- Door link system: NavMeshDoorLink objects for all doors, carvers for locked/unlocked states
- Switch/exfil door blockers with state change subscriptions

**Despite the name, Waypoints provides NO waypoints or patrol routes.** It's purely NavMesh infrastructure.

### 2.6 BSG Native AI — Known Limitations

Key limitations that all mods must work around:

1. **Zone Restrictions**: `AssaultEnemyFar` layer locks bots to their zone. Must be bypassed.
2. **StandBy Deactivation**: Bots far from players get frozen. Must disable `CanDoStandBy`.
3. **BotMover Quality**: Both SAIN and Phobos completely replace it with `Player.Move()`.
4. **MovementContext.IsAI**: Must patch to false to unlock player-grade movement (vaulting, smooth anims).
5. **Exfiltration Layer**: Priority 79 — can hijack behavior. Needs bypassing.
6. **Door NavMesh Carvers**: Create gaps that block pathing. Phobos shrinks them.
7. **PatrollingData**: BSG's patrol must be paused when custom layers are active.

---

## 3. Gap Analysis

### Critical Gaps (highest impact on "feeling alive")

#### Gap 1: No Between-Objective Organic Behavior

**The #1 reason bots feel robotic.** When a bot completes an objective:
1. Wait exactly 5 seconds (flat timer)
2. Beeline to next objective at full speed

No lingering, no looking around, no "I was heading this way and noticed something interesting." Bots are GPS navigators.

**What it should feel like**: Bot finishes a task, looks around for a moment, maybe checks a nearby room, notices a container, pauses to listen, then moves on. The transitions between objectives should be the interesting part.

**Files**: `BotObjectiveManager.cs:211-226`, `BotJobAssignmentFactory.cs:575-692`

#### Gap 2: Binary Movement Speed with No Variation

Bots have two modes: sprint or walk. No jog, no cautious creep, no speed variation based on context. Combined with always-upright posture and forward-only look direction, movement looks mechanical.

**What it should feel like**: A bot entering a building slows down and crouches. A bot crossing open ground picks up speed. A bot that heard something nearby moves cautiously with head on a swivel.

**Files**: `BotSprintingController.cs`, `GoToPositionAbstractAction.cs`, `CustomPathFollower.cs`

#### Gap 3: Quest-Centric Architecture

Everything is quest-driven. There's no concept of a bot "just existing" in the world. If a bot doesn't have a quest, it does nothing useful — falls through to BSG's default patrol (stand and pace).

**What it should feel like**: Bots should have baseline behaviors that don't require a quest: wander toward interesting areas, investigate sounds, check rooms, sit in a corner for a minute. Quests should be a layer ON TOP of organic behavior, not the only source of movement.

### High-Impact Gaps

#### Gap 4: Binary Utility AI Scoring

Most tasks score 0 or 0.65 — no gradient. GoToObjective is either "go" or "don't go." This means the utility AI rarely produces interesting decisions because there's nothing to weigh.

**Fix needed**: Distance-based decay for GoToObjective, time-in-raid modifier, personality influence, environment awareness (indoor/outdoor), recent combat proximity.

#### Gap 5: Zone Movement Underused

The zone movement grid+vector field infrastructure exists but is opt-in as an alternative quest type. `spawn_point_wander.desirability: 0` is completely disabled.

**Fix needed**: Make zone movement the default movement mode when no quest is active, not a competing quest type.

#### Gap 6: No Bot Personality or Narrative Arc

All bots do the same thing in the same way. No weapon-based behavior, no time-in-raid progression, no individual "style." A shotgun bot moves the same as a sniper. A bot 5 minutes into a raid moves the same as one 30 minutes in.

### Medium-Impact Gaps

#### Gap 7: BSG Patrol Fallback

When no QuestingBots layer is active, bots revert to BSG's terrible patrol. This happens during quest selection gaps, combat recovery periods, and when quest pool is exhausted.

#### Gap 8: No Spawn Entry Behavior

Bots spawn and instantly beeline to their first objective. No loading-in delay, no checking surroundings, no initial orientation.

#### Gap 9: Unexploited Config Knobs

Several powerful knobs exist but are disabled or hardcoded:
- `spawn_point_wander.desirability: 0` (disabled)
- `desirability_camping_multiplier: 1` / `desirability_sniping_multiplier: 1` (neutral)
- Zone movement field weights hardcoded in `FieldComposer`
- `default_wait_time_after_objective_completion: 5` (too short for organic feel)

---

## 4. Proposed Improvements

### Tier 1 — High Impact, Low-Medium Effort ✅ IMPLEMENTED

#### 1.1 Idle Behavior System (LingerTask) ✅

Implemented as `LingerTask` (`BotActionTypeId=Linger(15)`, BaseScore=0.45, hysteresis=0.10):
- Linear score decay: `baseScore * (1 - elapsed / duration)` over 10–30s random duration
- `LingerAction`: pauses patrol, slight crouch (pose=0.7), random head scans every 3–8s
- Gates: `ObjectiveCompletedTime > 0`, `!IsInCombat`, `LingerDuration > 0`
- `LingerConfig`: 10 JSON properties under `questing.linger`
- ~27 new tests

**Files**: `LingerTask.cs`, `LingerAction.cs`, `LingerConfig.cs`

#### 1.2 Speed and Posture Variation ✅

Implemented in `GoToObjectiveAction.Update()`:
- Indoor (`EnvironmentId == 0`): pose 0.8, no sprint
- Combat/suspicious: pose 0.6, no sprint
- Near objective (<30m): pose 0.75; within 15m: no sprint
- Personality affects base pose: lerp 0.8..1.0 by aggression
- Uses `Math.Min` to apply the most restrictive condition

**Files**: `GoToObjectiveAction.cs`

#### 1.3 Zone Movement as Default Fallback ✅

Implemented in `BotObjectiveLayer.IsActive()`:
- `tryZoneMovementFallback()` activates when `trySetNextActionUtility()` returns false
- Dispatches `GoToObjective` with "ZoneWander" reason
- `spawn_point_wander.desirability` bumped from 0 to 3

**Files**: `BotObjectiveLayer.cs`, config.json

#### 1.4 Variable Wait Times ✅

Implemented as `QuestObjectiveStep.SampleWaitTime()`:
- Random sampling from `[WaitTimeMin, WaitTimeMax]` range (default: 5–15s)
- `default_wait_time_after_objective_completion` reduced from 5s to 3s (linger adds 10–30s idle on top)
- Config: `wait_time_min` (5s) and `wait_time_max` (15s) in `questing` section
- 5 new config validation tests

**Files**: `QuestObjectiveStep.cs`, config.json

### Tier 2 — High Impact, Medium Effort ✅ IMPLEMENTED

#### 2.1 Investigate Task (Sound/Gunfire Response) ✅

Implemented as `InvestigateTask` (`BotActionTypeId=Investigate(16)`, MaxBaseScore=0.40, hysteresis=0.15):
- Gates: `HasNearbyEvent`, `!IsInCombat`, not already vulturing, `CombatIntensity >= threshold(5)`
- Scoring: intensity component (IntensityWeight=0.20) + proximity component (ProximityWeight=0.20)
- `InvestigateAction`: 2-state BigBrain action — cautious approach (speed 0.5, pose 0.6) → look around (head scanning, 5–10s)
- `InvestigateConfig`: 14 JSON properties under `questing.investigate`
- Personality-influenced via `ScoringModifiers.CombinedModifier()` — aggressive bots investigate more (1.2×)
- ~25 new tests

**Files**: `InvestigateTask.cs`, `InvestigateAction.cs`, `InvestigateConfig.cs`

#### 2.2 Personality-Influenced Scoring ✅

Implemented as `BotPersonality` + `ScoringModifiers`:
- `BotPersonality`: byte constants (Timid=0→Reckless=4) with aggression float (0.1→0.9)
- `PersonalityHelper`: maps from `BotDifficulty` (easy→Cautious, normal→Normal, hard→Aggressive, impossible→Reckless)
- `ScoringModifiers.PersonalityModifier()`: per-task lerp multipliers — aggressive bots rush (GoToObjective 1.15×), cautious bots camp (Ambush/Snipe 1.2×, Linger 1.3×)
- Applied to all 12 task `ScoreEntity` methods via `ScoringModifiers.CombinedModifier()`
- `PersonalityConfig` under `questing.personality`
- ~49 new tests

**Files**: `BotPersonality.cs`, `ScoringModifiers.cs`, `PersonalityConfig.cs`

#### 2.3 Time-of-Raid Behavior Progression ✅

Implemented as `ScoringModifiers.RaidTimeModifier()`:
- `RaidTimeNormalized` (0.0=start, 1.0=end) synced from game timer each HiveMind tick
- Per-task multipliers: early raid GoToObjective ×1.2 (rush), late raid Linger ×1.3 + Loot ×1.2 (cautious/looting)
- Combined with personality: `PersonalityModifier × RaidTimeModifier` = single multiplication per task
- Included in ScoringModifiers tests

**Files**: `ScoringModifiers.cs`, `BotEntityBridge.cs`

#### 2.4 Convergence Field Tuning ✅

Implemented as `ConvergenceMapConfig` + combat pull + time weight:
- Per-map convergence settings (radius, force, enabled) for all 12 maps — Factory disabled, Customs 250m/1.0, Woods 400m/0.8, etc.
- `CombatPullPoint`: temporary convergence boost toward recent gunfire (linear decay over 30s)
- `ConvergenceTimeWeight`: early raid 1.3× (creates encounters), mid 1.0×, late 0.7× (bots spread out)
- `CombatEventRegistry.GatherCombatPull()`: zero-alloc scanning for field integration
- Config: `convergence_per_map`, `combat_convergence_*` under `questing.zone_movement`
- ~33 new tests

**Files**: `ConvergenceMapConfig.cs`, `ConvergenceField.cs`, `FieldComposer.cs`, `WorldGridManager.cs`

### Tier 3 — Medium Impact, Medium-High Effort

#### 3.1 Spawn Entry Behavior

When a bot first spawns:
- 3-5 second pause (checking surroundings)
- Initial look scan (360 degrees over 3s)
- First objective biased toward spawn direction (not 180-degree turns)
- Squad members stagger departure (1-3s between members)

**Effort**: ~200 lines. New spawn state in `BotObjectiveManager`, modify `PMCGenerator`.

#### 3.2 Head-Look Variance

While moving, bots should occasionally:
- Glance at interesting objects (containers, doors, corpses) within 20m
- Check flanks every 5-15s (random head rotation ±45°)
- Look toward nearby sounds (if hearing sensor is available)
- Look at squad members when close

This uses BSG's `BotOwner.LookDirection` or `Player.Rotate()`.

**Effort**: ~250 lines. New `LookVarianceController`, integrate with movement system.

#### 3.3 Room Clearing Behavior

When entering a building (environment transition from outdoor to indoor):
- Slow down to walk speed
- Lower pose to 0.7
- Check corners (brief pauses at doorways)
- Clear rooms sequentially (use door detection from Waypoints)

**Effort**: ~350 lines. New `RoomClearAction`, environment transition detection.

#### 3.4 Continuous Scoring for GoToObjective ✅ (moved to Phase 1)

Implemented as part of Tier 1:
- `GoToObjectiveTask.ScoreEntity()`: `BaseScore * (1 - exp(-distance / 75))` — continuous distance decay
- Far from objective: score ≈ 0.65, close: score → 0, allowing other tasks to compete

### Tier 4 — Future Vision

#### 4.1 Per-Map Zone Configs (Phobos-style)

Define advection zones per map with attractor/repulsor forces:
- Dorms on Customs: strong attractor early, repulsor late
- Resort on Shoreline: moderate attractor
- Killa on Interchange: dynamic based on boss alive/dead

**Effort**: High. Per-map JSON configs, zone definition system.

#### 4.2 Dynamic Objective Generation

Instead of only using pre-defined quest positions:
- Generate objectives from live game state (fresh corpses, opened containers, active firefights)
- Loot-driven objectives from container/item scan
- "Clear this building" objectives generated from BotZone data

**Effort**: High. New objective generation system.

#### 4.3 Patrol Route System

Define named patrol routes per map (sequences of waypoints):
- Bots follow routes organically (not rigidly)
- Routes have patrol types: perimeter, interior, overwatch
- Dynamic route selection based on time, threat, personality

**Effort**: High. Route definition system, per-map JSON data, route-following logic.

---

## 5. Implementation Roadmap

### Phase 1: Quick Wins ✅ COMPLETE

1. ✅ **Variable wait times** (Tier 1.4)
2. ✅ **Speed/posture variation** (Tier 1.2)
3. ✅ **Enable spawn_point_wander** (config change)
4. ✅ **Continuous GoToObjective scoring** (Tier 3.4)

### Phase 2: Organic Behavior ✅ COMPLETE

5. ✅ **LingerTask** (Tier 1.1)
6. ✅ **Zone movement as fallback** (Tier 1.3)
7. ✅ **InvestigateTask** (Tier 2.1)

### Phase 3: Personality and Progression ✅ COMPLETE

8. ✅ **Personality scoring** (Tier 2.2)
9. ✅ **Time-of-raid progression** (Tier 2.3)
10. ✅ **Convergence tuning** (Tier 2.4)

### Phase 4: Immersion Polish (3-4 sessions)

**Goal**: The details that make bots feel like players.

11. **Spawn entry behavior** (Tier 3.1)
12. **Head-look variance** (Tier 3.2)
13. **Room clearing** (Tier 3.3)

---

## Key Architectural Principle

The most important insight from this research is:

> **Movement should be the default, not a task.**

Currently, bots need a quest to move. Phobos gets this right — bots ALWAYS have a destination because the vector field always provides a direction. Quest objectives should be high-priority attractors within the movement system, not the only source of movement.

The zone movement system we already have is the right foundation. It just needs to be the default mode instead of an optional quest type. Quests should modify the vector field (boosting convergence toward quest locations) rather than replacing the movement system entirely.

This architectural shift — from "quest drives movement" to "movement is constant, quests steer it" — is the single most impactful change we can make.

---

## Appendix: Reference Mod Summary

| Mod | Movement Innovation | What We Can Learn |
|-----|-------------------|-------------------|
| **Phobos** | Vector field zone system | Grid+advection+convergence+congestion = emergent organic movement |
| **SAIN** | Personality-driven combat | Aggression multiplier as master behavioral knob |
| **Vulture** | Multi-phase approach | Approach → Silent → Ambush → Greed creates tension |
| **LootingBots** | Environment-driven movement | OverlapSphere scan + NavMesh path distance = loot pulls bots naturally |
| **Waypoints** | NavMesh infrastructure | Custom NavMesh + door links = reliable pathfinding foundation |
| **BSG Native** | Limitations to work around | Zone restrictions, StandBy, BotMover quality, IsAI flag |
