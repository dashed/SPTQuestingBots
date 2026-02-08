# Integration Testing Guide

## Overview

Integration tests for SPTQuestingBots require actual SPT/EFT game assemblies that
cannot be distributed or included in CI. This document describes how to run
integration tests locally.

## Prerequisites

1. **SPT 4.0.x** installed locally
2. **Game assemblies** copied to `libs/` (see `docs/development.md`)
3. **.NET SDK 9.0** installed

## Test Projects

| Project | Target | Scope | CI-Compatible |
|---------|--------|-------|---------------|
| `SPTQuestingBots.Server.Tests` | net9.0 | Server logic, config validation | Yes (with libs/) |
| `SPTQuestingBots.Client.Tests` | net9.0 | Infrastructure smoke tests | Yes |

## Running Tests

```bash
# Run all tests (requires libs/ populated)
make test

# Run server tests only
make test-server

# Run client tests only
make test-client

# Run with verbose output
export DOTNET_ROOT="$HOME/.dotnet"
dotnet test tests/SPTQuestingBots.Server.Tests/ -v detailed
```

## Server Integration Tests

The server test project includes tests that exercise real SPT types from `libs/`:

- **Config deserialization**: Round-trips `config.json` through `QuestingBotsConfig`
- **PMC brain removal**: Tests against real `PmcConfig`/`BotConfig` record types
- **Bot hostility**: Tests against real `AdditionalHostilitySettings` models
- **Array validation**: Tests the plugin's startup validation logic

These tests use `RuntimeHelpers.GetUninitializedObject()` to construct SPT record
types that have `required` properties, and NSubstitute for `ISptLogger<T>` mocking.

## Client Integration Tests

Full client integration testing requires running inside the game (Unity runtime).
The `SPTQuestingBots.Client.Tests` project provides infrastructure smoke tests only.

**To test client code manually:**

1. Build the mod: `make build`
2. Copy output to SPT's `BepInEx/plugins/` directory
3. Launch SPT and verify:
   - Plugin loads without errors (check BepInEx log)
   - Bot questing behavior activates during raids
   - PMC/PScav spawning works as configured

## Adding New Tests

### Server tests
Add test files to `tests/SPTQuestingBots.Server.Tests/`. The project references
the server project directly. Use `internal` visibility + `InternalsVisibleTo` for
testing non-public methods.

### Client tests
Due to Unity/EFT dependencies, most client code cannot be unit-tested. If you
extract pure logic into framework-independent classes, those can be tested by
adding source file links to the test project:

```xml
<ItemGroup>
  <Compile Include="../../src/SPTQuestingBots.Client/SomePureClass.cs"
           Link="Linked/SomePureClass.cs" />
</ItemGroup>
```

## Known Limitations

- **GClass references**: Obfuscated class names (GClass522, GClass663, etc.) change
  between game versions. Tests using these types will break on version updates.
- **No Unity runtime**: Client components, MonoBehaviours, and coroutines cannot
  be tested outside Unity.
- **BigBrain dependency**: `CustomLayer`/`CustomLogic` types require BigBrain's
  DLL which in turn requires Unity assemblies.
