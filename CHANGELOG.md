# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Performance
- Eliminated `.Keys.ToArray()` allocations in HiveMind hot loops (`BotHiveMindAbstractSensor`, `BotHiveMindMonitor`) — 7+ array allocations removed per 50ms tick
- Replaced `Dictionary + OrderBy().First()` with simple min-tracking loop in `GetLocationOfNearestGroupMember()`
- Replaced LINQ chain in `GetAllGroupMembers()` with explicit for-loop
- Replaced `List<BotOwner>.Contains()` (O(n)) with `HashSet<BotOwner>` (O(1)) in `BotRegistrationManager`
- Replaced nested `.Where().Where().Where().Count()` LINQ in `BotJobAssignmentFactory.NumberOfActiveBots()` with counting for-loop
- Replaced `.Where()` x5 chain in `BotJobAssignmentFactory.GetAllPossibleQuests()` with reusable static buffer + for-loop
- Restructured `updateBossFollowers()` to use deferred removal pattern instead of `.ToArray()` for safe dictionary mutation

### Changed
- Stuck detection now attempts vault before jump (vault at 1.5s, jump at 3s) — vault is less disruptive
- Reduced stuck detection thresholds: vault 8s→1.5s, jump 6s→3s, debounce 4s→2s

### Added
- Detailed Phobos vs QuestingBots technical comparison (`docs/phobos-comparison.md`)
- Phobos lessons implementation plan with 3-phase roadmap (`docs/phobos-lessons-implementation-plan.md`)

## [1.0.0] - 2026-02-08

Initial C# port of SPTQuestingBots, based on the original TypeScript mod v0.10.3 by DanW.

### Added
- Full C# port of the server-side TypeScript mod (`mod.ts`, `CommonUtils.ts`, `BotLocationUtil.ts`, `PMCConversionUtil.ts`)
- C# solution with separate Server (net9.0) and Client (netstandard2.1) projects
- SDK-style `.csproj` files with shared properties via `Directory.Build.props`
- Makefile build system with targets: `build`, `test`, `format`, `lint`, `ci`, `copy-libs`, `check-libs`, `clean`
- `copy-libs` / `check-libs` Makefile targets for automated DLL management from SPT installations
- CSharpier code formatting integration
- `.editorconfig` with comprehensive C# code style rules
- NUnit 3.x + NSubstitute unit test infrastructure (55 server tests, client smoke tests)
- GitHub Actions CI workflow (format check, lint, tests)
- Developer documentation (`docs/architecture.md`, `docs/development.md`, `docs/migration.md`)
- Comprehensive README.md with architecture overview, setup instructions, and project structure
- Spawning mod detection logic (detects SWAG, MOAR, BetterSpawnsPlus, RealPlayerSpawn, BotPlacementSystem)
- `generateBots` interception for PScav conversion via `QuestingBotGenerateRouter` (overrides `BotStaticRouter` with `[Injectable(typeOverride)]`)
- Comprehensive XML documentation on all server-side C# files

### Fixed
- JSON serialization mismatch in `/QuestingBots/GetConfig` route (was using `System.Text.Json` instead of Newtonsoft, producing PascalCase keys instead of the expected snake_case)
- GClass obfuscated name changes for SPT 4.x (`GClass385`→`GClass395`, `GClass522`→`BotActionNodesClass`, `GClass168`→`BotNodeAbstractClass`, `GClass529`→`BotCurrentPathAbstractClass`, `GClass677`→`GClass699`, `GClass663`→`BotProfileDataClass`, `GClass3424`→`KeyInteractionResultClass`, `GClass3901`→`GClass1661`)
- Private fields now public properties in SPT 4.x (`BotMover`, `BotDoorOpener`, `BotExfiltrationData`, `BotsGroup`, `BotsController`, `BotSpawner`)
- SPT client API change: `AiHelpers` → `AIExtensions`
- `BotCreationDataClass.Create` updated for `GInterface22` parameter
- Server csproj `InternalsVisibleTo` moved to correct MSBuild element
- Test project DLL references fixed for runtime resolution

### Changed
- Server plugin ported from TypeScript/Node.js to C# for SPT 4.x compatibility
- Converted tsyringe DI patterns to .NET DI with `[Injectable]` attributes
- Converted StaticRouterModService/DynamicRouterModService to C# StaticRouter/DynamicRouter pattern
- Updated mod lifecycle from `IPreSptLoadMod`/`IPostDBLoadMod`/`IPostSptLoadMod` to `IPreSptLoadModAsync`/`IOnLoad`
- Reorganized client BepInEx plugin source into structured namespace hierarchy
- Updated all Harmony patches for SPT 4.x API changes
- Both server and client compile cleanly against SPT 4.x assemblies (0 errors, 0 warnings)

### Migration Notes
- Based on SPTQuestingBots v0.10.3 (SPT 3.x, `sptVersion: >=3.11.2 <3.12.0`)
- See [docs/migration.md](docs/migration.md) for detailed SPT 3.x to 4.x migration information
