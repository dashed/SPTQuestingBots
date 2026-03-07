# Related Repo Audits

Date: 2026-03-07

This bundle extends the QuestingBots audit to the repos under `/home/alberto/github`
that most directly affect QuestingBots behavior, dependencies, or historical porting decisions.

## Reports

- [questingbots-follow-up.md](/home/alberto/github/SPTQuestingBots/docs/related-repo-audits/2026-03-07/questingbots-follow-up.md)
- [server-csharp.md](/home/alberto/github/SPTQuestingBots/docs/related-repo-audits/2026-03-07/server-csharp.md)
- [sain-bigbrain-waypoints.md](/home/alberto/github/SPTQuestingBots/docs/related-repo-audits/2026-03-07/sain-bigbrain-waypoints.md)
- [phobos-vulture.md](/home/alberto/github/SPTQuestingBots/docs/related-repo-audits/2026-03-07/phobos-vulture.md)
- [lootingbots-original.md](/home/alberto/github/SPTQuestingBots/docs/related-repo-audits/2026-03-07/lootingbots-original.md)

## Highest-Priority Cross-Repo Risks

### 1. Route and override behavior upstream is still order-sensitive

The upstream `server-csharp` router model executes every matching router and lets later handlers overwrite earlier outputs. That is especially relevant for mods like QuestingBots that shadow `/client/game/bot/generate`.

See:

- [server-csharp.md](/home/alberto/github/SPTQuestingBots/docs/related-repo-audits/2026-03-07/server-csharp.md)

### 2. Version compatibility handling is inconsistent across the ecosystem

- SAIN appears to carry version metadata without enforcing it at startup.
- BigBrain and Waypoints rely on exception-driven startup failure.
- Vulture pins a very old BigBrain version.
- Phobos uses dependency/timing assumptions instead of explicit readiness checks.

The pattern is that compatibility often fails late or unclearly unless a downstream mod hardens around it.

See:

- [sain-bigbrain-waypoints.md](/home/alberto/github/SPTQuestingBots/docs/related-repo-audits/2026-03-07/sain-bigbrain-waypoints.md)
- [phobos-vulture.md](/home/alberto/github/SPTQuestingBots/docs/related-repo-audits/2026-03-07/phobos-vulture.md)

### 3. Reflection-heavy interop surfaces are only weakly documented and lightly tested

- SAIN integration relies on runtime reflection and appears to have at least one potentially inverted combat gate.
- LootingBots interop docs have already drifted from the live API.
- Vulture's SAIN integration uses broad heuristic detection plus reflection.

This is the highest-probability place for silent behavior drift that won’t show up until a live raid.

See:

- [sain-bigbrain-waypoints.md](/home/alberto/github/SPTQuestingBots/docs/related-repo-audits/2026-03-07/sain-bigbrain-waypoints.md)
- [lootingbots-original.md](/home/alberto/github/SPTQuestingBots/docs/related-repo-audits/2026-03-07/lootingbots-original.md)
- [phobos-vulture.md](/home/alberto/github/SPTQuestingBots/docs/related-repo-audits/2026-03-07/phobos-vulture.md)

### 4. Movement/pathing repos make global behavior changes with limited regression coverage

- Waypoints globally swaps navmesh data and replaces EFT pathfinding.
- Phobos uses partial, still-experimental movement infrastructure.

These repos are powerful but they increase the blast radius of regressions when QuestingBots depends on their behavior.

See:

- [sain-bigbrain-waypoints.md](/home/alberto/github/SPTQuestingBots/docs/related-repo-audits/2026-03-07/sain-bigbrain-waypoints.md)
- [phobos-vulture.md](/home/alberto/github/SPTQuestingBots/docs/related-repo-audits/2026-03-07/phobos-vulture.md)

## Notes

- `SPTQuestingBots_original` is included as a historical reference, not as a current runtime baseline.
- These reports are read-only audits; no changes were made in the external repos themselves.
