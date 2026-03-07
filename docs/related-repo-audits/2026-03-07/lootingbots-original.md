# Related Repo Audit: SPT-LootingBots, SPTQuestingBots_original

Date: 2026-03-07

Scope:

- `/home/alberto/github/SPT-LootingBots`
- `/home/alberto/github/SPTQuestingBots_original`

## SPT-LootingBots

### Medium: the public interop documentation has drifted from the actual API surface

Files:

- `/home/alberto/github/SPT-LootingBots/using_looting_bots_interop.md:6`
- `/home/alberto/github/SPT-LootingBots/using_looting_bots_interop.md:34`
- `/home/alberto/github/SPT-LootingBots/using_looting_bots_interop.md:39`
- `/home/alberto/github/SPT-LootingBots/using_looting_bots_interop.md:161`
- `/home/alberto/github/SPT-LootingBots/LootingBots/LootingBotsInterop.cs:70`
- `/home/alberto/github/SPT-LootingBots/LootingBots/LootingBotsInterop.cs:83`
- `/home/alberto/github/SPT-LootingBots/LootingBots/LootingBotsInterop.cs:122`

Why this matters:

- The doc links `LootingBotsInterop.cs` to the wrong GitHub file.
- The section header says `TryForceBotToScanLoot(BotOwner, float)` even though the actual method takes only `BotOwner`.
- The example signature uses `TryForceBotToLootNow`, which does not match the real API.
- The `GetItemPrice` example passes `botOwner` where the real method expects a loot item.

QuestingBots relevance:

- QuestingBots maintains its own reflection-based LootingBots interop bridge.
- Stale public docs increase the risk that future interop updates are copied from documentation instead of the live code.

Recommendation:

- Treat `LootingBotsInterop.cs` and `External.cs` as the source of truth, not the markdown guide.
- Update the guide or add a tiny interop contract test project.

### Medium: the interop layer is reflection-based but I did not find a visible automated test suite

Files:

- `/home/alberto/github/SPT-LootingBots/LootingBots/LootingBotsInterop.cs`
- `/home/alberto/github/SPT-LootingBots/LootingBots/External.cs`

What I checked:

- Searched for `test`, `tests`, `Tests`, and `Test` directories at repo depth 2 and found none.

Why this matters:

- The interop contract is exactly the sort of thing that drifts quietly across versions.
- QuestingBots depends on two of these methods: `ForceBotToScanLoot` and `PreventBotFromLooting`.

### Low: minor copy/paste drift already exists in the interop implementation comments

Files:

- `/home/alberto/github/SPT-LootingBots/LootingBots/LootingBotsInterop.cs:27`

Why this matters:

- `IsLootingBotsLoaded()` says "Only check for SAIN once".
- This is harmless at runtime, but it is a useful signal that the interop surface is maintained informally.

## SPTQuestingBots_original

### High: the original TypeScript mod still documents and implements the old dynamic `AdjustPScavChance` route

Files:

- `/home/alberto/github/SPTQuestingBots_original/src/mod.ts:89`
- `/home/alberto/github/SPTQuestingBots_original/src/mod.ts:91`
- `/home/alberto/github/SPTQuestingBots_original/src/mod.ts:95`
- `/home/alberto/github/SPTQuestingBots_original/src/mod.ts:97`

Why this matters:

- The TypeScript mod registers `/QuestingBots/AdjustPScavChance/` and scales the base PScav conversion chance dynamically.
- The route takes `Number(...)` input from the URL with no validation.

QuestingBots relevance:

- The C# port intentionally removed this route and moved to client-side chance interpolation plus server-side zeroing of vanilla conversion chance.
- This repo is the clearest source for why the route drifted semantically during the port: the original route expected a scalar factor, not a final interpolated chance.

### Medium: the original repo is pinned to SPT `3.11.2`-`3.12.0`, so it is reference material, not an executable comparison target

Files:

- `/home/alberto/github/SPTQuestingBots_original/package.json:7`

Why this matters:

- The repo's declared SPT version range is `>=3.11.2 <3.12.0`.
- Any behavior comparison to the current C# port must account for framework and server-API evolution first.

### Medium: README paths and operational guidance are historically useful but no longer match the current plugin layout

Files:

- `/home/alberto/github/SPTQuestingBots_original/README.md:90`
- `/home/alberto/github/SPTQuestingBots_original/README.md:346`
- `/home/alberto/github/SPTQuestingBots_original/README.md:351`

Why this matters:

- The README still centers the older `user\\mods\\DanW-SPTQuestingBots-#.#.#` layout.
- The current C# port uses the split server-mod plus BepInEx-plugin arrangement.

QuestingBots relevance:

- This is fine as an archival reference, but it should not be treated as current operational documentation.

### Medium: there is no modern automated test suite for the historical server/client pair

What I checked:

- Searched for `test`, `tests`, `Tests`, and `Test` directories at repo depth 2 and found none.
- The repo does include an old `bepinex_dev/SPTQuestingBots-InteropTest` utility project, but not a current regression suite.

## Summary

`SPT-LootingBots` is still the live interop contract QuestingBots should watch, but its public docs have already drifted from the code. `SPTQuestingBots_original` is valuable primarily as historical context for route semantics, layout migration, and old SPT assumptions. The key takeaway is that source code, not older markdown or the TypeScript mod's behavior, should drive future QuestingBots compatibility decisions.
