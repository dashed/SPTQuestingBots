# Round 5 Reverse Engineering Findings

Fifth-round analysis covering six unexplored game systems: grenades/explosives, combat decisions, weapons/shooting, health/medical, group coordination, and boss-specific behaviors.

---

## Grenade & Explosive System

### Grenade Throwing Pipeline
`BotOwner.WeaponManager.Grenades` → `BotGrenadeController`: 7 precondition checks (group cooldown, AIPlaceInfo.BlockGrenade, friendly fire, etc.), physics-based parabolic trajectory via `GClass577`, 4-state machine (ready → change2grenade → grenadeReady → waitForEndThrow). Mass hardcoded to 0.5 regardless of grenade type.

### 7 Grenade Types
frag (highest bot priority), flash (second), stun, smoke, gas, incendiary, sonar. Selection: frag > flash > random from remaining.

### Trajectory Calculation (GClass577)
Parabolic with configurable angle (ANG_TYPE 1-6 → 15°-65°). Force multiplied by 1.3x safety margin. Obstacle detection via raycasts at GRENADE_PRECISION sample points. Random precision offset per-axis regenerated after each throw.

### 51 Settings in BotGlobalsGrenadeSettings
Covers throw power, precision, angles, distances, timing, flash/smoke handling, suppress deltas, notification chances. All per-difficulty tunable via `BotOwner.Settings.FileSettings.Grenade`.

### Grenade Evasion (BotBewareGrenade)
`ShallRunAway()` checked by brain layer (priority ~80). Creates GrenadeDangerPoint (GClass581) with BEWARE_TYPE tracking mode (1=avg position, 2=grenade transform, 3=initial impact). Finds cover via raycast from grenade. Voice line: `EPhraseTrigger.OnEnemyGrenade`.

### Mine/Tripwire System (BotMinesData)
Pre-placed `AIMinePoint` positions with Priority/SafeRad. Cache loads 8-10 nearby mines. `BotMinesRealtimePlaceFinder` dynamically places tripwires between path corners during movement. 10m minimum spacing.

### Pre-Placed Grenade Positions
`AIPlaceInfo.GrenadePlaces[]` → `ThrowGrenadePlace` with pre-computed From/Target/Force/Angle. Returns `alwaysGood` throw data. Some tied to doors.

### Group Coordination (BotsGroupGrenade)
5s cooldown for first throw, 2s for subsequent. Prevents multiple bots throwing simultaneously.

---

## Combat Decision Engine

### Brain Layer Stack (Default Assault)
15 layers, priorities 2-80. Combat layers: AssaultEnemyFar (59, hides when enemy far), PushAndSuppress (58, hard bots only), AssaultHaveEnemy (55, main combat), Pursuit (25, melee chase).

### ShallUseNow() Conditions
- **AssaultEnemyFar (59)**: `GoalEnemy != null AND distance > FAR_DISTANCE` (weapon-type dependent)
- **AssaultHaveEnemy (55)**: `GoalEnemy != null` (any known enemy)
- **Pursuit (25)**: `HavePursuitableEnemy AND PriorityAxeTarget.IsInPossibleRadius()`

### Combat Decision Tree (12 branches)
1. Under-fire check (3s cycle) → hide or fight
2. Heal if damaged + safe
3. Shoot immediately if enemy < SHOOT_IMMEDIATELY_DIST
4. DogFight if state active
5. Flank after 5s in cover + recently fired
6. Stationary weapon check
7. Search if enemy lost > COVER_SECONDS_AFTER_LOSE_VISION
8. Hold position in cover
9. Run to cover if no ammo
10. Stay prone if already prone + enemy visible
11. Run-by-shoots for scavs
12. Default: attackMoving

### Enemy Scoring (CalcWeight)
```
weight = distance + visibility_penalty + shootability_penalty + hit_recency_bonus
  Not visible: +100,000 | Sensed: +1,000 | Can't shoot: +20,000
  Hit within 1s: +0 (priority!) | Hit >1s ago: +clamp(1200×timeSinceHit, 0, 10000)
```
Lower weight = higher priority. Boss-specific choosers for Gluhar, Zryachiy, Killa, etc.

### DogFight Mode (Close-Quarters)
3-state: none → dogFight → shootFromPlace. Enter at DOG_FIGHT_IN, exit at DOG_FIGHT_OUT. Retreat: back up 2m along inverse enemy direction with NavMesh validation.

### Aggression System
Simple: only 2 triggers — ally death (FRIEND_DEAD_AGR_LOW, typically negative) and nearby kill (FRIEND_AGR_KILL, typically positive). Initial value randomized in [MIN/MAX_START_AGGRESION_COEF].

### Flanking
Triggered from EndShootFromCover after 5s in cover + recently fired. Flank point: `botPos - normalize(fireDir)*8 + normalize(awayDir)*3`. Two-phase: side-step then reposition.

### Prone Behavior (BotLay)
Entry: enemy far + cooldown + physics check. Get-up triggers: enemy behind, too close, angle too steep, hit count > DAMAGE_TIME_TO_GETUP, MAX_LAY_TIME timeout. Artillery forces prone.

---

## Weapon & Shooting System

### Shooting Pipeline (ShootData)
14 blocked states (Sprint, Jump, FallDown, Transition, etc.). Trigger timing from WeaponAIPreset: single shot (FINGER_HOLD_SINGLE_SHOT), auto (BASE_AUTOMATIC_TIME × Random(0.7,1.3)). Friendly fire: SphereCast radius 0.6 + SHPERE_FRIENDY_FIRE_SIZE.

### Aiming System (BotAimingClass)
Aim time: `(coverCoef × BOTTOM_COEF + angleCoef × distCoef × AccuratySpeed × panicCoef + nextAimingDelay) × moveCoef`. Scatter: power-law `pow(BaseShift + distance, scatteringDistModifier) × scatteringPerMeter`. Precision improves over time via BETTER_PRECICING_COEF.

### Bad Shoots (Intentional Miss)
First N shots miss: BAD_SHOOTS_MIN to BAD_SHOOTS_MAX per target. Additional bad shots scale with `log(1.2 + dist × 0.2)`. First contact: extra aim delay + forced misses on facing-away enemies.

### Recoil System
Additive only during auto fire. `yRecoil = Random(0, RECOIL_PER_METER × distance)`. Linear decay. Max Y capped at MAX_RECOIL_PER_METER × distance.

### Weapon Selection
Distance-based: LOW_DIST to FAR_DIST window. 25s cooldown between changes. Auto-return to main after 30s. Close weapons (pistol/shotgun/revolver) get IsCloseWeapon flag and reduced AmbushDistance.

### Fire Mode Switching
Every 3s: auto if enemy < DITANCE_TO_OFF_AUTO_FIRE, single if > that distance. Being hit from range forces single for 60s.

### Reload System
Priority: check meds → check weapon ready → check malfunction → empty mag → peace reload threshold. 4 types: magazine, ammo, barrel, revolver. Ammo replenishment from secured container to pockets.

### Malfunction (5-state machine)
None → UnknownMalfunction → KnownMalfunction → FixInProgress → Fixed. Brain layer priority 78. VALIDATE_MALFUNCTION_CHANCE controls whether bot even notices.

### 158 Total Settings
92 in FileSettings.Shoot + 66 in FileSettings.Aiming covering recoil, fire mode, weapon switching, reload, suppression, malfunction, melee, scatter, precision, ADS, movement, first contact, bad shoots, body part targeting.

---

## Health & Medical System

### BotMedecine Architecture
Three subsystems: FirstAid (healing), SurgicalKit (destroyed limb repair), Stimulators (stims). Role-specific: Sanitar gets RestoreFullHealth on apply, Zryachiy gets full heal when no enemy visible.

### Healing Priority
Heavy bleeding → Light bleeding → Surgery → HP below 65% threshold (PART_PERCENT_TO_HEAL). Taking damage cancels current healing (BeingHitAction → CancelCurrent).

### Healing in Combat Brain (5 integration points)
1. Pre-combat: in cover + enemy unseen 6s
2. In cover for 3s+ with damage
3. Stim usage from cover
4. Heal-another (Sanitar mechanic)
5. Chest HP < 52% → request group heal

### Stamina System (GClass774)
Current/TotalCapacity, Exhausted at < 15f threshold. Sprint blocked by: Overweight >= 1.0, UsingMeds, SprintDisabled, leg damage (unless on painkillers). Bosses get DoPainKiller() on combat activation — ignore leg damage for sprint.

### Physical Conditions (EPhysicalCondition)
OnPainkillers, LeftLegDamaged, RightLegDamaged, ProneDisabled, arm damage (1.5-2x aim drain), Tremor, SprintDisabled, Panic (increased stamina costs).

### Food/Drink
Timer-based (90s initial + random EAT_DRINK_PERIOD 30s). Requires patrol pair interaction. Gated by Mind.CAN_USE_FOOD_DRINK.

### Key Constants
| Constant | Value |
|----------|-------|
| PART_PERCENT_TO_HEAL | 0.65 (65%) |
| HEAL_DELAY_SEC | 5s |
| FOOD_DRINK_DELAY_SEC | 40s |
| EXHAUSTED_THRESHOLD | 15f |
| ManualUpdate cycle | 20s |

---

## Group Coordination & Voice System

### BotsGroup — Central Hub (54 fields)
Shared enemy registry propagated to all members. PlacesForCheck for investigation. Group tactic: averages all members' AggressionCoef × group power → Attack or Ambush. Followers always Protect.

### Voice System (110 EPhraseTrigger values)
Tactical commands (CoverMe, FollowMe, HoldPosition, Suppress, Spreadout, Regroup), directional callouts (LeftFlank, RightFlank, OnSix), resource requests (NeedFrag, NeedAmmo, NeedHelp), loot phrases, status, health.

### BotGroupTalk Throttle
GROUP_ANY_PHRASE_DELAY between any phrases, GROUP_EXACTLY_PHRASE_DELAY for same phrase. Per-group coordination prevents voice spam.

### Voice Reception (BotReceiver)
Handles: Silence (100m), NeedHelp (100m), FollowMe (10m), Stop (10m), GetInCover (10m), Spreadout (25m). Loyalty chance system with 120s cooldown. ForeverBlock on rejection.

### Group Request System (11 request types)
suppressionFire, followMe, attackClose, hold, goToPoint, throwGrenade, throwGrenadeFromPlace, hide, doorOpen, getInCover, wait. MAX_REQUESTS__PER_GROUP limit. Staggered pickup via bot ID. 30s block after failure.

### Dead Boss Succession
Random follower of same WildSpawnType becomes new boss via `BossGroup.method_0()`.

### PatrolFollowerType (6 types)
Close, CloseCover, CloseCoverWide, Scout, StayAtPlace, CloseCoverWithStop.

### BotEventHandler — Global Event Bus (33 delegates)
OnSoundPlayed, OnKill, OnGrenadeThrow, OnPhraseSay, OnHardAim, OnQETilt, OnGestusShow, OnBodyBotDead, OnTrainCome, BeingHitAction, and more. Canonical way to broadcast/receive events.

---

## Boss-Specific AI Behaviors

### Boss Architecture (3 pillars)
1. **BossLogic**: 21 per-type logic classes (ABossLogic subclasses)
2. **Brain classes**: Per-role layer stacks (4-12 layers each)
3. **BossSpawnerClass**: Zone selection, spawn point allocation, escort sub-data

### 22 Boss Types Mapped
Complete mapping of WildSpawnType → logic class, brain class, NeedProtection flag, patrol mode. Only Reshala/Killa/Tagilla/Sanitar/Gluhar set NeedProtection=true.

### Notable Boss Behaviors
- **Reshala**: Sends PERSONS_SEND followers to investigate after losing enemy. Grenade requests.
- **Killa**: KIBA alarm switches patrol routes. Escalating aggression (up to 6x) on bullet-near. Tanks grenades (no dodge layer).
- **Shturman**: Loot-centric territory defense. 40m flanking formations. Player zone entry detection with 10s/15s delayed reporting.
- **Gluhar**: 4-role followers (Security/Assault/Scout/Snipe). Reinforcement spawning when follower count drops. Train event integration.
- **Tagilla**: Melee-focused, inherits from Killa base. Simple melee chase via brain layers.
- **Sanitar**: Heals other bots via HealAnotherTarget. Drops IFAKs during patrol.
- **Knight**: Cross-group enemy sharing with exUsec groups.
- **Kaban**: Zone-wide coordination via BotEventHandler.AnyEvent("BossBoarBorn"). Smoke grenade tactics.
- **Partisan**: Stealth ambush boss with karma system and mine placement.

### 130+ Boss Settings
Per-boss config in BotGlobalsBossSettings for distances, timing, probabilities, follower counts, combat thresholds.

### 8 Boss Patrol Modes
bossRoundProtect, bossRoundProtectAndStay, bossCoverScouts, bossStayAtPlaces, byNameAndStay, groupMoving, follower, simple.

### ECoverPointSpecial Flags
Bitmask: noSnipePatrol=1, forFollowers=2, forBoss=4. Used to filter cover points for boss/follower-specific cover selection.

---

## Access Patterns

| Data | Access Path |
|------|-------------|
| Grenade controller | `BotOwner.WeaponManager.Grenades` |
| Currently throwing | `BotOwner.WeaponManager.Grenades.ThrowindNow` |
| Grenade evasion | `BotOwner.BewareGrenade.ShallRunAway()` |
| Danger places | `VoxelesPersonalData.CurVoxel.GetNearestPlacesToAvoid()` |
| Mine data | `BotOwner.MinesData` |
| Grenade settings | `BotOwner.Settings.FileSettings.Grenade` |
| DogFight state | `BotOwner.DogFight.DogFightState` |
| Current tactic | `BotOwner.Tactic.SubTactic.Tactic` |
| Aggression | `BotOwner.Tactic.AggressionCoef` |
| Goal enemy | `BotOwner.Memory.GoalEnemy` |
| Is in cover | `BotOwner.Memory.IsInCover` |
| Enemy chooser | `BotOwner.EnemyChooser.FindDangerEnemy()` |
| Prone state | `BotOwner.BotLay.IsLay` |
| Brain layer | `BotOwner.Brain.Agent.UsingLayer` |
| Last decision | `BotOwner.Brain.LastDecision` |
| Shooting state | `BotOwner.ShootData.Shooting` |
| Weapon ready | `BotOwner.WeaponManager.IsWeaponReady` |
| Close weapon | `BotOwner.WeaponManager.IsCloseWeapon` |
| Ammo count | `BotOwner.WeaponManager.Reload.BulletCount` |
| Reloading | `BotOwner.WeaponManager.Reload.Reloading` |
| Aim status | `BotOwner.AimingManager.CurrentAiming.Status` |
| Malfunction | `BotOwner.WeaponManager.Malfunctions.HaveMalfunction()` |
| Healing active | `BotOwner.Medecine.Using` |
| Need healing | `BotOwner.Medecine.FirstAid.Have2Do` |
| Is bleeding | `BotOwner.Medecine.FirstAid.IsBleeding` |
| Stamina | `BotOwner.GetPlayer.Physical.Stamina.Current` |
| Stamina exhausted | `BotOwner.GetPlayer.Physical.Stamina.Exhausted` |
| Overweight | `BotOwner.GetPlayer.Physical.Overweight` |
| Group requests | `BotOwner.BotsGroup.RequestsController` |
| Voice throttle | `BotOwner.BotsGroup.GroupTalk.CanSay()` |
| Places for check | `BotOwner.BotsGroup.AddPointToSearch()` |
| Is boss | `BotOwner.Boss.IamBoss` |
| Boss logic | `BotOwner.Boss.BossLogic` |
| Is follower | `BotOwner.BotFollower.HaveBoss` |
| Follower index | `BotOwner.BotFollower.Index` |
| Need protection | `BotOwner.Boss.NeedProtection` |
| Warning system | `BotOwner.BotsGroup.BotGroupWarnData` |
| Boss settings | `BotOwner.Settings.FileSettings.Boss` |

## Combined Priority List

### High Priority
1. **Stamina-aware sprint gating** — check Physical.Stamina.Exhausted before calling Sprint
2. **Healing state in utility scoring** — score movement at 0 while Medecine.Using
3. **Weapon readiness checks** — verify IsWeaponReady, !Reloading, CanShootByState before combat actions
4. **Malfunction state awareness** — skip questing when HaveMalfunction() (priority 78 overrides us)
5. **Grenade awareness during questing** — check BewareGrenade.ShallRunAway() (priority ~80 overrides at 18)
6. **Reload awareness in task scoring** — factor BulletCount/MaxBulletCount into combat task scores
7. **Physical condition sprint check** — respect SprintDisabled and leg damage flags
8. **Integrate BSG request system** — use BotGroupRequestController instead of custom dispatch
9. **Hook into BotEventHandler** — subscribe to OnSoundPlayed, OnKill, OnGrenadeExplosive canonically
10. **Fire mode awareness** — snipe/ambush tasks should prefer single fire at range

### Medium Priority
11. **Danger place avoidance** — use GetNearestPlacesToAvoid() + TryReplacePathAround in movement
12. **Aggression system extension** — add triggers beyond ally death/nearby kill (damage, time, ammo)
13. **DogFight threshold tuning** — adjust DOG_FIGHT_IN/OUT per personality
14. **Cover search influence** — inject virtual danger positions to bias cover selection
15. **Weapon type for task suitability** — IsCloseWeapon holders shouldn't snipe
16. **Leverage PlacesForCheck** — use AddPointToSearch() for investigation instead of custom system
17. **Respect group tactic state** — check Attack/Ambush/Protect for scoring adjustments
18. **Group grenade coordination** — check ThrowindNow before movement commands
19. **Coordinate healing with squad** — overwatch while squadmate heals
20. **Overweight movement check** — factor Overweight into post-loot sprint decisions
21. **Boss death succession** — watch OnBossDead for HiveMind leader transitions
22. **Gluhar reinforcement awareness** — account for reinforcement spawns in bot caps
23. **PatrolType.boss route avoidance** — don't send questing bots to boss patrol points

### Lower Priority
24. **Flanking trigger enhancement** — add squad-coordinated flank beyond EndShootFromCover
25. **Push/Suppress for all difficulties** — extend beyond hard-only
26. **Boss warning system awareness** — avoid unnecessary aggro near boss zones
27. **Injury-aware movement noise** — damaged legs increase MinStepSound
28. **Tactical grenade positions** — use AIPlaceInfo.GrenadePlaces for pre-computed throws
29. **Smoke for tactical movement** — BotSuppressGrenade.Init() for smoke throws
30. **Mine awareness in pathfinding** — avoid planted mine locations
31. **Suppression fire integration** — use BotSuppressShoot.Init() for our suppression
32. **Group voice throttling** — respect BotGroupTalk.CanSay()
33. **Follower index formation** — use BotFollower.Index for spread calculations
34. **ECoverPointSpecial filtering** — avoid forBoss/forFollowers covers for questing bots
35. **NeedProtection flag** — only Reshala/Killa/Tagilla/Sanitar/Gluhar need follower protection
36. **Boss-specific combat state** — read BossLogic.ShallAttack/FightAtZone
37. **Enemy scoring personality** — modulate CalcWeight with personality (aggressive = less distance weight)
38. **Prone decision integration** — ensure room clearing gets up when transitioning indoors
