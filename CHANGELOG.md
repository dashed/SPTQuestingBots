# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-02-08

Initial C# port of SPTQuestingBots, based on the original TypeScript mod v0.10.3 by DanW.

### Added
- Full C# port of the server-side TypeScript mod (`mod.ts`, `CommonUtils.ts`, `BotLocationUtil.ts`, `PMCConversionUtil.ts`)
- C# solution with separate Server and Client projects targeting .NET Framework 4.7.2
- SDK-style `.csproj` files with shared properties via `Directory.Build.props`
- Makefile build system with targets: `build`, `test`, `format`, `format-check`, `lint`, `lint-fix`, `clean`, `ci`
- CSharpier code formatting integration
- `.editorconfig` with comprehensive C# code style rules
- NUnit 3.x + NSubstitute unit test infrastructure (server and client test projects targeting net9.0)
- Developer documentation (`docs/architecture.md`, `docs/development.md`, `docs/migration.md`)
- Comprehensive README.md with architecture overview, setup instructions, and project structure
- Spawning mod detection logic (detects SWAG, MOAR, BetterSpawnsPlus, RealPlayerSpawn, BotPlacementSystem and disables spawning system to avoid conflicts)
- Comprehensive XML documentation on all server-side C# files

### Fixed
- JSON serialization mismatch in `/QuestingBots/GetConfig` route (was using `System.Text.Json` instead of Newtonsoft, producing PascalCase keys instead of the expected snake_case)

### Changed
- Server plugin ported from TypeScript/Node.js to C# for SPT 4.x compatibility
- Converted tsyringe DI patterns to .NET DI with `[Injectable]` attributes
- Converted StaticRouterModService/DynamicRouterModService to C# controller pattern
- Updated mod lifecycle from `IPreSptLoadMod`/`IPostDBLoadMod`/`IPostSptLoadMod` to `IPreSptLoadModAsync`/`IOnLoad`
- Reorganized client BepInEx plugin source into structured namespace hierarchy
- Updated all Harmony patches for SPT 4.x API changes

### Known Issues
- The `generateBots` interception for PScav conversion is not yet ported from the original TypeScript

### Migration Notes
- Based on SPTQuestingBots v0.10.3 (SPT 3.x, `sptVersion: >=3.11.2 <3.12.0`)
- See [docs/migration.md](docs/migration.md) for detailed SPT 3.x to 4.x migration information
