using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Helpers;

/// <summary>
/// Source-scanning tests that validate reflection and Harmony patterns
/// for consistency, null-safety, and correct storage conventions.
///
/// Complements <see cref="ReflectionValidationTests"/> (field-name accuracy)
/// and <see cref="HelperFieldRegistrationTests"/> (KnownFields registration).
/// </summary>
[TestFixture]
public class ReflectionConsistencyTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string ClientSrcDir = Path.Combine(RepoRoot, "src", "SPTQuestingBots.Client");

    private static string FindRepoRoot()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        return TestContext.CurrentContext.TestDirectory;
    }

    #region No raw GetField calls outside ReflectionHelper

    [Test]
    public void NoRawGetFieldWithBindingFlags_OutsideReflectionHelper()
    {
        // Raw typeof(X).GetField(name, BindingFlags...) calls should use
        // ReflectionHelper.RequireField instead, so missing fields are logged.
        var csFiles = Directory.GetFiles(ClientSrcDir, "*.cs", SearchOption.AllDirectories);
        var pattern = new Regex(@"\.GetField\s*\([^)]*BindingFlags", RegexOptions.Compiled);
        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            if (file.EndsWith("ReflectionHelper.cs"))
            {
                continue;
            }

            // ConfigController uses dynamic type resolution — the target type is found
            // at runtime, so it can't use RequireField. Null-safety is handled locally.
            if (file.EndsWith("ConfigController.cs"))
            {
                continue;
            }

            string content = File.ReadAllText(file);
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (pattern.IsMatch(lines[i]))
                {
                    string relPath = Path.GetRelativePath(RepoRoot, file);
                    violations.Add(relPath + ":" + (i + 1) + " — " + lines[i].Trim());
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Found raw .GetField(BindingFlags) calls that should use ReflectionHelper.RequireField:\n  " + string.Join("\n  ", violations)
        );
    }

    #endregion

    #region RequireField results stored in static fields

    [Test]
    public void RequireFieldResults_AreStoredInStaticFields()
    {
        // RequireField should be called once and cached in a static field,
        // not called inside a method body that runs repeatedly.
        // We check that every RequireField call either:
        //   (a) appears on a line with "static" and "FieldInfo" (field declaration), OR
        //   (b) is inside a known-safe one-shot method (GetTargetMethod, constructor with guard)
        var csFiles = Directory.GetFiles(ClientSrcDir, "*.cs", SearchOption.AllDirectories);

        // Match lines containing RequireField that are NOT static field initializers
        var requireFieldPattern = new Regex(@"RequireField\s*\(", RegexOptions.Compiled);
        var staticFieldPattern = new Regex(@"static\s+(readonly\s+)?FieldInfo\s+\w+\s*=\s*.*RequireField", RegexOptions.Compiled);
        // One-shot method contexts where it's acceptable
        var oneShotMethodPattern = new Regex(@"(GetTargetMethod|protected\s+override\s+MethodBase)", RegexOptions.Compiled);
        // Constructor with static null guard: if (field == null) { field = RequireField(...) }
        var guardedAssignmentPattern = new Regex(@"\w+\s*=\s*.*RequireField", RegexOptions.Compiled);

        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            if (file.EndsWith("ReflectionHelper.cs"))
            {
                continue;
            }

            string content = File.ReadAllText(file);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (!requireFieldPattern.IsMatch(line))
                {
                    continue;
                }

                // OK: static field initializer (possibly multi-line, check prior lines too)
                string contextBlock = string.Join(" ", lines.Skip(System.Math.Max(0, i - 3)).Take(4));
                if (staticFieldPattern.IsMatch(contextBlock))
                {
                    continue;
                }

                // OK: inside GetTargetMethod (one-shot Harmony setup)
                // Look backwards for method declaration
                bool inOneShotMethod = false;
                for (int j = i - 1; j >= System.Math.Max(0, i - 20); j--)
                {
                    if (oneShotMethodPattern.IsMatch(lines[j]))
                    {
                        inOneShotMethod = true;
                        break;
                    }
                    // Stop at class declaration
                    if (Regex.IsMatch(lines[j], @"^\s*(public|private|internal)\s+(class|struct)"))
                    {
                        break;
                    }
                }
                if (inOneShotMethod)
                {
                    continue;
                }

                // OK: guarded by null check or resolved-flag check
                // Patterns: if (field == null) or if (!_fieldResolved)
                bool hasGuard = false;
                for (int j = i - 1; j >= System.Math.Max(0, i - 20); j--)
                {
                    if (Regex.IsMatch(lines[j], @"if\s*\(\s*\w+\s*==\s*null\s*\)"))
                    {
                        hasGuard = true;
                        break;
                    }
                    if (Regex.IsMatch(lines[j], @"if\s*\(\s*!\w+Resolved\s*\)"))
                    {
                        hasGuard = true;
                        break;
                    }
                }
                if (hasGuard && guardedAssignmentPattern.IsMatch(line))
                {
                    continue;
                }

                string relPath = Path.GetRelativePath(RepoRoot, file);
                violations.Add(relPath + ":" + (i + 1) + " — " + line.Trim());
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Found RequireField calls not stored in static fields or one-shot methods:\n  "
                + string.Join("\n  ", violations)
                + "\n\nRequireField results should be cached in static (readonly) fields to avoid per-call reflection."
        );
    }

    #endregion

    #region Null guards before GetValue

    [Test]
    public void GetValueCalls_HaveNullGuardsOnFieldInfo()
    {
        // Every .GetValue() call on a FieldInfo variable should be preceded by
        // a null check on that variable (or use null-conditional ?.).
        // This prevents NullReferenceException when RequireField returns null.
        var csFiles = Directory.GetFiles(ClientSrcDir, "*.cs", SearchOption.AllDirectories);

        // Match: someField.GetValue( — where someField is likely a FieldInfo
        var getValuePattern = new Regex(@"(\w+)\.GetValue\s*\(", RegexOptions.Compiled);
        // Skip patterns that are not FieldInfo (e.g., config.GetValue, dictionary.GetValue)
        var skipVarNames = new HashSet<string> { "chance", "Config", "config", "matchingProperties", "ChanceOfBeingHostileTowardBosses" };

        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            string content = File.ReadAllText(file);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                var match = getValuePattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                string varName = match.Groups[1].Value;

                // Skip non-FieldInfo GetValue calls
                if (skipVarNames.Contains(varName))
                {
                    continue;
                }

                // Skip if this file is ReflectionHelper.cs itself
                if (file.EndsWith("ReflectionHelper.cs"))
                {
                    continue;
                }

                // Skip array indexer patterns like matchingProperties[0].SetValue
                if (line.Contains("[0]."))
                {
                    continue;
                }

                // Check: is there a null guard in the preceding lines?
                bool hasGuard = false;
                string lookbackBlock = string.Join("\n", lines.Skip(System.Math.Max(0, i - 10)).Take(11));

                // Pattern 1: varName == null or varName != null check
                if (Regex.IsMatch(lookbackBlock, Regex.Escape(varName) + @"\s*[!=]=\s*null"))
                {
                    hasGuard = true;
                }

                // Pattern 2: null-conditional access (varName?.GetValue)
                if (line.Contains(varName + "?.GetValue"))
                {
                    hasGuard = true;
                }

                // Pattern 3: ternary with null check (varName != null ? ... : null)
                if (Regex.IsMatch(line, Regex.Escape(varName) + @"\s*!=\s*null\s*\?"))
                {
                    hasGuard = true;
                }

                // Pattern 4: chained from raw GetField (not stored in variable) —
                // the .GetField().GetValue() pattern is a separate issue
                if (line.Contains(".GetField("))
                {
                    // This is the ConfigController pattern — flag it
                    hasGuard = false;
                }

                if (!hasGuard)
                {
                    string relPath = Path.GetRelativePath(RepoRoot, file);
                    violations.Add(relPath + ":" + (i + 1) + " — variable '" + varName + "' — " + line.Trim());
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Found .GetValue() calls without null guards on the FieldInfo variable:\n  "
                + string.Join("\n  ", violations)
                + "\n\nAdd a null check (if field == null return/skip) before calling .GetValue()."
        );
    }

    #endregion

    #region SetValue calls have null guards

    [Test]
    public void SetValueCalls_HaveNullGuardsOnFieldInfo()
    {
        // Every .SetValue() call on a FieldInfo variable should be preceded by
        // a null check or use null-conditional ?.SetValue.
        var csFiles = Directory.GetFiles(ClientSrcDir, "*.cs", SearchOption.AllDirectories);

        var setValuePattern = new Regex(@"(\w+)(\.|\?\.)SetValue\s*\(", RegexOptions.Compiled);
        // Skip non-FieldInfo SetValue calls
        var skipVarNames = new HashSet<string> { "matchingProperties" };

        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            string content = File.ReadAllText(file);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                var match = setValuePattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                string varName = match.Groups[1].Value;
                string accessor = match.Groups[2].Value;

                if (skipVarNames.Contains(varName))
                {
                    continue;
                }

                // Skip array indexer patterns
                if (line.Contains("[0]."))
                {
                    continue;
                }

                // Null-conditional is a valid guard
                if (accessor == "?.")
                {
                    continue;
                }

                // Check preceding lines for null guard
                bool hasGuard = false;
                string lookbackBlock = string.Join("\n", lines.Skip(System.Math.Max(0, i - 10)).Take(11));

                if (Regex.IsMatch(lookbackBlock, Regex.Escape(varName) + @"\s*[!=]=\s*null"))
                {
                    hasGuard = true;
                }

                if (!hasGuard)
                {
                    string relPath = Path.GetRelativePath(RepoRoot, file);
                    violations.Add(relPath + ":" + (i + 1) + " — variable '" + varName + "' — " + line.Trim());
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Found .SetValue() calls without null guards on the FieldInfo variable:\n  "
                + string.Join("\n  ", violations)
                + "\n\nAdd a null check or use ?.SetValue() to guard against RequireField returning null."
        );
    }

    #endregion

    #region Backing field naming convention

    [Test]
    public void BackingFieldStrings_FollowNamingConvention()
    {
        // All backing field strings should follow the C# compiler convention:
        // <PropertyName>k__BackingField — with angle brackets and exact suffix.
        var csFiles = Directory.GetFiles(ClientSrcDir, "*.cs", SearchOption.AllDirectories);
        var backingFieldPattern = new Regex(@"""([^""]*k__BackingField[^""]*)""", RegexOptions.Compiled);
        var validPattern = new Regex(@"^<[A-Z]\w*>k__BackingField$", RegexOptions.Compiled);

        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            string content = File.ReadAllText(file);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var matches = backingFieldPattern.Matches(lines[i]);
                foreach (Match match in matches)
                {
                    string fieldString = match.Groups[1].Value;
                    if (!validPattern.IsMatch(fieldString))
                    {
                        string relPath = Path.GetRelativePath(RepoRoot, file);
                        violations.Add(relPath + ":" + (i + 1) + " — \"" + fieldString + "\" does not match <PropertyName>k__BackingField");
                    }
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Found backing field strings that don't match the C# compiler convention:\n  " + string.Join("\n  ", violations)
        );
    }

    #endregion

    #region Chained GetField().GetValue() pattern

    [Test]
    public void NoChainedGetFieldGetValue_WithoutNullCheck()
    {
        // The pattern .GetField(...).GetValue(...) is dangerous because GetField
        // can return null, causing a NullReferenceException on the chained .GetValue().
        // These should either use RequireField or add an intermediate null check.
        var csFiles = Directory.GetFiles(ClientSrcDir, "*.cs", SearchOption.AllDirectories);
        var chainPattern = new Regex(@"\.GetField\s*\([^)]*\)\s*\.GetValue\s*\(", RegexOptions.Compiled);
        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            string content = File.ReadAllText(file);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (chainPattern.IsMatch(lines[i]))
                {
                    string relPath = Path.GetRelativePath(RepoRoot, file);
                    violations.Add(relPath + ":" + (i + 1) + " — " + lines[i].Trim());
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Found chained .GetField().GetValue() calls without intermediate null check:\n  "
                + string.Join("\n  ", violations)
                + "\n\nUse ReflectionHelper.RequireField + null guard, or add a null check between GetField and GetValue."
        );
    }

    #endregion

    #region Patch registration — no orphaned patches

    /// <summary>
    /// Extracts all concrete patch class names from the Patches/ directory.
    /// Matches: "class FooPatch : ModulePatch" (including abstract base classes).
    /// </summary>
    private static List<(string ClassName, string RelPath)> GetAllPatchClasses()
    {
        var patchDir = Path.Combine(ClientSrcDir, "Patches");
        var csFiles = Directory.GetFiles(patchDir, "*.cs", SearchOption.AllDirectories);
        var classPattern = new Regex(@"(?:public|internal)\s+class\s+(\w+Patch\d?)\s*:\s*(\w+)", RegexOptions.Compiled);

        var results = new List<(string, string)>();
        foreach (var file in csFiles)
        {
            string content = File.ReadAllText(file);
            foreach (Match m in classPattern.Matches(content))
            {
                string className = m.Groups[1].Value;
                string baseClass = m.Groups[2].Value;

                // Skip abstract base classes (they aren't enabled directly)
                if (content.Contains("abstract class " + className))
                {
                    continue;
                }

                results.Add((className, Path.GetRelativePath(RepoRoot, file)));
            }
        }
        return results;
    }

    [Test]
    public void AllPatchClasses_AreRegisteredInPlugin()
    {
        // Every concrete ModulePatch subclass in Patches/ should appear in
        // QuestingBotsPlugin.cs as "new Patches.<name>().Enable()" — either
        // active or commented out. Orphaned patches are dead code that confuse
        // maintainers and may indicate missing functionality.
        var pluginPath = Path.Combine(ClientSrcDir, "QuestingBotsPlugin.cs");
        string pluginSource = File.ReadAllText(pluginPath);

        var patchClasses = GetAllPatchClasses();
        Assert.That(patchClasses, Is.Not.Empty, "Failed to find any patch classes");

        var orphaned = new List<string>();
        foreach (var (className, relPath) in patchClasses)
        {
            // Check if the class name appears anywhere in the plugin file
            // (either enabled or commented out)
            if (!pluginSource.Contains(className))
            {
                orphaned.Add(className + " (" + relPath + ")");
            }
        }

        Assert.That(
            orphaned,
            Is.Empty,
            "Found patch classes that are never referenced in QuestingBotsPlugin.cs:\n  "
                + string.Join("\n  ", orphaned)
                + "\n\nEither register them with .Enable() or remove the dead code."
        );
    }

    #endregion

    #region Patch target methods — obfuscated method name tracking

    [Test]
    public void ObfuscatedMethodNames_AreDocumented()
    {
        // Patches that use hardcoded string method names (not nameof()) are fragile
        // because game updates can rename them. This test tracks all such patches
        // to ensure none are silently added without documentation.
        var patchDir = Path.Combine(ClientSrcDir, "Patches");
        var csFiles = Directory.GetFiles(patchDir, "*.cs", SearchOption.AllDirectories);

        // Match: .GetMethod("someString" — where the string is NOT a nameof() call
        var getMethodStringPattern = new Regex(@"\.GetMethod\s*\(\s*""(\w+)""", RegexOptions.Compiled);
        // Also match nameof patterns to exclude them
        var nameofPattern = new Regex(@"nameof\(\w+\.\w+\)", RegexOptions.Compiled);

        // Known obfuscated method names — these are expected and reviewed.
        // When a new one appears, it should be consciously added here.
        var knownObfuscated = new HashSet<string>
        {
            "method_10", // BotOwnerBrainActivatePatch → BotOwner
            "method_15", // AirdropLandPatch → AirdropLogicClass
            "vmethod_5", // GameStartPatch → BaseLocalGame
            "GetNewProfile", // PScavProfilePatch → BotsPresets.BaseType (dynamic)
            "CreateFromLegacyParams", // ServerRequestPatch → dynamic type
            "ExceptAI", // ExceptAIPatch → dynamic type
            "IsValid", // SpawnPointIsValidPatch → dynamic type
        };

        var undocumented = new List<string>();

        foreach (var file in csFiles)
        {
            string content = File.ReadAllText(file);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                var match = getMethodStringPattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                // Skip if the line also uses nameof() (safe compile-time reference)
                if (nameofPattern.IsMatch(line))
                {
                    continue;
                }

                string methodName = match.Groups[1].Value;
                if (!knownObfuscated.Contains(methodName))
                {
                    string relPath = Path.GetRelativePath(RepoRoot, file);
                    undocumented.Add(relPath + ":" + (i + 1) + " — \"" + methodName + "\" — " + line.Trim());
                }
            }
        }

        Assert.That(
            undocumented,
            Is.Empty,
            "Found undocumented obfuscated method name strings in patch target methods:\n  "
                + string.Join("\n  ", undocumented)
                + "\n\nAdd new entries to the knownObfuscated set in this test after review."
        );
    }

    #endregion

    #region Patch target methods — GetTargetMethod returns non-null

    [Test]
    public void AllPatches_HaveGetTargetMethodOverride()
    {
        // Every concrete ModulePatch subclass must override GetTargetMethod().
        // This catches accidental omissions where the patch compiles but does nothing.
        var patchDir = Path.Combine(ClientSrcDir, "Patches");
        var csFiles = Directory.GetFiles(patchDir, "*.cs", SearchOption.AllDirectories);
        var classPattern = new Regex(@"(?:public|internal)\s+class\s+(\w+Patch\d?)\s*:\s*\w+", RegexOptions.Compiled);
        var getTargetPattern = new Regex(@"override\s+MethodBase\s+GetTargetMethod", RegexOptions.Compiled);

        var missing = new List<string>();

        foreach (var file in csFiles)
        {
            string content = File.ReadAllText(file);

            // Skip abstract classes
            if (content.Contains("abstract class"))
            {
                continue;
            }

            var classMatch = classPattern.Match(content);
            if (!classMatch.Success)
            {
                continue;
            }

            string className = classMatch.Groups[1].Value;

            if (!getTargetPattern.IsMatch(content))
            {
                string relPath = Path.GetRelativePath(RepoRoot, file);
                missing.Add(className + " (" + relPath + ")");
            }
        }

        Assert.That(missing, Is.Empty, "Found patch classes without GetTargetMethod override:\n  " + string.Join("\n  ", missing));
    }

    #endregion

    #region Patch ordering — HarmonyBefore/After attributes

    [Test]
    public void HarmonyOrderingAttributes_ReferenceValidPatchIds()
    {
        // If any [HarmonyBefore] or [HarmonyAfter] attributes are used,
        // verify they reference known patch IDs (plugin GUIDs).
        var patchDir = Path.Combine(ClientSrcDir, "Patches");
        var csFiles = Directory.GetFiles(patchDir, "*.cs", SearchOption.AllDirectories);
        var orderPattern = new Regex(@"\[Harmony(?:Before|After)\s*\(\s*""([^""]+)""\s*\)\]", RegexOptions.Compiled);

        var references = new List<string>();

        foreach (var file in csFiles)
        {
            string content = File.ReadAllText(file);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var match = orderPattern.Match(lines[i]);
                if (match.Success)
                {
                    string patchId = match.Groups[1].Value;
                    string relPath = Path.GetRelativePath(RepoRoot, file);
                    references.Add(relPath + ":" + (i + 1) + " — " + patchId);
                }
            }
        }

        // Currently no HarmonyBefore/After attributes exist. If any are added,
        // this test will start collecting them for validation.
        if (references.Count > 0)
        {
            TestContext.WriteLine(
                "Found HarmonyBefore/After references (verify patch IDs are valid):\n  " + string.Join("\n  ", references)
            );
        }

        // This is an informational test — it passes but logs findings.
        // If we need strict validation in the future, assert against known plugin IDs.
        Assert.Pass("Found " + references.Count + " HarmonyBefore/After attribute(s). None currently expected.");
    }

    #endregion

    #region Conditional patches — config toggle coverage

    [Test]
    public void ConditionalPatches_MatchConfigToggles()
    {
        // Verify that patches enabled under config conditions in QuestingBotsPlugin.cs
        // reference config properties that actually exist. This catches typos in
        // config paths and ensures conditional patches aren't wired to dead toggles.
        var pluginPath = Path.Combine(ClientSrcDir, "QuestingBotsPlugin.cs");
        string pluginSource = File.ReadAllText(pluginPath);

        // Extract config conditions before .Enable() calls
        // Pattern: if (ConfigController.Config.Some.Path) ... new Patches.X().Enable()
        var conditionalPattern = new Regex(@"ConfigController\.Config\.(\w+(?:\.\w+)*)", RegexOptions.Compiled);

        var configPaths = new HashSet<string>();
        foreach (Match m in conditionalPattern.Matches(pluginSource))
        {
            configPaths.Add(m.Groups[1].Value);
        }

        // Verify these config paths actually appear in configuration classes
        var configDir = Path.Combine(ClientSrcDir, "Configuration");
        var configFiles = Directory.Exists(configDir) ? Directory.GetFiles(configDir, "*.cs", SearchOption.AllDirectories) : new string[0];

        // Also check the main config model
        var modelFiles = Directory
            .GetFiles(ClientSrcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Contains("Configuration") || f.Contains("Config"))
            .ToArray();

        string allConfigSource = string.Join("\n", modelFiles.Select(f => File.ReadAllText(f)));

        // For each leaf property name in config paths, verify it exists somewhere
        var missingProperties = new List<string>();
        foreach (var path in configPaths)
        {
            string leafProperty = path.Split('.').Last();

            // Skip "Enabled" — it's a universal convention
            if (leafProperty == "Enabled")
            {
                continue;
            }

            // Check if the leaf property name exists in any config class
            if (!allConfigSource.Contains(leafProperty))
            {
                missingProperties.Add(path + " (leaf: " + leafProperty + ")");
            }
        }

        Assert.That(
            missingProperties,
            Is.Empty,
            "Config paths referenced in QuestingBotsPlugin.cs not found in configuration classes:\n  "
                + string.Join("\n  ", missingProperties)
        );
    }

    #endregion

    #region Every patch has a PatchPrefix or PatchPostfix

    [Test]
    public void AllPatches_HavePatchPrefixOrPostfix()
    {
        // A patch class without [PatchPrefix] or [PatchPostfix] does nothing.
        var patchDir = Path.Combine(ClientSrcDir, "Patches");
        var csFiles = Directory.GetFiles(patchDir, "*.cs", SearchOption.AllDirectories);
        var classPattern = new Regex(@"(?:public|internal)\s+class\s+(\w+Patch\d?)\s*:\s*\w+", RegexOptions.Compiled);
        var patchAttrPattern = new Regex(@"\[Patch(?:Prefix|Postfix|Transpiler|Finalizer)\]", RegexOptions.Compiled);

        var missing = new List<string>();

        foreach (var file in csFiles)
        {
            string content = File.ReadAllText(file);

            if (content.Contains("abstract class"))
            {
                continue;
            }

            var classMatch = classPattern.Match(content);
            if (!classMatch.Success)
            {
                continue;
            }

            string className = classMatch.Groups[1].Value;

            if (!patchAttrPattern.IsMatch(content))
            {
                string relPath = Path.GetRelativePath(RepoRoot, file);
                missing.Add(className + " (" + relPath + ")");
            }
        }

        Assert.That(
            missing,
            Is.Empty,
            "Found patch classes without any [PatchPrefix], [PatchPostfix], [PatchTranspiler], or [PatchFinalizer]:\n  "
                + string.Join("\n  ", missing)
        );
    }

    #endregion
}
