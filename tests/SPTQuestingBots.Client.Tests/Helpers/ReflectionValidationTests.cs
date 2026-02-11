using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Helpers;

/// <summary>
/// Validates that all reflection-based field lookups in the codebase are registered
/// in <c>ReflectionHelper.KnownFields</c> and use correct field names.
///
/// These tests provide layered defense against obfuscated field name mismatches:
/// <list type="bullet">
///   <item>Source scanning ensures all <c>AccessTools.Field</c> calls use <c>RequireField</c></item>
///   <item>Source scanning ensures Harmony <c>___param</c> injections are registered</item>
///   <item>DLL metadata validation (when libs/ present) verifies field names against the game assembly</item>
/// </list>
/// </summary>
[TestFixture]
public class ReflectionValidationTests
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

    #region Source-scanning: AccessTools.Field calls use RequireField

    [Test]
    public void AllAccessToolsFieldCalls_ShouldUseRequireField()
    {
        // Verify that no raw AccessTools.Field calls remain in client source code.
        // All field lookups should go through ReflectionHelper.RequireField for error logging.
        var csFiles = Directory.GetFiles(ClientSrcDir, "*.cs", SearchOption.AllDirectories);
        var rawCallPattern = new Regex(@"AccessTools\.Field\s*\(", RegexOptions.Compiled);
        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            // Skip the ReflectionHelper itself (it uses AccessTools.Field internally)
            if (file.EndsWith("ReflectionHelper.cs"))
            {
                continue;
            }

            string content = File.ReadAllText(file);
            var matches = rawCallPattern.Matches(content);
            if (matches.Count > 0)
            {
                string relPath = Path.GetRelativePath(RepoRoot, file);
                violations.Add(relPath + " (" + matches.Count + " call(s))");
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Found raw AccessTools.Field calls that should use ReflectionHelper.RequireField instead:\n  " + string.Join("\n  ", violations)
        );
    }

    #endregion

    #region Source-scanning: Harmony ___param field names are registered

    [Test]
    public void AllHarmonyFieldInjections_AreDocumentedInReflectionHelper()
    {
        // Scan PatchPrefix/PatchPostfix methods for ___param parameters.
        // Each one should have a corresponding entry in ReflectionHelper.KnownFields.
        var patchDir = Path.Combine(ClientSrcDir, "Patches");
        var csFiles = Directory.GetFiles(patchDir, "*.cs", SearchOption.AllDirectories);

        // Match parameters like: ___Boss_1, ____allPlayers, ___Bots
        // Pattern: exactly 3 or 4 leading underscores followed by a letter
        var paramPattern = new Regex(@"(?<!\w)(_{3,4})([A-Za-z]\w*)", RegexOptions.Compiled);
        // Match PatchPrefix/PatchPostfix method signatures
        var patchMethodPattern = new Regex(@"static\s+\w+\s+Patch(?:Prefix|Postfix)\s*\(([^)]+)\)", RegexOptions.Compiled);

        var reflectionHelperSource = File.ReadAllText(Path.Combine(ClientSrcDir, "Helpers", "ReflectionHelper.cs"));

        var unregistered = new List<string>();

        foreach (var file in csFiles)
        {
            string content = File.ReadAllText(file);
            var methodMatches = patchMethodPattern.Matches(content);

            foreach (Match methodMatch in methodMatches)
            {
                string paramList = methodMatch.Groups[1].Value;
                var fieldParams = paramPattern.Matches(paramList);

                foreach (Match fieldParam in fieldParams)
                {
                    string underscores = fieldParam.Groups[1].Value;
                    string identPart = fieldParam.Groups[2].Value;

                    // Reconstruct the actual field name:
                    // ___Bots → field "Bots" (3 underscores stripped)
                    // ____allPlayers → field "_allPlayers" (3 underscores stripped, one remains)
                    string fieldName = underscores.Length > 3 ? new string('_', underscores.Length - 3) + identPart : identPart;

                    // Check if the field name appears in ReflectionHelper's KnownFields
                    if (!reflectionHelperSource.Contains("\"" + fieldName + "\""))
                    {
                        string relPath = Path.GetRelativePath(RepoRoot, file);
                        unregistered.Add(relPath + ": " + underscores + identPart + " (field: " + fieldName + ")");
                    }
                }
            }
        }

        Assert.That(
            unregistered,
            Is.Empty,
            "Found Harmony field injections not registered in ReflectionHelper.KnownFields:\n  "
                + string.Join("\n  ", unregistered)
                + "\n\nAdd entries to ReflectionHelper.KnownFields for validation."
        );
    }

    #endregion

    #region DLL metadata validation (ci-full only)

    /// <summary>
    /// Parses <c>ReflectionHelper.KnownFields</c> from source code to extract
    /// (type simple name, field name) pairs without requiring game assembly references.
    /// </summary>
    private static List<(string TypeName, string FieldName, string Context)> ParseKnownFieldsFromSource()
    {
        var helperPath = Path.Combine(ClientSrcDir, "Helpers", "ReflectionHelper.cs");
        string source = File.ReadAllText(helperPath);

        // Match entries like: (typeof(BotSpawner), "Bots", "BotDiedPatch ___Bots"),
        var entryPattern = new Regex(@"typeof\((?:[\w.]+\.)?(\w+)\)(?:\.BaseType)?,\s*""([^""]+)"",\s*""([^""]+)""", RegexOptions.Compiled);

        var results = new List<(string, string, string)>();
        foreach (Match m in entryPattern.Matches(source))
        {
            results.Add((m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value));
        }
        return results;
    }

    [Test]
    public void KnownFields_MatchGameAssemblyMetadata()
    {
        // Validates field names against the actual Assembly-CSharp.dll metadata.
        // Skipped when libs/ is not available (e.g. in CI without game DLLs).
        var dllPath = Path.Combine(RepoRoot, "libs", "Assembly-CSharp.dll");
        if (!File.Exists(dllPath))
        {
            Assert.Ignore("Skipped: libs/Assembly-CSharp.dll not found (run 'make copy-libs' for full validation)");
        }

        var knownFields = ParseKnownFieldsFromSource();
        Assert.That(knownFields, Is.Not.Empty, "Failed to parse KnownFields from ReflectionHelper.cs source");

        // Build a dictionary of type name → set of field names from the DLL
        var typeFields = new Dictionary<string, HashSet<string>>();

        using var stream = File.OpenRead(dllPath);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();

        foreach (var typeDef in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(typeDef);
            string typeName = metadata.GetString(type.Name);

            if (!typeFields.ContainsKey(typeName))
            {
                typeFields[typeName] = new HashSet<string>();
            }

            foreach (var fieldHandle in type.GetFields())
            {
                var field = metadata.GetFieldDefinition(fieldHandle);
                typeFields[typeName].Add(metadata.GetString(field.Name));
            }
        }

        var failures = new List<string>();
        foreach (var (typeName, fieldName, context) in knownFields)
        {
            // Skip backing fields and BigBrain internals (not in Assembly-CSharp.dll)
            if (fieldName.StartsWith("<") || typeName == "CustomLayerWrapper")
            {
                continue;
            }

            if (!typeFields.TryGetValue(typeName, out var fields))
            {
                // Type might be a base class resolved at runtime (e.g. BotsPresets.BaseType)
                // Skip these since we can't resolve inheritance from raw metadata
                TestContext.WriteLine("INFO: Type '" + typeName + "' not found in DLL (may be runtime-resolved). Skipping.");
                continue;
            }

            if (!fields.Contains(fieldName))
            {
                string available = string.Join(", ", fields.OrderBy(f => f));
                failures.Add("Field '" + fieldName + "' not found on " + typeName + " (" + context + "). Available: [" + available + "]");
            }
        }

        Assert.That(
            failures,
            Is.Empty,
            "Field names in ReflectionHelper.KnownFields do not match game DLL:\n  " + string.Join("\n  ", failures)
        );
    }

    #endregion

    #region Registry completeness

    [Test]
    public void KnownFields_HasExpectedMinimumEntryCount()
    {
        // Sanity check that the registry hasn't been accidentally emptied or truncated.
        var knownFields = ParseKnownFieldsFromSource();
        Assert.That(knownFields.Count, Is.GreaterThanOrEqualTo(10), "ReflectionHelper.KnownFields should have at least 10 entries");
    }

    #endregion
}
