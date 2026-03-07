# QuestingBots Follow-Up From Related Repo Audits

Date: 2026-03-07

This note translates the external repo audits into concrete follow-up work for
`SPTQuestingBots`. It is intentionally scoped to changes that are worth making
in this repo, not generic ecosystem observations.

## Recommended Actions

### 1. Add external interop contract tests

Priority: High

Why:

- The `SAIN`, `LootingBots`, and `Vulture` audits all point to reflection-heavy,
  weakly documented interop surfaces.
- `SPT-LootingBots` already has public docs that drift from the live API.
- `SAIN` appears to expose behavior that could drift silently across releases.

What to do in QuestingBots:

- Add tests around the method names and type names used in:
  - `src/SPTQuestingBots.Client/BotLogic/ExternalMods/Interop/SAINInterop.cs`
  - `src/SPTQuestingBots.Client/BotLogic/ExternalMods/Interop/LootingBotsInterop.cs`
- Lock in the exact reflected members we depend on.
- Make failures identify the missing type/member clearly, so an upstream mod
  update fails loudly in tests instead of only failing in raid.

Suggested test scope:

- Type resolution for `SAIN.Interop.SAINExternal, SAIN`
- Method resolution for:
  - `ExtractBot`
  - `TrySetExfilForBot`
  - `IsPathTowardEnemy`
  - `TimeSinceSenseEnemy`
  - `CanBotQuest`
  - `GetExtractedBots`
  - `GetExtractionInfos`
  - `IgnoreHearing`
  - `GetPersonality`
- Type resolution for `LootingBots.External, skwizzy.LootingBots`
- Method resolution for:
  - `ForceBotToScanLoot`
  - `PreventBotFromLooting`

### 2. Strengthen external-mod compatibility handling

Priority: High

Why:

- The audit bundle shows inconsistent compatibility behavior across the ecosystem:
  weak version gating in some repos, exception-driven startup failure in others,
  and heuristic detection in softer integrations.
- QuestingBots currently has good core SPT version preflight, but external-mod
  compatibility is still mostly presence-based plus logging.

What to do in QuestingBots:

- Tighten `AbstractExternalModInfo`, `ExternalModHandler`, `SAINModInfo`, and
  `LootingBotsModInfo` so we distinguish:
  - installed
  - version-compatible
  - interop-available
  - feature-enabled
- Escalate from warning-only behavior when the user has explicitly enabled a
  feature that requires working interop.

Examples:

- If `use_sain_for_extracting` is enabled and SAIN interop is not available,
  surface a stronger warning or dependency-style error.
- If LootingBots-specific behavior is expected and its external API cannot be
  resolved, make the fallback explicit in the logs.

### 3. Add a startup compatibility summary for external mods

Priority: Medium

Why:

- The current external-mod detection path logs individual findings, but the
  user still has to piece together whether the final runtime state is healthy.
- Several upstream repos fail late or unclearly.

What to do in QuestingBots:

- Emit one summarized startup report after `CheckForExternalMods()`:
  - mod name
  - detected version
  - compatible/incompatible
  - interop initialized/not initialized
  - fallback path being used

This should live close to:

- `src/SPTQuestingBots.Client/BotLogic/ExternalMods/ExternalModHandler.cs`

### 4. Expand route and conflict diagnostics on the server side

Priority: Medium

Why:

- The `server-csharp` audit showed that router dispatch is order-sensitive and
  matching is permissive.
- QuestingBots already intercepts `/client/game/bot/generate`, so hidden route
  conflicts are a real compatibility risk.

What to do in QuestingBots:

- Keep route contract tests around removed endpoints.
- Add more explicit startup warnings when known spawning/router mods are loaded.
- Consider broadening conflict detection around route-shadowing mods, not just
  spawn-count mods.

Relevant code:

- `src/SPTQuestingBots.Server/QuestingBotsServerPlugin.cs`
- `tests/SPTQuestingBots.Server.Tests/Routers/RouteContractTests.cs`

### 5. Keep current QuestingBots startup discipline; do not copy upstream anti-patterns

Priority: Medium

Why:

- The audits found several patterns worth explicitly not adopting:
  - fixed startup delays (`Phobos`)
  - heuristic plugin-name detection (`Vulture`)
  - exception-driven compatibility failure (`BigBrain`, `Waypoints`)
  - non-portable build assumptions (`Vulture`)

What this means for QuestingBots:

- Keep explicit version preflight.
- Prefer exact GUID checks over fuzzy plugin-name heuristics.
- Prefer structured warnings/errors over startup throws where possible.
- Keep reproducible local build/test flows instead of machine-specific setup.

## Watchlist, Not Immediate Changes

### SAIN `CanBotQuest()` suspected recency bug

The audit found a likely inverted under-fire recency check in SAIN. That is worth
tracking, but it is not an immediate QuestingBots code change because this repo
does not currently use `SAINInterop.CanBotQuest(...)` in live logic.

Action:

- Keep it on the watchlist.
- Re-evaluate only if QuestingBots starts relying on that SAIN API directly.

## Suggested Implementation Order

1. Add interop contract tests for SAIN and LootingBots.
2. Tighten external-mod compatibility state and failure handling.
3. Add one startup compatibility summary log/report.
4. Expand route/conflict diagnostics and related tests.

## Bottom Line

The biggest value to bring back into QuestingBots is not porting more code from
other repos. It is hardening the boundaries where QuestingBots depends on them:
interop contracts, compatibility checks, and route/conflict diagnostics.
