# Related Repo Audit: Phobos, Vulture

Date: 2026-03-07

Scope:

- `/home/alberto/github/Phobos`
- `/home/alberto/github/Vulture`

## Phobos

### High: plugin uses BigBrain types but does not declare a BigBrain dependency

Files:

- `/home/alberto/github/Phobos/Phobos/Plugin.cs:9`
- `/home/alberto/github/Phobos/Phobos/Plugin.cs:20`

Why this matters:

- `Plugin.cs` imports `DrakiaXYZ.BigBrain.Brains` and registers `PhobosLayer`.
- The plugin declares `[BepInDependency("xyz.drakia.waypoints")]` but no BigBrain dependency.

QuestingBots relevance:

- QuestingBots ports several behavioral ideas from Phobos and also depends on BigBrain.
- Missing dependency metadata is a concrete example of how a mod can appear structurally compatible while still being load-order fragile.

Recommendation:

- Add an explicit BigBrain dependency and, ideally, a startup preflight for the expected framework version.

### High: startup order is intentionally delayed by a fixed five-second sleep instead of an explicit readiness check

Files:

- `/home/alberto/github/Phobos/Phobos/Plugin.cs:45`
- `/home/alberto/github/Phobos/Phobos/Plugin.cs:52`

Why this matters:

- `Awake()` starts a coroutine and `DelayedLoad()` waits a fixed five seconds "to allow all the 500 shonky mods" to load.
- This is a timing workaround, not a contract.

QuestingBots relevance:

- QuestingBots already had to harden startup sequencing around version preflight and patch enablement.
- Fixed waits are a classic source of machine-dependent and mod-pack-dependent failures.

Recommendation:

- Replace fixed delay bootstrapping with explicit dependency detection and readiness checks.

### Medium: `MovementSystem.MoveToDirect(...)` is still a hard `NotImplementedException`

Files:

- `/home/alberto/github/Phobos/Phobos/Systems/MovementSystem.cs:112`

Why this matters:

- The method exists in a core movement system and throws immediately.
- If any future call path starts using it, the failure mode is a hard runtime crash.

QuestingBots relevance:

- QuestingBots borrows movement and ECS ideas from Phobos.
- This is a useful warning that some Phobos patterns are still partial or experimental rather than production-hardened.

### Medium: no automated test suite was obvious in the repo

What I checked:

- Searched for `test`, `tests`, `Tests`, and `Test` directories at repo depth 2.
- Found no test project; only `Gym/` benchmarking code.

Why this matters:

- Phobos is a reference architecture for QuestingBots, but not a strongly regression-tested one.

## Vulture

### High: BigBrain dependency is pinned to a very old version

Files:

- `/home/alberto/github/Vulture/Source/Plugin.cs:11`
- `/home/alberto/github/Vulture/Source/Plugin.cs:12`

Why this matters:

- Vulture declares `[BepInDependency("xyz.drakia.bigbrain", "0.3.0")]`.
- That is far older than the versions QuestingBots currently targets.

QuestingBots relevance:

- QuestingBots ported the vulture behavior concept, but not the original plugin itself.
- Any direct integration testing against this repo needs to account for framework-version drift before attributing behavior differences to the behavior logic.

Recommendation:

- Update dependency metadata and add startup validation that fails clearly on framework mismatch.

### High: the project file is not portable because it hardcodes one developer's machine paths

Files:

- `/home/alberto/github/Vulture/Vulture.csproj:25`
- `/home/alberto/github/Vulture/Vulture.csproj:28`
- `/home/alberto/github/Vulture/Vulture.csproj:31`
- `/home/alberto/github/Vulture/Vulture.csproj:35`

Why this matters:

- `HintPath` values point at `/home/admin571397/Games/escape-from-tarkov/...`.
- That makes clean builds impossible without local manual surgery.

QuestingBots relevance:

- QuestingBots is now using a proper `Makefile` and reproducible local build/test flow.
- This repo is a useful contrast: the behavior design may be portable, but the build surface is not.

### Medium: SAIN integration detection is intentionally broad and reflection-heavy, so false positives and silent drift are plausible

Files:

- `/home/alberto/github/Vulture/Source/Integration/SAINIntegration.cs:19`
- `/home/alberto/github/Vulture/Source/Integration/SAINIntegration.cs:30`
- `/home/alberto/github/Vulture/Source/Integration/SAINIntegration.cs:69`
- `/home/alberto/github/Vulture/Source/Integration/SAINIntegration.cs:83`

Why this matters:

- The integration treats any plugin key or loaded assembly name containing `sain` as evidence SAIN is present.
- It then reflects concrete type/property names like `SAIN.Components.BotComponent`, `Info`, and `Personality`.
- Runtime failures are mostly suppressed once the integration path is entered.

QuestingBots relevance:

- QuestingBots also interoperates with SAIN, but it benefits from making those contracts explicit and testable where possible.
- This repo shows how quickly soft integration can become heuristic-driven.

### Medium: no automated test suite was obvious in the repo

What I checked:

- Searched for `test`, `tests`, `Tests`, and `Test` directories at repo depth 2.
- Found none.

## Summary

Phobos and Vulture are still useful idea sources, but they should be treated as design references, not trusted integration baselines. The main recurring pattern is startup and dependency looseness: missing or outdated dependency declarations, timing workarounds, reflection-heavy soft integrations, and almost no visible regression coverage.
