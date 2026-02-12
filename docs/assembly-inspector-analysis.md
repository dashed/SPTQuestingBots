# AssemblyInspector: Design Analysis

## Executive Summary

SPTQuestingBots uses reflection to access obfuscated fields in the game's `Assembly-CSharp.dll`. When the game updates, the deobfuscator may rename these fields (e.g., `float_2` becomes `float_3`), silently breaking the mod at runtime. We currently have a `ReflectionHelper.KnownFields` registry and test suite that catches some of these issues, but validation only runs during `make ci-full` (requires game DLLs) and lacks type-signature checking.

**AssemblyInspector** is a standalone CLI tool that uses Mono.Cecil to inspect .NET assemblies. It provides two commands:
- **`inspect`**: Enumerate all fields of a type (or types matching a pattern) with their resolved type signatures
- **`validate`**: Check our `KnownFields` registry against a game DLL, verifying both field existence and type signatures

This gives mod maintainers immediate feedback after a game update, before even attempting a build.

## dnSpy Architecture Analysis

### Library Choice: dnlib

dnSpy uses **dnlib** v4.5.0, a .NET metadata reading library by the same author (de4dot). It is NOT Mono.Cecil and NOT System.Reflection.Metadata. dnlib provides a rich object model:

- `ModuleDefMD.Load(filename)` — loads a .NET assembly from disk
- `ModuleDef.Types` — all top-level type definitions
- `TypeDef.Fields` / `.Methods` / `.Properties` / `.Events` / `.NestedTypes`
- `FieldDef.FieldSig.Type` — the field's type as a `TypeSig` (polymorphic)
- `GenericInstSig` — generic instantiations like `List<Player>`

### Key Architecture Patterns

**Assembly Loading** (`DsDocument.cs:169`):
```csharp
ModuleDef module = ModuleDefMD.Load(filename, moduleContext);
module.EnableTypeDefFindCache = true; // Performance optimization
```

**Type Enumeration** (`TypeNodeImpl.cs:42-58`):
```csharp
// dnSpy creates tree nodes by iterating TypeDef members
foreach (var f in TypeDef.Fields)
    yield return new FieldNodeImpl(treeNodeGroup, f);
foreach (var t in TypeDef.NestedTypes)
    yield return new TypeNodeImpl(treeNodeGroup, t);
```

**Field Display** (`NodeFormatter.cs:241-251`):
```csharp
// Writes: fieldName : fieldType
output.Write(color, NameUtilities.CleanIdentifier(field.Name));
output.Write(BoxedTextColor.Punctuation, ":");
if (field.FieldSig?.Type.ToTypeDefOrRef() is ITypeDefOrRef fieldType)
    decompiler.WriteType(output, fieldType, false, field as IHasCustomAttribute);
```

**Generic Type Resolution** (`CSharpFormatter.cs:1059-1067`):
```csharp
case ElementType.GenericInst:
    var gis = (GenericInstSig?)type;
    if (TypeFormatterUtils.IsSystemNullable(gis)) {
        // Write inner type + "?"
    }
    // Otherwise write: TypeName<Arg1, Arg2, ...>
```

### Obfuscated Name Handling

dnSpy displays obfuscated names as-is. It does not attempt de-obfuscation. The `TypeFormatterUtils.FilterName()` method sanitizes control characters but preserves names like `GClass1234`, `Vector3_0`, `float_2`, etc. This is exactly what we need — we want to see the names as the deobfuscator produced them.

## ILSpy / ICSharpCode.Decompiler Analysis

### Architecture Overview

ILSpy uses **ICSharpCode.Decompiler**, which is built on **System.Reflection.Metadata (SRM)** — the low-level .NET metadata reader shipped with the runtime. Unlike Mono.Cecil, which provides a rich object model directly, ICSharpCode.Decompiler builds its own full type system abstraction on top of SRM:

- **PEFile** (`ICSharpCode.Decompiler/Metadata/PEFile.cs:34-105`) wraps `PEReader`/`MetadataReader` from SRM
- **MetadataModule** (`ICSharpCode.Decompiler/TypeSystem/MetadataModule.cs:38-1050`) bridges SRM handles to `ITypeDefinition`/`IField`/`IMethod` interfaces
- **MetadataField** (`ICSharpCode.Decompiler/TypeSystem/Implementation/MetadataField.cs:35-321`) lazily decodes field signatures from raw SRM blob data

The type system is fully lazy-initialized using a `LazyInit.VolatileRead`/`GetOrSet` pattern throughout. For example, `MetadataModule` pre-allocates arrays indexed by row number for all entity types (`MetadataModule.cs:97-104`) and fills them on-demand:

```csharp
// MetadataModule.cs:236-250
public IField GetDefinition(FieldDefinitionHandle handle)
{
    if (handle.IsNil) return null;
    int row = MetadataTokens.GetRowNumber(handle);
    var field = LazyInit.VolatileRead(ref fieldDefs[row]);
    if (field != null) return field;
    field = new MetadataField(this, handle);
    return LazyInit.GetOrSet(ref fieldDefs[row], field);
}
```

Field types are decoded lazily in `MetadataField.DecodeTypeAndVolatileFlag()` (`MetadataField.cs:224-245`), which calls `fieldDef.DecodeSignature()` with a `TypeProvider` and detects `volatile` modifier types:

```csharp
// MetadataField.cs:224-245
ty = fieldDef.DecodeSignature(module.TypeProvider,
    new GenericContext(DeclaringType?.TypeParameters));
if (ty is ModifiedType mod && mod.Modifier.Name == "IsVolatile" ...)
    Volatile.Write(ref this.isVolatile, true);
ty = ApplyAttributeTypeVisitor.ApplyAttributesToType(ty, ...);
```

This is significantly more infrastructure than Cecil provides. Cecil's `ModuleDefinition.ReadModule()` -> `TypeDefinition.Fields` -> `FieldDefinition.FieldType` gives us the same data with no setup required.

### Type Name Formatting: Three-Layer Architecture

ILSpy's type-to-string conversion uses three layers:

**Layer 1: IAmbience Interface** (`ICSharpCode.Decompiler/Output/IAmbience.cs:145-154`)
```csharp
public interface IAmbience
{
    ConversionFlags ConversionFlags { get; set; }
    string ConvertSymbol(ISymbol symbol);
    string ConvertType(IType type);
    string ConvertConstantValue(object constantValue);
    string WrapComment(string comment);
}
```

The `ConversionFlags` enum (`IAmbience.cs:26-140`) has 22 flags controlling output verbosity — from showing modifiers and accessibility to using fully qualified names and nullable specifiers.

**Layer 2: CSharpAmbience** (`ICSharpCode.Decompiler/CSharp/OutputVisitor/CSharpAmbience.cs:36-464`)

Implements `IAmbience` by delegating to `TypeSystemAstBuilder`:
```csharp
// CSharpAmbience.cs:424-433
public string ConvertType(IType type)
{
    TypeSystemAstBuilder astBuilder = CreateAstBuilder();
    AstType astType = astBuilder.ConvertType(type);
    return astType.ToString();
}
```

**Layer 3: TypeSystemAstBuilder** (`ICSharpCode.Decompiler/CSharp/Syntax/TypeSystemAstBuilder.cs:39-654+`)

The core conversion logic in `ConvertTypeHelper()` (`TypeSystemAstBuilder.cs:303-473`) handles every .NET type variant:

| Type Kind | ILSpy Handling | Location |
|---|---|---|
| Pointer (`T*`) | `.MakePointerType()` | `TypeSystemAstBuilder.cs:309` |
| Array (`T[]`, `T[,]`) | `.MakeArrayType(dimensions)` | `TypeSystemAstBuilder.cs:313` — tracks dimensionality |
| By-reference (`ref T`) | `.MakeRefType()` | `TypeSystemAstBuilder.cs:321` |
| Nullable annotation (`T?`) | `.MakeNullableType()` | `TypeSystemAstBuilder.cs:314-315, 333` |
| Tuple (`(T1, T2)`) | `TupleAstType` with element names | `TypeSystemAstBuilder.cs:337-347` |
| Function pointer (`delegate*<>`) | `FunctionPointerAstType` | `TypeSystemAstBuilder.cs:349-416` |
| `Nullable<T>` value type | Converts to `T?` | `TypeSystemAstBuilder.cs:446-449` |
| `nint`/`nuint` | `PrimitiveType(type.Name)` | `TypeSystemAstBuilder.cs:455-458` |
| Built-in types | Via `KnownTypeReference.GetCSharpNameByTypeCode` | `TypeSystemAstBuilder.cs:481-488` |
| Nested types | Recursive with `DeclaringType` | `TypeSystemAstBuilder.cs:545-556` |
| Generic types | `AddTypeArguments()` | `TypeSystemAstBuilder.cs:624-638` |

The built-in type mapping (`KnownTypeReference.cs:306-345`) maps 17 type codes to C# keywords — the same set our `MapBuiltinType()` covers.

### Comparison to Our FormatTypeName

Our `FormatTypeName` in `tools/AssemblyInspector/InspectCommand.cs:138-167` handles:
- GenericInstanceType (generics)
- ArrayType (1D only)
- ByReferenceType (as `T&`)
- PointerType (as `T*`)
- 17 built-in type mappings

**Edge cases we are missing** (identified from ILSpy):

1. **`Nullable<T>` to `T?`**: ILSpy checks `pt.IsKnownType(KnownTypeCode.NullableOfT)` at `TypeSystemAstBuilder.cs:446` and converts to `T?`. In Cecil, `Nullable<int>` appears as a `GenericInstanceType` where `ElementType.FullName == "System.Nullable`1"`. We should detect this and format as `int?` instead of `Nullable<int>`.

2. **Multi-dimensional arrays**: ILSpy passes `((ArrayType)type).Dimensions` to `MakeArrayType()` at `TypeSystemAstBuilder.cs:313`. In Cecil, `ArrayType` has a `Rank` property. A 2D array should display as `int[,]`, not `int[]`. Our code unconditionally appends `[]`.

3. **Modifier types**: `MetadataField.DecodeTypeAndVolatileFlag()` (`MetadataField.cs:232-233`) unwraps `ModifiedType` to detect `volatile`. Cecil has `RequiredModifierType` and `OptionalModifierType` — we should unwrap these to show the underlying type.

4. **Nested type formatting**: ILSpy formats nested types as `OuterType.InnerType`. Cecil's `TypeReference.Name` for nested types is just the inner name, but `FullName` uses `/` as separator (e.g., `Outer/Inner`). We use `.Name` which is correct for our purpose, but if we ever need full names, we should handle the `/` → `.` conversion.

### Assembly Comparison Feature

ILSpy's comparison feature (`ILSpy/ViewModels/CompareViewModel.cs:49-617`) was added in 2025 and provides assembly-level diffing.

**Algorithm** (`CompareViewModel.cs:297-519`):

Step 1 — Build entity trees (`CreateEntityTree`, lines 297-429):
```csharp
// For each assembly, builds: Module → Namespaces → Types → Members
var ambience = new CSharpAmbience();
ambience.ConversionFlags = ConversionFlags.All & ~ConversionFlags.ShowDeclaringType;

// Each entity gets a signature string for comparison
var entry = new Entry {
    Signature = ambience.ConvertSymbol(entity),
    Entity = entity
};
```

Step 2 — Signature-based diff (`CalculateDiff`, lines 431-519):
```csharp
// Build dictionaries keyed by signature string
Dictionary<string, List<Entry>> leftMap = new();
Dictionary<string, List<Entry>> rightMap = new();

// Match: in both → identical or updated
// Left only → removed
// Right only → added
```

Step 3 — Recursive merge (`MergeTrees`, lines 249-295):
```csharp
// Merge matched entries recursively
// Propagate DiffKind up the tree
```

**Diff classification** (`CompareViewModel.cs:539-617`):
```csharp
public enum DiffKind
{
    None = ' ',
    Add = '+',
    Remove = '-',
    Update = '~'
}
```

The `Entry.RecursiveKind` property (`CompareViewModel.cs:544-577`) computes a composite kind from children — if all children are added, the parent is Add; if mixed, it is Update.

**JSON export** (`CompareViewModel.cs:176-247`): The comparison results can be exported as JSON with `changedTypes`, `addedTypes`, and `removedTypes` arrays.

**Relevance to our diff command**: ILSpy's approach validates that signature-based comparison works well for assembly diffing. For our tool, we would want a simpler variant focused on specific types and their fields, matching by field name rather than full signature. The `DiffKind` enum and JSON export patterns are directly reusable.

### Search Implementation

ILSpy's search (`ICSharpCode.ILSpyX/Search/AbstractSearchStrategy.cs:62-167`) supports structured keyword matching:

**Keyword operators** (`AbstractSearchStrategy.cs:83-128`):
- Default: case-insensitive substring match
- `+term`: must contain (same as default)
- `-term`: must NOT contain
- `=term`: exact match (ignoring generic backtick suffix)
- `~term`: non-contiguous (subsequence) match

**Non-contiguous match** (`AbstractSearchStrategy.cs:130-160`):
```csharp
// Matches if all characters of searchTerm appear in order in text
// e.g., "~bsp" matches "BotSpawner"
bool IsNoncontiguousMatch(string text, string searchTerm)
{
    var i = 0;
    for (int searchIndex = 0; searchIndex < searchTerm.Length;)
    {
        while (i != textLength)
        {
            if (text[i] == searchTerm[searchIndex])
            {
                if (searchTerm.Length == ++searchIndex) return true;
                i++; break;
            }
            i++;
        }
        if (i == textLength) return false;
    }
    return false;
}
```

**Fitness scoring** (`ILSpy/Search/SearchResultFactory.cs:40-61`):
- Compiler-generated names (starting with `<`) score 0
- Score = `1.0f / text.Length` (shorter names rank higher)
- Constructors use declaring type name for scoring

**Member search** (`MemberSearchStrategy.cs:38-121`) iterates metadata handles by kind (types, methods, fields, properties, events) and matches against the language-specific entity name.

**Relevance to our tool**: The non-contiguous match algorithm could improve our `SuggestTypes` method in `InspectCommand.cs:60-84`, which currently uses simple substring matching. Adding subsequence matching would help find types when only partial names are remembered.

### Key Patterns Worth Adopting

1. **ConversionFlags-style configurability**: ILSpy's `ConversionFlags` enum allows precise control over output formatting. Our tool could benefit from similar options (e.g., `--show-namespace`, `--show-accessibility`, `--fully-qualified`).

2. **Lazy entity resolution**: ILSpy's `LazyInit` pattern for on-demand type resolution is efficient for large assemblies. Not critical for our tool (we load one assembly at a time), but good practice.

3. **Signature-based comparison**: Using formatted string signatures as comparison keys is simple and effective. No need for structural comparison.

4. **JSON export**: For CI integration and tooling, JSON output of comparison results is practical.

### Recommendations

**Should we switch from Cecil to ICSharpCode.Decompiler?** No. ICSharpCode.Decompiler builds an entire type system and compilation context on top of SRM to support full decompilation. For our needs (enumerate fields, format types, compare field lists), this is massive overkill. Cecil provides the right level of abstraction — rich enough for type resolution but without the decompiler infrastructure. Additionally, Cecil is the standard in the BepInEx/Harmony modding ecosystem.

**Patterns to port from ILSpy:**

| Pattern | Source | Benefit | Priority |
|---|---|---|---|
| `Nullable<T>` → `T?` formatting | `TypeSystemAstBuilder.cs:446-449` | Correct display of nullable value types | High |
| Multi-dimensional array support | `TypeSystemAstBuilder.cs:313` | Correct display of `T[,]`, `T[,,]` | High |
| Non-contiguous (fuzzy) matching | `AbstractSearchStrategy.cs:130-160` | Better type suggestions in inspect command | Medium |
| DiffKind enum + JSON export | `CompareViewModel.cs:539-617` | Foundation for future diff command | Medium |
| Modifier type unwrapping | `MetadataField.cs:232-233` | Handle volatile/pinned fields correctly | Low |

**Should we add a diff command?** Yes, as a future enhancement. ILSpy's approach confirms that signature-based comparison is sufficient. Our version would be simpler — focused on field-level changes for specific types rather than whole-assembly tree diffing. Target design:
```
AssemblyInspector diff --old old/Assembly-CSharp.dll --new Assembly-CSharp.dll [--types T1,T2]
```

## Library Comparison

| Criteria | System.Reflection.Metadata | Mono.Cecil | dnlib | ICSharpCode.Decompiler |
|---|---|---|---|---|
| **License** | MIT | MIT | GPL v3 | MIT |
| **Type model** | Raw metadata handles | Rich object model | Rich object model | Full decompilation |
| **Field enumeration** | Manual handle iteration | `TypeDefinition.Fields` | `TypeDef.Fields` | `MetadataModule.GetDefinition()` via SRM |
| **Generic type resolution** | Manual blob parsing | `GenericInstanceType` | `GenericInstSig` | Automatic |
| **Readable type names** | Must build manually | `.FullName` / `.Name` | `.FullName` / `.Name` | Full C# syntax |
| **Package size** | Built into .NET SDK | ~300 KB NuGet | ~500 KB NuGet | ~2 MB NuGet |
| **Dependency count** | 0 | 0 | 0 | Several |
| **Ecosystem fit** | .NET standard | BepInEx/Harmony ecosystem | dnSpy ecosystem | ILSpy ecosystem |

### Recommendation: Mono.Cecil

**Mono.Cecil** is the right choice for our tool:

1. **MIT license** — compatible with our project, unlike dnlib's GPL v3
2. **Rich enough** — `TypeDefinition.Fields` gives us `FieldDefinition` objects with `.Name`, `.FieldType` (fully resolved), `.IsPublic`, `.IsStatic`, etc.
3. **Generic type formatting built-in** — `GenericInstanceType.FullName` produces `System.Collections.Generic.List`1<EFT.Player>` automatically
4. **Ecosystem alignment** — BepInEx, Harmony, and most Unity modding tools use Cecil
5. **Already chosen** — the project scaffold uses `Mono.Cecil 0.11.6`

System.Reflection.Metadata is too low-level (we already hit its limits in `ReflectionValidationTests.cs` where we can't resolve base class fields). ICSharpCode.Decompiler is overkill.

## Our Current Infrastructure

### ReflectionHelper.cs

Central registry at `src/SPTQuestingBots.Client/Helpers/ReflectionHelper.cs`:

```csharp
private static readonly (Type Type, string FieldName, string Context)[] KnownFields = new[]
{
    // AccessTools.Field lookups (4 entries)
    (typeof(BotCurrentPathAbstractClass), "Vector3_0", "BotPathingHelpers — path corner points"),
    (typeof(NonWavesSpawnScenario), "float_2", "TrySpawnFreeAndDelayPatch — retry time delay"),
    (typeof(LocalGame), "wavesSpawnScenario_0", "GameStartPatch — waves spawn scenario"),
    (typeof(BotsGroup), "<BotZone>k__BackingField", "GoToPositionAbstractAction — bot zone"),
    // Harmony ___param field injections (6 entries)
    (typeof(BossGroup), "Boss_1", "SetNewBossPatch ___Boss_1"),
    (typeof(BotSpawner), "Bots", "BotDiedPatch ___Bots"),
    (typeof(BotSpawner), "OnBotRemoved", "BotDiedPatch ___OnBotRemoved"),
    (typeof(BotSpawner), "AllPlayers", "GetAllBossPlayersPatch ___AllPlayers"),
    (typeof(AirdropLogicClass), "AirdropSynchronizableObject_0", "AirdropLandPatch"),
    (typeof(LighthouseTraderZone), "physicsTriggerHandler_0", "LighthouseTraderZone patches"),
};
```

Key methods:
- `RequireField(type, fieldName, context)` — drop-in for `AccessTools.Field()` with error logging
- `ValidateAllReflectionFields()` — validates all entries at plugin startup (runtime only)

### Existing Test Coverage

**ReflectionValidationTests.cs** — 4 tests:

| Test | What it validates | When it runs |
|---|---|---|
| `AllAccessToolsFieldCalls_ShouldUseRequireField` | No raw `AccessTools.Field()` calls outside ReflectionHelper | `make ci` (source scan) |
| `AllHarmonyFieldInjections_AreDocumentedInReflectionHelper` | All `___param` Harmony injections are registered in KnownFields | `make ci` (source scan) |
| `KnownFields_MatchGameAssemblyMetadata` | Field names exist in Assembly-CSharp.dll | `make ci-full` only (needs DLL) |
| `KnownFields_HasExpectedMinimumEntryCount` | Registry has >= 10 entries | `make ci` (sanity check) |

**BugFixRegressionTests.cs** — 18+ tests that verify specific field name strings exist in specific source files (e.g., `"float_2"` in TrySpawnFreeAndDelayPatch.cs).

### Dynamic Lookups (Not in KnownFields)

Two `RequireField` calls use runtime-resolved types and are intentionally excluded from KnownFields:

1. `LogicLayerMonitor.cs:176` — `AICoreStrategyAbstractClass<BotLogicDecision>` type, field `"List_0"` (brain layer list)
2. `LogicLayerMonitor.cs:261` — `BigBrain.Internal.CustomLayerWrapper` type, field `"customLayer"` (BigBrain internal, not in Assembly-CSharp.dll)

### Identified Gaps

1. **No field type validation**: We verify field names exist but not their types. If the deobfuscator renames `List_0` to `List_1` and both exist with different generic parameters, we'd pick the wrong one.
2. **No base class resolution**: `PScavProfilePatch` uses `typeof(BotsPresets).BaseType` — the SRM-based test skips these because it can't resolve inheritance from raw metadata.
3. **No proactive discovery**: The DLL validation test only runs during `make ci-full`. A standalone CLI tool could run immediately after copying new DLLs.
4. **No cross-version diffing**: We can't compare two versions of Assembly-CSharp.dll to see what fields changed on types we care about.
5. **No field type display in validation output**: When validation fails, we show available field names but not their types, making it harder to find the correct replacement.

## Tool Design Specification

### Commands

#### `inspect <TypeName> [options]`

List all fields of a type in the assembly. Supports partial/pattern matching.

```
$ AssemblyInspector inspect BotSpawner --dll libs/Assembly-CSharp.dll

Type: EFT.BotSpawner
  Bots                : BotsClass                          (private)
  OnBotRemoved        : Action<BotOwner>                   (private)
  AllPlayers          : List<Player>                       (private)
  float_0             : float                              (private)
  ...
```

Options:
- `--dll <path>` — path to Assembly-CSharp.dll (default: `libs/Assembly-CSharp.dll`)
- `--format <table|json>` — output format (default: `table`)
- `--include-inherited` — show fields from base types too

#### `validate [options]`

Parse `ReflectionHelper.KnownFields` from source and validate against the DLL.

```
$ AssemblyInspector validate

Validating 10 entries from ReflectionHelper.KnownFields...

  OK  BotCurrentPathAbstractClass.Vector3_0    (Vector3[])
  OK  NonWavesSpawnScenario.float_2            (float)
  OK  LocalGame.wavesSpawnScenario_0           (WavesSpawnScenario)
  OK  BotsGroup.<BotZone>k__BackingField       (BotZone)
  OK  BossGroup.Boss_1                         (BotOwner)
  OK  BotSpawner.Bots                          (BotsClass)
  OK  BotSpawner.OnBotRemoved                  (Action<BotOwner>)
  OK  BotSpawner.AllPlayers                    (List<Player>)
  OK  AirdropLogicClass.AirdropSynchronizableObject_0  (AirdropSynchronizableObject)
  OK  LighthouseTraderZone.physicsTriggerHandler_0     (PhysicsTriggerHandler)

Result: 10/10 passed
```

On failure:
```
FAIL  NonWavesSpawnScenario.float_2 — field not found
      Available fields: float_0 (float), float_1 (float), float_3 (float), ...
      Suggestion: float_3 looks like a likely replacement (same type: float)
```

Options:
- `--dll <path>` — path to Assembly-CSharp.dll (default: `libs/Assembly-CSharp.dll`)
- `--source <path>` — path to ReflectionHelper.cs (default: `src/SPTQuestingBots.Client/Helpers/ReflectionHelper.cs`)
- `--format <table|json>` — output format

### Exit Codes

- `0` — success (inspect completed, or validate passed)
- `1` — usage error or file not found
- `2` — validation failures detected

## Recommended Implementation Approach

### Cecil API Usage for inspect

```csharp
using Mono.Cecil;

var module = ModuleDefinition.ReadModule(dllPath);

// Find type by name (supports partial matching)
var types = module.Types
    .SelectMany(t => FlattenNestedTypes(t))
    .Where(t => t.Name == typeName || t.FullName.Contains(typeName));

foreach (var type in types)
{
    Console.WriteLine($"Type: {type.FullName}");
    foreach (var field in type.Fields)
    {
        string fieldTypeName = FormatTypeName(field.FieldType);
        string access = field.IsPublic ? "public" : "private";
        Console.WriteLine($"  {field.Name,-30} : {fieldTypeName,-35} ({access})");
    }
}
```

### Cecil API Usage for validate

```csharp
// Parse KnownFields from source using regex (same pattern as ReflectionValidationTests.cs)
var entryPattern = new Regex(
    @"typeof\((?:[\w.]+\.)?(\w+)\)(?:\.BaseType)?,\s*""([^""]+)"",\s*""([^""]+)""");

var module = ModuleDefinition.ReadModule(dllPath);

// Build lookup: typeName → TypeDefinition (including nested types)
var typeMap = module.Types
    .SelectMany(t => FlattenNestedTypes(t))
    .GroupBy(t => t.Name)
    .ToDictionary(g => g.Key, g => g.ToList());

foreach (var (typeName, fieldName, context) in knownFields)
{
    if (!typeMap.TryGetValue(typeName, out var matchingTypes))
    {
        // Type not found — may be base-class resolved
        continue;
    }

    var type = matchingTypes.First();
    var field = type.Fields.FirstOrDefault(f => f.Name == fieldName);

    if (field == null)
    {
        // FAIL: field not found
        // List available fields with their types for diagnosis
        var available = type.Fields.Select(f => $"{f.Name} ({FormatTypeName(f.FieldType)})");
        ReportFailure(typeName, fieldName, context, available);
    }
    else
    {
        // OK: show field type for confirmation
        ReportSuccess(typeName, fieldName, FormatTypeName(field.FieldType));
    }
}
```

### Type Name Formatting

```csharp
static string FormatTypeName(TypeReference typeRef)
{
    if (typeRef is GenericInstanceType git)
    {
        string baseName = git.ElementType.Name;
        // Remove backtick suffix: "List`1" → "List"
        int tick = baseName.IndexOf('`');
        if (tick >= 0) baseName = baseName[..tick];

        string args = string.Join(", ", git.GenericArguments.Select(FormatTypeName));
        return $"{baseName}<{args}>";
    }

    if (typeRef is ArrayType arr)
        return FormatTypeName(arr.ElementType) + "[]";

    if (typeRef is ByReferenceType byRef)
        return "ref " + FormatTypeName(byRef.ElementType);

    // Use short names for primitives
    return typeRef.FullName switch
    {
        "System.Void" => "void",
        "System.Boolean" => "bool",
        "System.Int32" => "int",
        "System.Int64" => "long",
        "System.Single" => "float",
        "System.Double" => "double",
        "System.String" => "string",
        "System.Object" => "object",
        "System.Byte" => "byte",
        _ => typeRef.Name
    };
}
```

### Flattening Nested Types

```csharp
static IEnumerable<TypeDefinition> FlattenNestedTypes(TypeDefinition type)
{
    yield return type;
    foreach (var nested in type.NestedTypes)
    {
        foreach (var inner in FlattenNestedTypes(nested))
            yield return inner;
    }
}
```

## Integration Plan

### Makefile Targets

```makefile
# Build the AssemblyInspector CLI tool
build-inspector:
	$(DOTNET) build tools/AssemblyInspector/AssemblyInspector.csproj -c Release

# Inspect a specific type
inspect:
	$(DOTNET) run --project tools/AssemblyInspector -- inspect $(TYPE) --dll libs/Assembly-CSharp.dll

# Validate KnownFields against the DLL
validate-fields:
	$(DOTNET) run --project tools/AssemblyInspector -- validate
```

### CI Integration

- `make ci` — unchanged (format-check + client tests, no DLLs needed)
- `make ci-full` — add `validate-fields` after existing tests
- `validate-fields` is a fast standalone check (no build of main project needed)

### Relationship to Existing Tests

The AssemblyInspector `validate` command complements, not replaces, the existing tests:

| Layer | Tool | What it checks | When |
|---|---|---|---|
| Source scanning | `ReflectionValidationTests` | All field lookups use RequireField, all ___params registered | `make ci` |
| DLL metadata | `ReflectionValidationTests` (SRM) | Field names exist in DLL | `make ci-full` |
| DLL metadata + types | AssemblyInspector `validate` | Field names exist AND shows type signatures | `make validate-fields` |
| Interactive exploration | AssemblyInspector `inspect` | Browse type fields after game update | Manual |
| Runtime | `ReflectionHelper.ValidateAllReflectionFields()` | Fields resolve on actual game types | Plugin startup |

The key improvement: AssemblyInspector shows **field types** alongside names, making it much easier to find the correct replacement when a field is renamed. The existing SRM-based test only checks existence.

### Workflow After Game Update

1. Copy new DLLs: `make copy-libs SPT_DIR=/path/to/new/spt`
2. Run validation: `make validate-fields`
3. If failures, inspect the affected types: `make inspect TYPE=NonWavesSpawnScenario`
4. Update field names in `ReflectionHelper.KnownFields` and patch files
5. Run full CI: `make ci-full`
