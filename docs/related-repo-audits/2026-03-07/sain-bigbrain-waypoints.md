# Related Repo Audit: SAIN, BigBrain, Waypoints

Date: 2026-03-07

Scope: `/home/alberto/github/SAIN`, `/home/alberto/github/SPT-BigBrain`, `/home/alberto/github/SPT-Waypoints`

## SAIN

### High: `SAINExternal.CanBotQuest()` appears to treat stale under-fire timestamps as "recent"

Files:

- `/home/alberto/github/SAIN/SAIN/Interop/SAINExternal.cs:227`

Why this matters:

- `CanBotQuest()` delegates to `IsBotInCombat(...)`.
- The under-fire recency branch is `memory.UnderFireTime + TimeSinceUnderFireThreshold < Time.time`.
- If `UnderFireTime` is a "last under fire at" timestamp, that condition becomes true only after the threshold has already elapsed, which is the opposite of "recently under fire".

Why this matters to QuestingBots:

- QuestingBots consumes SAIN's external API to decide whether a bot should keep questing or extracting.
- An inverted "under fire recently" check can suppress questing long after combat should have cleared.

Recommended follow-up:

- Verify the intended meaning of `BotMemoryClass.UnderFireTime`.
- If it is a timestamp, invert the comparison and add a direct regression test around `CanBotQuest()`.

### High: version/build metadata exists but is not enforced at plugin startup

Files:

- `/home/alberto/github/SAIN/SAIN/SAINPlugin.cs:15`
- `/home/alberto/github/SAIN/SAIN/Plugin/AssemblyInfoClass.cs:21`

Why this matters:

- `AssemblyInfoClass` hardcodes `TarkovVersion = 40087` and `SPTVersion = "4.0.0"`.
- `SAINPlugin` does not perform an EFT/SPT preflight before enabling patches.
- The SPT dependency line is commented out in startup metadata.

Why this matters to QuestingBots:

- QuestingBots interoperates with SAIN heavily. If SAIN loads on a mismatched build and fails later inside reflection-heavy patches, QuestingBots inherits harder-to-diagnose breakage.

Recommended follow-up:

- Add the same kind of early compatibility preflight QuestingBots now uses.
- Re-enable an explicit SPT dependency check or replace it with an intentional version gate.

### Medium: broad reflection/patch surface with no automated tests

Files:

- `/home/alberto/github/SAIN/SAIN/Patches/VisionPatches.cs`
- `/home/alberto/github/SAIN/SAIN/Patches/MovementPatches.cs`
- `/home/alberto/github/SAIN/SAIN/Patches/GenericPatches.cs`

Why this matters:

- SAIN patches a large number of obfuscated methods and internal behaviors.
- I did not find a `tests/` directory or test project in the repo.

Why this matters to QuestingBots:

- QuestingBots depends on SAIN behavior, layer ordering, extraction, and interop contracts staying stable.
- Without regression coverage, upstream SAIN changes can silently invalidate QuestingBots assumptions.

## SPT-BigBrain

### High: plugin startup hard-fails on version mismatch instead of surfacing a structured dependency error

Files:

- `/home/alberto/github/SPT-BigBrain/BigBrainPlugin.cs:17`
- `/home/alberto/github/SPT-BigBrain/VersionChecker/TarkovVersion.cs:39`

Why this matters:

- `BigBrainPlugin.Awake()` calls `TarkovVersion.CheckEftVersion(...)` and throws `Exception("Invalid EFT Version")` on failure.
- The version checker logs and binds a config entry, but startup still ends in an exception-driven hard fail.

Why this matters to QuestingBots:

- BigBrain is a hard dependency for QuestingBots.
- A hard throw during dependency startup tends to produce worse diagnostics than explicit dependency errors and can obscure the actual root cause for downstream mods.

Recommended follow-up:

- Convert the failure mode to a dependency/preflight error instead of throwing from `Awake()`.

### High: layer activation patch depends on fragile obfuscated state with no guardrails

Files:

- `/home/alberto/github/SPT-BigBrain/Patches/BotBaseBrainActivateLayerPatch.cs:20`

Why this matters:

- The patch hardcodes the `List_0` field on the AI strategy type.
- It resolves the target method with `Single(...)` using both parameter type and parameter name `layer`.
- Any upstream rename, overload change, or deobfuscation drift will break patch discovery or behavior.

Why this matters to QuestingBots:

- QuestingBots installs custom BigBrain layers. If this patch stops ordering custom layers correctly, QuestingBots behavior can degrade even when its own code is unchanged.

Recommended follow-up:

- Replace name-sensitive discovery with stronger structural matching.
- Add a small reflection contract test suite that validates the expected field/method targets against the current EFT assembly.

### Medium: no automated tests for the framework QuestingBots depends on

Why this matters:

- I did not find a `tests/` directory or test project in the repo.
- BigBrain is foundational infrastructure for QuestingBots brain layers and actions.

## SPT-Waypoints

### High: navmesh replacement is global and asset-driven, with silent fallback when map bundles are missing

Files:

- `/home/alberto/github/SPT-Waypoints/Patches/WaypointPatch.cs:23`

Why this matters:

- On `BotsController.Init`, Waypoints may replace the entire scene navmesh via `NavMesh.RemoveAllNavMeshData()` and `NavMesh.AddNavMeshData(...)`.
- If the expected bundle is missing, the patch just returns without any explicit state signal beyond the absence of a debug log.

Why this matters to QuestingBots:

- QuestingBots pathing and custom mover behavior depend on the effective navmesh.
- A missing or partial bundle can change movement quality map-by-map without an obvious compatibility failure.

Recommended follow-up:

- Promote missing bundle cases to clearer warnings.
- Add a startup self-check that records whether custom navmesh injection actually occurred for the current map.

### High: `FindPathPatch` fully replaces EFT pathfinding with raw `NavMesh.CalculatePath`

Files:

- `/home/alberto/github/SPT-Waypoints/Patches/FindPathPatch.cs:15`

Why this matters:

- The patch always returns `false` to skip the original `BotPathFinderClass.FindPath`.
- The replacement path result is just `NavMesh.CalculatePath(...)` plus a `PathInvalid` check.

Why this matters to QuestingBots:

- QuestingBots consumes pathfinding heavily. Any semantic gap between EFT's original finder and this replacement propagates directly into quest movement, extraction routing, and door/path edge cases.

Recommended follow-up:

- Add compatibility tests or documented invariants around what behaviors this override intentionally drops from the original pathfinder.

### Medium: startup also uses exception-driven hard failure and has no test coverage

Files:

- `/home/alberto/github/SPT-Waypoints/WaypointsPlugin.cs:26`

Why this matters:

- Waypoints throws on both EFT version mismatch and dependency validation failure.
- I did not find a `tests/` directory or test project in the repo.

Why this matters to QuestingBots:

- Waypoints is a hard dependency. Startup failures and pathing regressions are user-visible in QuestingBots even if the root cause lives upstream.
