# CLAUDE.md

## Project Overview

SPT (Single Player Tarkov) mod that makes AI bots complete quests. Two-part plugin: server-side (SPT 4.x) + client-side (BepInEx). This is not malware; it is standard game modding.

## Build & Test Commands

```bash
make build          # Build server + client DLLs (requires libs/)
make build-server   # Build server plugin only
make build-client   # Build client plugin only
make test           # Run all unit tests
make test-server    # Run server tests only
make test-client    # Run client tests only
make ci             # CI pipeline — no game DLLs needed (format-check + client tests)
make ci-full        # Full CI — requires libs/ (format-check + lint + all tests)
make format         # Auto-format with CSharpier
make format-check   # Check formatting (CI-safe)
make copy-libs      # Copy game DLLs from SPT installation to libs/
make check-libs     # Verify all required DLLs are present
make clean          # Remove build artifacts
```

The .NET SDK lives at `~/.dotnet`. The Makefile handles `DOTNET_ROOT` and `PATH` automatically.

## Architecture

- **Server** (`src/SPTQuestingBots.Server/`, net9.0): SPT server plugin — config loading, quest data serving, PMC conversion, bot location management. 9 C# files.
- **Client** (`src/SPTQuestingBots.Client/`, netstandard2.1): BepInEx client plugin — BigBrain layers, bot behavior, quest objectives, spawning, patches. ~165 C# files.
- **Server tests** (`tests/SPTQuestingBots.Server.Tests/`, net9.0): NUnit + NSubstitute.
- **Client tests** (`tests/SPTQuestingBots.Client.Tests/`, net9.0): NUnit + NSubstitute.
- **Game DLLs**: 29 DLLs in `libs/` copied from SPT installation. Never committed. Run `make copy-libs SPT_DIR=/path/to/spt`.
- **Build output**: `build/SPTQuestingBots.Server.dll` + `build/SPTQuestingBots.dll`.
- **Quest data**: `quests/` (12 per-map JSON files) + `config/` (config.json, eftQuestSettings.json, zoneAndItemQuestPositions.json).

## Server Patterns

- DI via `[Injectable]` attribute, lifecycle via `IOnLoad`.
- Routes: `StaticRouter` / `DynamicRouter` with `RouteAction<T>`.
- Config: `ConfigServer.GetConfig<T>()`, custom `QuestingBotsConfigLoader`.
- Bot generation intercepted via `[Injectable(typeOverride: typeof(BotStaticRouter))]`.
- Plugin ID: `com.danw.sptquestingbots`.

## Client Patterns

- BepInEx plugin: `[BepInPlugin("com.DanW.QuestingBots")]`.
- Dependencies: BigBrain (`xyz.drakia.bigbrain`), Waypoints (`xyz.drakia.waypoints`).
- 4 BigBrain layers: Sleeping (99), Regrouping (26), Following (19), Questing (18).
- 13 action types via `CustomLogic` subclasses (GoToObjective, Ambush, Snipe, PlantItem, etc.).
- HiveMind system for boss/follower coordination (5 sensor types).
- 35 Harmony patches organized by category (core, spawning, scav limits, lighthouse, debug).
- Base classes: `CustomLayerDelayedUpdate`, `CustomLogicDelayedUpdate`, `GoToPositionAbstractAction`.

## Conventions

- Follow `.editorconfig` rules.
- Format with CSharpier before committing.
- Use conventional commits: `feat:`, `fix:`, `refactor:`, `test:`, `style:`, `chore:`, `docs:`.
- Keep tests mirroring src structure.

## Key Constraints

- `libs/` and `build/` are gitignored — never commit game DLLs or build output.
- `make ci` runs WITHOUT game DLLs (format-check + client tests only). `make ci-full` requires `libs/`.
- Adding new DLL references requires copying to `libs/` and adding a `<Reference>` in the relevant .csproj.
- Server tests can test server logic directly. Client tests cannot depend on Unity/game assemblies.
- Quest JSON files in `quests/` and `config/` ARE committed and should be kept in sync with game updates.
