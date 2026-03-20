# Round 3 Reverse Engineering Findings

Third-round analysis covering loot/inventory pricing, quest trigger zones, extraction internals, cover point quality, and sound propagation.

---

## Loot & Inventory System

### Pricing
- `HandbookClass.GetBasePrice(templateId)` is the only pricing API — flat ruble price, no rarity multipliers
- `ItemTemplate.Rarity` exists (Common/Rare/Superrare) but BSG never uses it for scoring
- BSG's own `CalcRarityScore()` (Common=1, Rare=5, Superrare=10) is dead code — never called

### BSG Bot Looting (GClass117 "LootPatrol")
- Cluster-based: `AILootPointsCluster` sums `GetBasePrice * StackObjectsCount` at raid start
- Distance decay: `value * 0.9^(dist/8)` — exponential, not linear
- Group coordination: boss reservation, `AllGroupWantLootCluster()` sync
- Per-point locking: `LockFor(ownerId, period)` with timeout release

### DeadBodiesController
- `GClass386` per-body: position, faction, isAI, createdTime, work-claim system
- `IsEnemiesBody(side)`: same-faction AI = not enemy, humans always enemy
- `SetUnderWork(bot)` / `IsFreeFor(bot)`: single-bot claim (one inspector at a time)

### Actionable Items
1. **Compute real container/corpse values** — iterate contents via `ItemOwner.RootItem` instead of flat 15k/20k defaults
2. **Use ItemTemplate.Rarity** — Common=1x, Rare=5x, Superrare=10x multiplier on handbook price
3. **Value-per-slot scoring** — `price / (Width * Height)` for packing efficiency
4. **Durability-adjusted armor comparison** — factor `Durability / MaxDurability` into upgrade decisions
5. **CanSellOnRagfair filter** — items that can't be sold on flea have lower resale value
6. **Stack-aware pricing** — multiply by `StackObjectsCount` (BSG already does this)
7. **BSG distance decay model** — `0.9^(dist/8)` exponential vs our linear model
8. **Corpse freshness** — `CreatedTime` vs current time, older = more likely already looted
9. **Weight budget** — check `Inventory.TotalWeight + item.Weight` before picking up heavy items

---

## Quest System & Trigger Zones

### Zone Architecture
- `TriggerWithId` → `QuestTrigger` (empty subclass) / `PlaceItemTrigger` (has `_beaconDummy`)
- Unity `OnTriggerEnter/Exit` → `player.AddTriggerZone(id)` / `player.RemoveTriggerZone(id)`
- Player tracks active zones in `Player.TriggerZones` (List<string>)
- Kill-in-zone: player must be inside trigger collider at kill time

### Why 75% Quest Failures (Root Cause Confirmed)
1. Zone colliders are large 3D volumes (entire buildings, 10-20m tall)
2. `collider.bounds.center` is at the volume center (e.g., floor 2 of a 3-story building)
3. NavMesh is a 2D surface only on walkable floors
4. Search distance was 5m — insufficient for tall zones
5. BSG's own collider bounds are unreliable (comment in NavMeshHelpers: "Bounds for exfiltration colliders are junk in EFT")

### PlaceItemTrigger Key Finding
- Has `_beaconDummy` GameObject — the intended "plant here" position
- We don't use it; we target the collider center instead
- Using `_beaconDummy.transform.position` would give the exact plant spot

### Quest Condition Types
- `ConditionVisitPlace`: zone ID tracked via `SessionCounters.TriggerVisited`
- `ConditionInZone`: array of `zoneIds[]` — ANY match = satisfied
- `ConditionPlaceBeacon`: requires BOTH correct item AND correct zone ID

### Actionable Items
1. **Use collider floor for tall zones** — when `extents.y > 2m`, project to `bounds.min.y + 0.75` instead of center
2. **Multi-floor NavMesh sampling** — sample every ~3m vertically for tall colliders
3. **Use BeaconDummy position** for PlantItem quests — exact intended plant position
4. **Expand zoneAndItemQuestPositions.json** — only 13 overrides currently, add known problem zones
5. **Use actual collider shape** — `BoxCollider.center + size` in local space, not `bounds` (AABB)
6. **Increase PoiScanner NavMesh tolerance** — currently 2m, many quest POIs silently excluded

---

## Extraction System

### ExfiltrationPoint State Machine
`Pending → RegularMode → Countdown → NotPresent`

### Key Mechanics
- `InfiltrationMatch`: checks `player.Profile.Info.EntryPoint` against `EligibleEntryPoints`
- `ExitTriggerSettings.Chance` (0-100): random per-raid availability
- Requirements: None, Empty backpack, HasItem, SkillLevel, ScavCooperation, Train, Timer, etc.
- Scav extraction uses distance-weighted random (farther exits preferred)

### Bot Extraction
- `BotExfiltrationData.TimeToExfiltration`: randomized between Min/Max (default both 1200s)
- Our code sets to `float.MaxValue` on init (prevent native), then `0f` to force
- `BotLeaveData.CheckCanLeave()`: no enemy, no boss/follower, min leave time

### Actionable Items
1. **Verify InfiltrationMatch for bots** — bots may not have entry points set correctly
2. **Check extraction requirements** — bots don't verify they meet exit requirements before heading there
3. **Distance-weighted extraction selection** — prefer farther exits like BSG's scav system

---

## Cover System

### Quality Scoring (CoverPointDefenceInfo)
8-direction raycasting at 8m range:
- Hit <2m: protected (no penalty)
- Hit 2-6m: exposed (+1)
- Hit >6m: very exposed (+2)
- No hit: open (+4)
- `IsSafe() = distanceCheckSum < 8` (enclosed on most sides)

### Cover Point Data
- `CoverLevel`: Stay (1.7m), Sit (1.0m), Lay (0.5m)
- `CoverType`: Wall (hard cover), Foliage (concealment only)
- `WallDirection`: protection direction vector
- `FirePosition`: lean-out position for shooting
- `CanLookLeft/Right` + `TiltType`: lean directions
- `ECoverPointSpecial`: noSnipePatrol, forFollowers, forBoss
- `IsSpotted`: time-limited flag with permanent `Block()` option

### Actionable Items
1. **Use CoverLevel for role assignment** — snipers to Stay/Sit, followers to any level
2. **Read IsSpotted state** — avoid placing squad on recently-spotted covers
3. **Use WallDirection for facing** — squad members face the wall's protection direction
4. **Use FirePosition** — lean-out position for fire arc validation
5. **CoverType awareness** — assault bots reject foliage, use for role-specific placement

---

## Sound Pipeline

### Sound Types (AISoundType)
Only 3: `step` (0), `silencedGun` (1), `gun` (2). Explosions are separate events.

### Hearing Formula
`hearingRange = CurrentHearingSense * power` — pure distance, no occlusion, no direction.

### Step Sound Falloff
Gradient between close/far thresholds:
- `< CloseHearingSense`: always hear
- `> FarHearingSense`: never hear
- Between: linear probability decay

### Sound Response
- Peaceful delay: `HEAR_DELAY_WHEN_PEACE` / `HEAR_DELAY_WHEN_HAVE_SMT`
- Dispersion: `DISPERSION_COEF` (steps) vs `DISPERSION_COEF_GUN` (guns have less noise)
- Bullet trajectory: `Vector3.Dot` + tangential distance <23m detection
- Cover spotted by sound: `dist < SOUND_TO_GET_SPOTTED` → marks cover compromised

### Actionable Items
1. **Read IsUnderFire** — suppress investigation when bot is already under fire
2. **Use PlaceForCheck look-around** — BSG generates radial look directions on arrival at danger points
3. **Account for sound dispersion** — heard positions have random noise, approach with uncertainty radius
4. **Bullet trajectory awareness** — could enhance combat-adjacent scoring

---

## Combined Priority List

### High Priority
1. Use BeaconDummy position for PlantItem quests (exact intended location)
2. Collider floor projection for tall zones (fix 75% quest failure root cause)
3. Compute real container/corpse values from contents
4. Use ItemTemplate.Rarity multiplier for loot scoring
5. Read IsSpotted state for cover selection

### Medium Priority
6. Value-per-slot loot scoring
7. Durability-adjusted armor comparison
8. Multi-floor NavMesh sampling for tall zones
9. BSG exponential distance decay for loot scoring
10. Use WallDirection for squad facing
11. Stack-aware pricing
12. Verify extraction InfiltrationMatch for bots

### Low Priority
13. CanSellOnRagfair filter
14. Weight budget for heavy items
15. Corpse freshness scoring
16. CoverLevel role assignment
17. Use FirePosition for fire arc validation
18. CoverType awareness for role placement
19. Sound dispersion uncertainty radius
