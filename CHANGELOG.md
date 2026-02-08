# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
