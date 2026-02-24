using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Patches;

/// <summary>
/// Source-scanning tests that verify Harmony patch files follow correct patterns.
/// These tests read the raw C# source files and check for common patch mistakes
/// without requiring game assemblies.
/// </summary>
[TestFixture]
public class HarmonyPatchCorrectnessTests
{
    private static readonly string PatchesRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "SPTQuestingBots.Client", "Patches")
    );

    private string[] _allPatchFiles;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Assert.That(Directory.Exists(PatchesRoot), Is.True, $"Patches directory not found at {PatchesRoot}");
        _allPatchFiles = Directory.GetFiles(PatchesRoot, "*.cs", SearchOption.AllDirectories);
        Assert.That(_allPatchFiles.Length, Is.GreaterThan(30), "Expected at least 30 patch files");
    }

    [Test]
    public void AllPatchFiles_ExtendModulePatch()
    {
        // Every patch class should extend ModulePatch (or a class that extends it)
        var failures = new List<string>();

        foreach (var file in _allPatchFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            // Skip abstract base classes
            if (content.Contains("abstract class"))
                continue;

            // Check for ModulePatch inheritance (direct or via abstract base)
            bool extendsModulePatch = Regex.IsMatch(content, @"class\s+\w+\s*:\s*\w*ModulePatch");
            bool extendsAbstractBase = Regex.IsMatch(content, @"class\s+\w+\s*:\s*Abstract\w+Patch");

            if (!extendsModulePatch && !extendsAbstractBase)
            {
                failures.Add(fileName);
            }
        }

        Assert.That(failures, Is.Empty, "Patch files not extending ModulePatch: " + string.Join(", ", failures));
    }

    [Test]
    public void AllPatchFiles_HaveGetTargetMethod()
    {
        // Every concrete patch class must override GetTargetMethod
        var failures = new List<string>();

        foreach (var file in _allPatchFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            // Skip abstract base classes
            if (content.Contains("abstract class"))
                continue;

            if (!content.Contains("GetTargetMethod"))
            {
                failures.Add(fileName);
            }
        }

        Assert.That(failures, Is.Empty, "Patch files missing GetTargetMethod: " + string.Join(", ", failures));
    }

    [Test]
    public void AllPatchFiles_HaveAtLeastOnePatchAttribute()
    {
        // Every concrete patch must have [PatchPrefix], [PatchPostfix], or [PatchTranspiler]
        var failures = new List<string>();
        var patchAttributes = new[] { "[PatchPrefix]", "[PatchPostfix]", "[PatchTranspiler]" };

        foreach (var file in _allPatchFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            if (content.Contains("abstract class"))
                continue;

            if (!patchAttributes.Any(attr => content.Contains(attr)))
            {
                failures.Add(fileName);
            }
        }

        Assert.That(failures, Is.Empty, "Patch files without any patch attribute: " + string.Join(", ", failures));
    }

    [Test]
    public void PrefixPatches_ReturningBool_HaveExplicitReturn()
    {
        // Prefix patches with bool return type should have explicit return statements
        var failures = new List<string>();

        foreach (var file in _allPatchFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            // Find prefix methods that return bool
            var prefixMatches = Regex.Matches(content, @"\[PatchPrefix\]\s+protected\s+static\s+bool\s+PatchPrefix\s*\(");

            if (prefixMatches.Count == 0)
                continue;

            // Extract the method body and check for return statements
            // A bool-returning prefix without "return" would be a compiler error,
            // but we check for "return true" or "return false" specifically
            if (!content.Contains("return true") && !content.Contains("return false"))
            {
                failures.Add(fileName);
            }
        }

        Assert.That(failures, Is.Empty, "Bool prefix patches without explicit true/false return: " + string.Join(", ", failures));
    }

    [Test]
    public void PrefixPatches_ModifyingResult_UseRefKeyword()
    {
        // Prefix patches that set __result must declare it with ref
        var failures = new List<string>();

        foreach (var file in _allPatchFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            // Check for prefix methods
            if (!content.Contains("[PatchPrefix]"))
                continue;

            // If __result is assigned (not just read), it must be ref
            bool assignsResult = Regex.IsMatch(content, @"__result\s*=\s*");
            bool hasRefResult = content.Contains("ref ") && content.Contains("__result");

            if (assignsResult && !hasRefResult)
            {
                failures.Add(fileName);
            }
        }

        Assert.That(failures, Is.Empty, "Prefix patches assigning __result without ref: " + string.Join(", ", failures));
    }

    [Test]
    public void PatchMethods_AreStatic()
    {
        // All Harmony patch methods must be static
        var failures = new List<string>();

        foreach (var file in _allPatchFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            // Check for non-static patch methods
            bool hasNonStaticPatch = Regex.IsMatch(content, @"\[Patch(Prefix|Postfix|Transpiler)\]\s+protected\s+(?!static)");

            if (hasNonStaticPatch)
            {
                failures.Add(fileName);
            }
        }

        Assert.That(failures, Is.Empty, "Patch files with non-static patch methods: " + string.Join(", ", failures));
    }

    [Test]
    public void FieldAccessParameters_UseTripleUnderscore()
    {
        // Harmony field access parameters must use ___ prefix (triple underscore)
        // This catches typos like __fieldName (double underscore, which means something else)
        var failures = new List<string>();

        foreach (var file in _allPatchFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            // Find all parameter declarations in patch methods that look like field access
            // Correct: ___FieldName_0 (triple underscore)
            // Wrong: __FieldName_0 (double underscore, means something else in Harmony)
            // Exception: __instance, __result, __state are Harmony builtins

            var paramMatches = Regex.Matches(content, @"(?:Patch(?:Prefix|Postfix|Transpiler))\s*\([^)]*\b(__\w+)[^)]*\)");

            foreach (Match match in paramMatches)
            {
                string param = match.Groups[1].Value;
                // Skip known Harmony special params
                if (
                    param == "__instance"
                    || param == "__result"
                    || param == "__state"
                    || param.StartsWith("___")
                    || Regex.IsMatch(param, @"^__\d+$")
                )
                    continue;

                // This is a double-underscore param that isn't a known Harmony special
                failures.Add($"{fileName}: suspicious param '{param}'");
            }
        }

        Assert.That(failures, Is.Empty, "Suspicious double-underscore parameters: " + string.Join(", ", failures));
    }

    [Test]
    public void NoPatchFiles_UseNewRandomInline()
    {
        // Documented gotcha: new System.Random() per call gives identical results
        // for bots activating in the same millisecond
        var failures = new List<string>();

        foreach (var file in _allPatchFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            // Check for inline Random instantiation in methods (not fields)
            // Allow static field declarations like "private static readonly Random _rng = new Random()"
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                // Skip field declarations (static readonly at class level)
                if (line.Contains("static") && line.Contains("readonly") && line.Contains("Random"))
                    continue;

                // Flag method-local new Random() or new System.Random()
                if (Regex.IsMatch(line, @"new\s+(System\.)?Random\s*\(\s*\)") && !line.StartsWith("//"))
                {
                    failures.Add($"{fileName}:{i + 1}");
                }
            }
        }

        Assert.That(
            failures,
            Is.Empty,
            "Patch files with inline new Random() (use shared static instance): " + string.Join(", ", failures)
        );
    }

    [Test]
    public void GetTargetMethod_DoesNotUseUnfilteredFirst()
    {
        // Using .GetMethods().First() without a predicate is fragile because
        // .NET reflection does not guarantee method ordering
        var failures = new List<string>();

        foreach (var file in _allPatchFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            // Check for GetMethods(...).First() without a lambda filter
            if (Regex.IsMatch(content, @"\.GetMethods\([^)]*\)\s*\.First\(\s*\)"))
            {
                failures.Add(fileName);
            }
        }

        Assert.That(failures, Is.Empty, "Patch files using unfiltered .First() on GetMethods (fragile): " + string.Join(", ", failures));
    }

    [Test]
    public void AllPatchFiles_HaveCorrectNamespace()
    {
        // Patch files should be in SPTQuestingBots.Patches namespace (or a sub-namespace)
        var failures = new List<string>();

        foreach (var file in _allPatchFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            if (!Regex.IsMatch(content, @"namespace\s+SPTQuestingBots\.Patches"))
            {
                failures.Add(fileName);
            }
        }

        Assert.That(failures, Is.Empty, "Patch files with incorrect namespace: " + string.Join(", ", failures));
    }

    [Test]
    public void AllPatchFiles_ImportSPTReflectionPatching()
    {
        // Every patch file should import SPT.Reflection.Patching for ModulePatch
        var failures = new List<string>();

        foreach (var file in _allPatchFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            // Skip abstract base classes that might not need the import
            if (content.Contains("abstract class") && !content.Contains("ModulePatch"))
                continue;

            if (!content.Contains("using SPT.Reflection.Patching;"))
            {
                failures.Add(fileName);
            }
        }

        Assert.That(failures, Is.Empty, "Patch files missing SPT.Reflection.Patching import: " + string.Join(", ", failures));
    }

    [Test]
    public void PrefixPatches_ThatAlwaysReturnFalse_AreIntentional()
    {
        // A prefix that always returns false unconditionally skips the original method.
        // This test checks that such prefixes are intentional (i.e., they set __result).
        var warnings = new List<string>();

        foreach (var file in _allPatchFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            if (!content.Contains("[PatchPrefix]"))
                continue;

            // Find prefix methods that return bool
            if (!Regex.IsMatch(content, @"static\s+bool\s+PatchPrefix"))
                continue;

            // Check if it has "return true" anywhere — if not, it always skips original
            if (!content.Contains("return true"))
            {
                // This prefix always returns false — verify it sets __result
                if (!content.Contains("__result"))
                {
                    warnings.Add($"{fileName}: prefix always skips original (returns false) without setting __result");
                }
            }
        }

        Assert.That(
            warnings,
            Is.Empty,
            "Prefix patches that always skip original without setting __result: " + string.Join("; ", warnings)
        );
    }

    [Test]
    public void PatchFileCount_MatchesExpected()
    {
        // Track the number of patch files to detect accidental additions/removals
        Assert.That(_allPatchFiles.Length, Is.EqualTo(40), "Patch file count changed — update this test if intentional");
    }

    [Test]
    public void NoDuplicateClassNames_AcrossPatchFiles()
    {
        // Two patch files in different directories can have the same class name,
        // but different namespaces. Check for truly duplicate fully-qualified names.
        var classNames = new Dictionary<string, string>();
        var duplicates = new List<string>();

        foreach (var file in _allPatchFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            var nsMatch = Regex.Match(content, @"namespace\s+([\w.]+)");
            var classMatch = Regex.Match(content, @"class\s+(\w+)\s*:");

            if (!nsMatch.Success || !classMatch.Success)
                continue;

            string fullName = nsMatch.Groups[1].Value + "." + classMatch.Groups[1].Value;

            if (classNames.ContainsKey(fullName))
            {
                duplicates.Add($"{fullName} in {fileName} and {classNames[fullName]}");
            }
            else
            {
                classNames[fullName] = fileName;
            }
        }

        Assert.That(duplicates, Is.Empty, "Duplicate patch class names: " + string.Join("; ", duplicates));
    }

    [TestCase("HandleFinishedTaskPatch.cs", "TasksExtensions")]
    [TestCase("ProcessSourceOcclusionPatch.cs", "SpatialAudioSystem")]
    [TestCase("BotMoverFixedUpdatePatch.cs", "BotMover")]
    [TestCase("EnableVaultPatch.cs", "Player")]
    [TestCase("MovementContextIsAIPatch.cs", "MovementContext")]
    [TestCase("AirdropLandPatch.cs", "AirdropLogicClass")]
    [TestCase("BotOwnerBrainActivatePatch.cs", "BotOwner")]
    [TestCase("BotOwnerSprintPatch.cs", "BotOwner")]
    [TestCase("CheckLookEnemyPatch.cs", "EnemyInfo")]
    [TestCase("OnBeenKilledByAggressorPatch.cs", "Player")]
    [TestCase("OnMakingShotPatch.cs", "Player")]
    [TestCase("ReturnToPoolPatch.cs", "AssetPoolObject")]
    [TestCase("ShrinkDoorNavMeshCarversPatch.cs", "GameWorld")]
    [TestCase("TarkovInitPatch.cs", "TarkovApplication")]
    [TestCase("MenuShowPatch.cs", "MenuScreen")]
    [TestCase("IsFollowerSuitableForBossPatch.cs", "BotBoss")]
    public void KnownPatch_TargetsExpectedType(string fileName, string expectedTargetType)
    {
        var file = _allPatchFiles.FirstOrDefault(f => Path.GetFileName(f) == fileName);
        Assert.That(file, Is.Not.Null, $"Patch file {fileName} not found");

        string content = File.ReadAllText(file);
        Assert.That(content.Contains($"typeof({expectedTargetType})"), Is.True, $"{fileName} should target {expectedTargetType}");
    }
}
