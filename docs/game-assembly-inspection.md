# Game Assembly Inspection Report

Analysis of `Assembly-CSharp.dll` (SPT 4.0.13) using AssemblyInspector's `validate`, `inspect`, `decompile`, and `xref` commands. All findings are based on the current game DLLs in `libs/`.

## KnownFields Validation

**Status: All 22 entries PASSED.** Zero field renames detected — the mod is fully compatible with the current game version.

## Type Inspection

### Obfuscation Density & Rename Risk

| Type | Total Fields | Obfuscated | % | Rename Risk |
|------|-------------|------------|---|-------------|
| BotCurrentPathAbstractClass | 5 | 4 | 80% | HIGH |
| NonWavesSpawnScenario | 15 | 15 | 100% | VERY HIGH |
| LocalGame | 4 | 4 | 100% | MODERATE |
| BotsGroup | 54 | ~10 | 19% | LOW |
| BossGroup | 1 | 1 | 100% | LOW (tiny type) |
| BotSpawner | 27 | ~5 | 19% | LOW |
| AirdropLogicClass | 26 | ~20 | 77% | HIGH |
| LighthouseTraderZone | 5 | 5 | 100% | HIGH |
| Player | 410 | ~50 | 12% | LOW |
| BotOwner | 138 | ~10 | 7% | LOW |
| AbstractGame | 13 | ~10 | 77% | MODERATE |

### Most Vulnerable KnownFields (rename risk on game update)

1. `NonWavesSpawnScenario.float_2` — 100% obfuscated type, many same-type siblings (float_0/1/2)
2. `BotCurrentPathAbstractClass.Vector3_0` — 80% obfuscated abstract class
3. `BossGroup.Boss_1` — suffix _1 suggests indexing that could shift
4. `AirdropLogicClass.AirdropSynchronizableObject_0` — obfuscated but unique type makes re-ID easy
5. `LocalGame.wavesSpawnScenario_0` — obfuscated but unique type

### Most Stable KnownFields

All 14 `k__BackingField` entries (BotsGroup combat state, BotOwner subsystems, Player.PlaceItemZone) — these are compiler-generated and extremely stable.

### GClass Instability Indicators

These GClass-typed fields are the most likely to break on game updates:

- **NonWavesSpawnScenario**: GClass1881 (×2), GClass1876, GClass1879 (now WaveInfoClass)
- **BotsGroup**: GClass573 (GroupDangerAreas), GClass578 (GrenadeSmokePlaces)
- **BotSpawner**: GClass1885 (SpawnDelaysService), GClass1888 (wave data)

We don't reference any GClass fields in KnownFields, which is a deliberate and smart stability choice.

## Potentially Useful Fields NOT Currently Used

### Tier 1: High-value, low-risk (named fields on stable types)

| Field | Type | Relevance to Mod |
|-------|------|------------------|
| `BotOwner.<PatrollingData>k__BackingField` | PatrollingData | Patrol route system |
| `BotOwner.<AssaultBuildingData>k__BackingField` | BotAssaultBuildingData | Room clearing |
| `BotOwner.<NearDoorData>k__BackingField` | BotNearDoorData | Door collision bypass |
| `BotOwner.<Ambush>k__BackingField` | BotAmbushData | Ambush task enhancement |
| `BotOwner.<Covers>k__BackingField` | BotCoversData | Cover point system |
| `BotOwner.<Mover>k__BackingField` | BotMover | Movement system access |
| `BotOwner.<SearchData>k__BackingField` | BotSearchData | Investigate task |
| `Player.<Environment>k__BackingField` | EnvironmentType | Indoor/outdoor for room clearing |
| `BotsGroup.Members` | List\<BotOwner\> | Squad member iteration |

### Tier 2: Useful, needs investigation

| Field | Type | Relevance |
|-------|------|-----------|
| `BotSpawner.ZonesPatrols` | Dict\<PatrolPoint, BotZone\> | Patrol zone mapping |
| `BotSpawner.AllBotZones` | BotZone[] | Zone enumeration |
| `BotsGroup.DeadBodiesController` | DeadBodiesController | Vulture system |
| `BotsGroup.PlacesForCheck` | List\<PlaceForCheck\> | Investigation points |
| `Player.Physical` | BasePhysicalClass | Stamina for sprint limiting |

### Tier 3: Nice to have, higher risk (obfuscated)

| Field | Type | Relevance |
|-------|------|-----------|
| `BotCurrentPathAbstractClass.Int_0` | int | Likely corner index |
| `BotCurrentPathAbstractClass.Float_0` | float | Likely path length |
| `AirdropLogicClass.Vector3_0` | Vector3 | Airdrop position for convergence |

### Direct Access (no reflection needed)

`BotsGroup.Members`, `BotSpawner.ZonesPatrols`, and `BotsGroup.DeadBodiesController` are public fields — they can be accessed directly without `AccessTools.Field()`.

---

## Cross-Reference Analysis

### Field Reference Counts (sorted by total references)

| Field | Total Refs | Reads | Writes | Scope |
|-------|-----------|-------|--------|-------|
| `AirdropLogicClass.AirdropSynchronizableObject_0` | 40 | 39 | 1 | Self-contained (0 external callers) |
| `BotCurrentPathAbstractClass.Vector3_0` | 25 | 24 | 1 | Self + debug gizmos |
| `BotSpawner.AllPlayers` | 16 | 15 | 1 | BotSpawner + spawn scenarios |
| `BotSpawner.Bots` | 13 | 12 | 1 | BotSpawner + GClass1890 |
| `Player._inventoryController` | 11 | 10 | 1 | Player + LocalPlayer async |
| `LocalGame.wavesSpawnScenario_0` | 8 | 7 | 1 | LocalGame + async state machine |
| `BotSpawner.OnBotRemoved` | 7 | 6 | 1 | Event pattern (add/remove/invoke) |
| `LighthouseTraderZone.physicsTriggerHandler_0` | 7 | 6 | 1 | Self-contained (Awake + OnDestroy) |
| `NonWavesSpawnScenario.float_2` | 6 | 2 | 4 | Update() only |
| `BossGroup.Boss_1` | 6 | 5 | 1 | Self-contained (.ctor + Dispose + getter) |

### Backing Field Property Callers

Backing fields all have exactly 2 direct refs (getter + setter). Real coupling is through the property:

| Property | Callers | Notes |
|----------|---------|-------|
| `BotsGroup.BotZone` | **67** | Patrol, pathfinding, danger zone, spawning, dead bodies, zone leave |
| `AbstractGame.GameTimer` | **22** | Exfiltration, timer UI, game lifecycle, spawn timing |
| `BotOwner.LeaveData` | **19** | Brain layers, boss management, zone leave controller |
| `BotOwner.Exfiltration` | **10** | Brain nodes, exfil decisions |
| `BotsGroup.EnemyLastSeenTimeReal` | **10** | Attack manager, fight logic, boss logic, brain layers |
| `BotsGroup.EnemyLastSeenTimeSence` | **5** | Boss logic, brain layers |
| `BotOwner.HearingSensor` | **4** | ExUsecBrainClass, Dispose, method_10 |
| `Player.PlaceItemZone` | **2** | GamePlayerOwner + HideoutPlayerOwner |
| `BotOwner.DangerArea` | **2** | Dispose + method_10 |
| `BotOwner.BotAvoidDangerPlaces` | **1** | GClass200 only |
| `BotsGroup.EnemyLastSeenPositionSence` | **1** | BossLogicUpdate only |
| `BotsGroup.EnemyLastSeenPositionReal` | **0** | **No game callers** — we may be the only consumer |

### Key Method Cross-References

| Method | Callers | Notes |
|--------|---------|-------|
| `Player.get_InventoryController` | **353** | Massively coupled — weapons, reload, grenades, items, UI |
| `BotSpawner.TryToSpawnInZoneInner` | 5 | Core spawn path, multiple entry points |
| `BotsGroup.IsPlayerEnemy` | 4 | Moderate coupling |
| `BotSpawner.BotDied` | 2 | Simple delegation chain |
| `BotSpawner.DeletePlayer` | 1 | Very isolated |
| `BotSpawner.AddPlayer` | 1 | Very isolated |
| `BotSpawner.GetGroupAndSetEnemies` | 1 | Used as callback, isolated |
| `NonWavesSpawnScenario.Update` | 0 | Unity lifecycle (engine-invoked) |
| `AirdropLogicClass.Init` | 0 | Unity lifecycle |
| `LighthouseTraderZone.Awake` | 0 | Unity lifecycle |

### Risk Assessment

**HIGH RISK (wide coupling):**
- `Player._inventoryController` — 353 callers, deeply embedded
- `BotsGroup.BotZone` — 67 callers, fundamental infrastructure
- `BotSpawner.AllPlayers` / `BotSpawner.Bots` — 13-16 refs, core spawner state

**MEDIUM RISK (moderate coupling):**
- `BotCurrentPathAbstractClass.Vector3_0` — 25 refs but self-contained
- `AbstractGame.GameTimer` — 22 callers, stable pattern
- `BotOwner.LeaveData` — 19 callers, moderate brain/zone coupling

**LOW RISK (few callers, stable patterns):**
- All remaining fields — 1-7 refs each, mostly self-contained
- All backing fields — exactly 2 refs (getter/setter), stable auto-property pattern

### Notable Findings

1. `BotsGroup.EnemyLastSeenPositionReal` has **0 game callers** — we may be the only consumer. Safe to depend on but game may remove it.
2. `AirdropLogicClass.AirdropSynchronizableObject_0` has 40 refs but ALL internal to AirdropLogicClass — zero external callers. Very stable.
3. `BotSpawner.DeletePlayer` and `BotSpawner.AddPlayer` each have only **1 caller** (BotsController) — simple delegation, very stable.
4. All backing fields use the standard auto-property pattern. Risk is in the property name, not the backing field name.

---

## Decompiled Game Logic Analysis

### Types Decompiled

| Type | Methods | Lines | Purpose |
|------|---------|-------|---------|
| BotSpawner | All public methods | ~750 | Bot spawning lifecycle |
| NonWavesSpawnScenario | Full class | ~200 | Scav spawn timing |
| BotsGroup | IsPlayerEnemy, AddEnemy, constructor | ~250 | Bot grouping, enemy detection |
| BossGroup | Full class | ~30 | Boss/follower tracking |
| BotBoss | Full class | ~300 | Boss self-assignment |
| BossSpawnScenario | Full class | ~250 | Boss wave spawning |
| BotsPresets | TryLoadBotsProfilesOnStart, CreateProfile | ~200 | Bot profile loading |
| AirdropLogicClass | Full class | ~200 | Airdrop lifecycle |
| LighthouseTraderZone | Full class | ~250 | Lighthouse trader zone |
| LocalGame + BaseLocalGame | vmethod_5 | ~80 | Game start lifecycle |
| BotsController | ActivateBotsByWave, SetSettings | ~60 | Bot activation |
| BotOwner | method_10 (brain activation) | ~60 | Brain startup |
| EnemyInfo | CheckLookEnemy | ~80 | Visibility system |
| BotsClass | GetListByZone | ~15 | Zone bot lookup |
| BossSpawnerClass | Spawn | ~200 | Boss spawn process |

### Patch Verification Results

| Patch | Status | Detail |
|-------|--------|--------|
| `BotsGroupIsPlayerEnemyPatch` (transpiler) | **Correct** | NOPs precisely target the `return false` for same-faction, preserving Scav loyalty/side checks |
| `GameStartPatch` (LocalGame.vmethod_5) | **Correct** | Fires post-initialization, pre-game-start — timing is right |
| `InitBossSpawnLocationPatch` | **Correct** | Handles BSG's escort double-counting quirk correctly |
| `AddEnemyPatch` | **Correct** | Narrowly scoped — only blocks `pmcBossKill` cause for our groups |
| `TrySpawnFreeAndDelayPatch` | **Correct** | float_2 confirmed as retry delay, default 10f |

### Issues Found in Patches

#### 1. BotDiedPatch — Missing IsDead Guard (Low Severity)

**Game code**: `BotSpawner.BotDied()` checks `if (!bot.IsDead)` before processing, preventing double-removal.

**Our patch**: Skips this guard for human-simulated bots. If `BotDied` is called twice (race condition), `OnBotRemoved` fires twice.

**Fix**: Add `if (bot.IsDead) return false;` at the start of the prefix.

#### 2. SetNewBossPatch — State Mismatch Risk (Medium Severity)

**Game's BossGroup.method_0**: Picks a RANDOM follower as new boss, calls `SetBoss()`, subscribes to OnBossDead.

**Our patch flow**:
- Prefix: clears BossToFollow for all followers
- Game runs method_0: promotes random follower, calls SetBoss(), subscribes events
- Postfix: overrides Boss_1 to find a follower with `IamBoss` set

**Risk**: Game promotes follower A, but our postfix sets Boss_1 to follower B (different `IamBoss` holder). Follower A already called `SetBoss()` and subscribed to events.

**Impact**: Could cause boss tracking inconsistency. Only triggers when boss dies with followers (relatively rare).

**Fix option**: Use prefix-only approach to prevent game's random promotion from conflicting.

#### 3. GetAllBossPlayersPatch — Over-Filtering (Medium Severity)

**Game**: Returns all players where `AIData.IAmBoss` is true.

**Our patch**: Returns only `!p.IsAI && p.AIData?.IAmBoss == true` — this filters out ALL AI bosses, not just our simulated ones.

**Impact**: If the game uses this for boss wave tracking, filtering real AI bosses could cause issues. Method appears to be an external query API rather than internal spawner logic.

**Fix option**: Filter only our generated bots rather than all AI bosses.

#### 4. BotOwner.method_10 — ActiveFail State (Low Severity)

**Game**: Wraps all brain activation in try/catch, setting `BotState = ActiveFail` on exception.

**Our postfix**: Runs even on ActiveFail, meaning `BotEntityBridge.RegisterBot()` might get a bot in a broken state.

**Fix option**: Check `BotState != ActiveFail` before registration.

### Thread Safety Observations

- `BotSpawner` uses `CancellationTokenSource` for async spawn operations
- `method_7` (SpawnBotsInZoneOnPositions) is async with `InSpawnProcess` counter
- `NonWavesSpawnScenario.Update()` runs on Unity main thread (MonoBehaviour)
- No explicit locks — all spawning relies on Unity's single-threaded update loop
- Our patches correctly avoid introducing threading concerns

### Obfuscated GClasses Referenced by Game Code

| GClass | Discovered Purpose |
|--------|-------------------|
| GClass1884 | Delayed bot spawn info (zone, count, data, callback) |
| GClass1885 | SpawnDelaysService — manages delayed spawn queue |
| GClass1876 | NonWaveGroupScenario spawner (group spawn logic) |
| GClass1881 | Weighted random selector (difficulty/role distribution) |
| GClass1888 | Spawned wave info (zone, count, data) — for OnSpawnedWave event |
| GClass575 | Bot zone group container (per-zone, per-side groups) |
| GClass856 | Utility class (RandomElement, Shuffle, IsTrue100, IsNullOrEmpty) |
| GClass2190 | Bot role classifier (IsBoss, IsFollower) |
| GClass675 | Quest-triggered spawn manager |
| GClass669 | Boss spawn process data (wave, zone, spawn point) |
| GClass684 | Profile backup manager |

---

## Actionable Items

### Bugs to Fix

1. **Add `IsDead` guard to BotDiedPatch** — prevent double-fire race condition
2. **Reconsider SetNewBossPatch** — prefix-only approach to avoid game's random promotion conflict
3. **Review GetAllBossPlayersPatch filter** — should it filter only our bots, not all AI bosses?
4. **Add ActiveFail check in BotOwnerBrainActivatePatch** — don't register bots in broken state

### Enhancement Opportunities

5. **Leverage BotOwner fields** — PatrollingData, AssaultBuildingData, Covers, SearchData, Mover could enhance existing systems
6. **Use direct field access** — BotsGroup.Members, BotSpawner.ZonesPatrols, BotsGroup.DeadBodiesController are public (no reflection needed)
7. **Add Player.Environment** — indoor/outdoor detection for room clearing without custom heuristics

### Monitoring

8. **Watch `EnemyLastSeenPositionReal`** — 0 game callers, could be removed in future update
9. **NonWavesSpawnScenario is 100% obfuscated** — highest rename risk on every game update; run `make validate-fields` after each update
10. **GClass references** — we correctly avoid GClass fields in KnownFields; continue this practice

---

## Tools Used

| Command | Usage | Purpose |
|---------|-------|---------|
| `make validate-fields` | Validate 22 KnownFields entries | Confirm field compatibility |
| `make inspect TYPE=X` | Inspect 11 game types | Discover field structures |
| `make inspect TYPE=X --include-inherited` | Walk base class chains | Find inherited fields |
| `make decompile TYPE=X` | Decompile 15 game types | Read actual game logic |
| `make decompile TYPE=X METHOD=Y` | Decompile specific methods | Focus on patched methods |
| `make xref TARGET=X.Y` | Trace 22 fields + 10 methods | Map coupling and usage |
