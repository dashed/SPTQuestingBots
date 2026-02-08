# Developer Setup Guide

This guide covers setting up a development environment for building, testing, and debugging SPTQuestingBots.

---

## Prerequisites

### Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| .NET SDK | 9.0 | Build and test runner |
| SPT (Single Player Tarkov) | 4.x | Source of required game DLLs |
| Git | 2.x+ | Version control |

The plugin targets **netstandard2.1** for SPT 4.x compatibility (set in `Directory.Build.props`), and the .NET 9.0 SDK handles compilation. Test projects target `net9.0`.

### Recommended IDE

Any of these work well:

- **Visual Studio 2022** -- Full .NET support, integrated debugger
- **JetBrains Rider** -- Cross-platform, strong C# support
- **VS Code** -- With the C# Dev Kit extension (`ms-dotnettools.csdevkit`)

### Recommended VS Code Extensions

- C# Dev Kit (`ms-dotnettools.csdevkit`)
- .NET Install Tool (`ms-dotnettools.vscode-dotnet-runtime`)
- EditorConfig (`editorconfig.editorconfig`)

---

## Initial Setup

### 1. Clone the repository

```bash
git clone <repository-url>
cd SPTQuestingBots
```

### 2. Set up the `libs/` folder

The `libs/` directory (gitignored) must contain game and framework DLLs copied from your SPT 4.x installation. Create it if it does not exist:

```bash
mkdir -p libs
```

#### Server plugin DLLs

Copy from your SPT 4.x server build output or installation:

| DLL | Description |
|-----|-------------|
| `SPTarkov.Server.Core.dll` | SPT server core library |
| `SPTarkov.DI.dll` | SPT dependency injection |
| `SPTarkov.Common.dll` | SPT shared utilities |
| `Newtonsoft.Json.dll` | JSON serialization (also needed by client) |

#### Client plugin DLLs

Copy from your SPT 4.x game installation:

**From `SPT/BepInEx/core/`:**
- `BepInEx.dll`
- `0Harmony.dll`

**From `SPT/EscapeFromTarkov_Data/Managed/`:**
- `Assembly-CSharp.dll`
- `UnityEngine.dll`
- `UnityEngine.CoreModule.dll`
- `UnityEngine.IMGUIModule.dll`
- `UnityEngine.PhysicsModule.dll`
- `UnityEngine.TextRenderingModule.dll`
- `UnityEngine.AIModule.dll`
- `UnityEngine.UI.dll`
- `Comfort.dll`
- `Comfort.Unity.dll`
- `CommonExtensions.dll`
- `DissonanceVoip.dll`
- `ItemComponent.Types.dll`
- `Newtonsoft.Json.dll`
- `Sirenix.Serialization.dll`
- `Unity.Postprocessing.Runtime.dll`
- `Unity.TextMeshPro.dll`

**From `SPT/BepInEx/plugins/spt/`:**
- `spt-common.dll`
- `spt-custom.dll`
- `spt-reflection.dll`
- `spt-singleplayer.dll`

**From `SPT/BepInEx/patchers/`:**
- `spt-prepatch.dll`

**From the BigBrain mod:**
- `DrakiaXYZ-BigBrain.dll`

### 3. Restore NuGet packages

```bash
make restore
```

This downloads test-framework packages (NUnit, NSubstitute, Microsoft.NET.Test.Sdk) for the test projects.

---

## Building

### Build plugin DLLs

```bash
make build           # Both server and client plugins
make build-server    # Server plugin only
make build-client    # Client plugin only
```

Build output goes to the `build/` directory. The build requires all DLLs in `libs/` to be present.

To build in Debug configuration:

```bash
make build CONFIGURATION=Debug
```

### Build test projects

```bash
make build-tests
```

Test projects do **not** require game DLLs in `libs/`. They target `net9.0` and test pure logic only (models, services, configuration, helpers).

---

## Testing

### Run all tests

```bash
make test
```

### Run tests by project

```bash
make test-server     # Server-side tests only
make test-client     # Client-side tests only
```

### Run specific test classes

Use `dotnet test` directly with a filter:

```bash
dotnet test tests/SPTQuestingBots.Server.Tests/ --filter "FullyQualifiedName~CommonUtilsTests"
```

### Test organization

| Project | What it tests | Framework |
|---------|---------------|-----------|
| `SPTQuestingBots.Server.Tests` | Server services, configuration logic, validation | NUnit 3.x + NSubstitute |
| `SPTQuestingBots.Client.Tests` | Client models, helpers, configuration classes | NUnit 3.x + NSubstitute |

Tests use **NSubstitute** for mocking dependencies. Game-dependent code (Unity MonoBehaviours, EFT types) cannot be tested directly -- only pure logic is covered.

---

## Code Quality

### Formatting (CSharpier)

CSharpier is the auto-formatter. It enforces a consistent style with no configuration needed.

```bash
make format          # Auto-format all source and test files
make format-check    # Check formatting without making changes (CI-safe)
```

Install CSharpier as a global .NET tool:

```bash
dotnet tool install -g csharpier
```

### Linting (dotnet format)

Code style is enforced by the `.editorconfig` file using `dotnet format`:

```bash
make lint            # Check code style (no changes)
make lint-fix        # Auto-fix code style issues
```

### Key code style rules

From `.editorconfig`:

- File-scoped namespaces (`csharp_style_namespace_declarations = file_scoped`)
- Braces required (`csharp_prefer_braces = true`)
- Private/internal fields prefixed with `_` (`_camelCase`)
- Constants use `PascalCase`
- `var` preferred when type is apparent
- No expression-bodied members (methods, constructors, properties)
- System usings sorted first, placed outside namespace
- 4-space indentation, LF line endings, max 140 character lines

---

## Debugging

### Debugging the BepInEx client plugin

1. Build in Debug configuration: `make build-client CONFIGURATION=Debug`
2. Copy `build/SPTQuestingBots.dll` and its `.pdb` file to your SPT BepInEx plugins directory
3. Attach a debugger to the EFT process:
   - **Visual Studio:** Debug > Attach to Process > select `EscapeFromTarkov.exe`
   - **Rider:** Run > Attach to Process
   - **VS Code:** Use the .NET Attach launch configuration
4. Set breakpoints in the client source files

### Debugging the server plugin

1. Build in Debug configuration: `make build-server CONFIGURATION=Debug`
2. Copy `build/SPTQuestingBots.Server.dll` and its `.pdb` to the SPT server mods directory
3. Attach a debugger to the SPT server process

### Logging

- **Client logging:** `LoggingController.LogInfo/LogWarning/LogError` writes to the BepInEx console and log file (`BepInEx/LogOutput.log`)
- **Server logging:** Uses SPT's `ILogger` via `CommonUtils` with `[Questing Bots]` prefix
- **Debug mode:** Set `debug.enabled = true` in `config.json` to enable additional logging and visual debug features (zone outlines, failed paths, door interaction points)

---

## Deployment

### Packaging for distribution

After building in Release mode:

1. **Server plugin:** Copy `build/SPTQuestingBots.Server.dll` and `config/` files into:
   ```
   user/mods/DanW-SPTQuestingBots-1.0.0/
   ├── SPTQuestingBots.Server.dll
   └── config/
       ├── config.json
       ├── eftQuestSettings.json
       └── zoneAndItemQuestPositions.json
   ```

2. **Client plugin:** Copy `build/SPTQuestingBots.dll` and `quests/` files into:
   ```
   BepInEx/plugins/DanW-SPTQuestingBots/
   ├── SPTQuestingBots.dll
   └── quests/
       └── standard/
           └── (per-map quest files)
   ```

### File placement summary

| File | Destination |
|------|-------------|
| `SPTQuestingBots.Server.dll` | `SPT/user/mods/DanW-SPTQuestingBots-1.0.0/` |
| `config/*.json` | `SPT/user/mods/DanW-SPTQuestingBots-1.0.0/config/` |
| `SPTQuestingBots.dll` | `SPT/BepInEx/plugins/DanW-SPTQuestingBots/` |
| `quests/standard/*` | `SPT/BepInEx/plugins/DanW-SPTQuestingBots/quests/standard/` |

---

## CI Pipeline

The full CI pipeline runs with:

```bash
make ci
```

This executes the following steps in order:

1. `restore` -- Restore NuGet packages
2. `format-check` -- Verify CSharpier formatting
3. `lint` -- Verify `.editorconfig` code style
4. `build-tests` -- Compile test projects
5. `test` -- Run all unit tests

All steps must pass. The CI pipeline does **not** require game DLLs since it only builds and runs the test projects (which target `net9.0` and test pure logic).

---

## Reference Mods

The following open-source SPT mods are useful pattern references when working on QuestingBots:

| Mod | What to reference |
|-----|-------------------|
| **SPT-BigBrain** (DrakiaXYZ) | `CustomLayer` / `CustomLogic` API, `BrainManager.AddCustomLayer()` registration pattern |
| **SPT-Waypoints** (DrakiaXYZ) | NavMesh data generation and bot navigation patterns |
| **Phobos** (Janky) | BigBrain layer implementation examples (squad behaviors, zone-based actions) |
| **SAIN** (Solarint) | Complex BigBrain integration, bot brain management, external-mod interop via reflection |
| **Master-Tool** | SPT 4.x server plugin patterns (`[Injectable]` DI, `ModulePatch` Harmony patches) |

See [docs/architecture.md](architecture.md#reference-mods) for details on how each is used.
