using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Helpers;

/// <summary>
/// Verifies that every <c>RequireField</c> call in helper files references a field name
/// that is also registered in <c>ReflectionHelper.KnownFields</c>.
/// Catches drift between helpers and the central field registry.
/// </summary>
[TestFixture]
public class HelperFieldRegistrationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string ClientSrcDir = Path.Combine(RepoRoot, "src", "SPTQuestingBots.Client");
    private static readonly string HelpersDir = Path.Combine(ClientSrcDir, "Helpers");

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

    /// <summary>
    /// Extracts all field name strings from RequireField calls in a source file.
    /// Matches patterns like: RequireField(typeof(X), "fieldName", ...)
    /// </summary>
    private static List<(string FileName, string FieldName)> GetRequireFieldCalls(string filePath)
    {
        string content = File.ReadAllText(filePath);
        string fileName = Path.GetFileName(filePath);

        // Match: RequireField(\n typeof(...),\n "fieldName"
        var pattern = new Regex(@"RequireField\s*\(\s*typeof\(\w+\)\s*,\s*""([^""]+)""", RegexOptions.Compiled);

        return pattern.Matches(content).Cast<Match>().Select(m => (fileName, m.Groups[1].Value)).ToList();
    }

    /// <summary>
    /// Extracts all field name strings registered in KnownFields.
    /// </summary>
    private static HashSet<string> GetKnownFieldNames()
    {
        var helperPath = Path.Combine(HelpersDir, "ReflectionHelper.cs");
        string source = File.ReadAllText(helperPath);

        // Match the second string in each KnownFields tuple: ..., "fieldName", ...
        var entryPattern = new Regex(@"typeof\(\w+\)\s*,\s*""([^""]+)""\s*,\s*""[^""]+""", RegexOptions.Compiled);

        return new HashSet<string>(entryPattern.Matches(source).Cast<Match>().Select(m => m.Groups[1].Value));
    }

    [Test]
    public void AllRequireFieldCalls_UseFieldNamesRegisteredInKnownFields()
    {
        var knownFieldNames = GetKnownFieldNames();
        Assert.That(knownFieldNames.Count, Is.GreaterThan(0), "Failed to parse any field names from KnownFields");

        var helperFiles = Directory.GetFiles(HelpersDir, "*.cs", SearchOption.TopDirectoryOnly);
        var unregistered = new List<string>();

        foreach (var file in helperFiles)
        {
            // Skip ReflectionHelper itself — it defines the registry, not a consumer
            if (file.EndsWith("ReflectionHelper.cs"))
            {
                continue;
            }

            var requireFieldCalls = GetRequireFieldCalls(file);
            foreach (var (fileName, fieldName) in requireFieldCalls)
            {
                if (!knownFieldNames.Contains(fieldName))
                {
                    unregistered.Add(fileName + ": \"" + fieldName + "\"");
                }
            }
        }

        Assert.That(
            unregistered,
            Is.Empty,
            "Found RequireField calls referencing field names not in ReflectionHelper.KnownFields:\n  "
                + string.Join("\n  ", unregistered)
                + "\n\nAdd matching entries to KnownFields for validation."
        );
    }

    [Test]
    public void AllRequireFieldCalls_AcrossEntireSource_AreRegistered()
    {
        // Scan the entire client source tree (not just Helpers/) for RequireField calls
        // that use typeof(X) patterns. Dynamic lookups (e.g. LogicLayerMonitor with
        // runtime-resolved types) naturally won't match the typeof() regex and are excluded.
        var knownFieldNames = GetKnownFieldNames();
        var csFiles = Directory.GetFiles(ClientSrcDir, "*.cs", SearchOption.AllDirectories);
        var unregistered = new List<string>();

        foreach (var file in csFiles)
        {
            if (file.EndsWith("ReflectionHelper.cs"))
            {
                continue;
            }

            var requireFieldCalls = GetRequireFieldCalls(file);
            foreach (var (fileName, fieldName) in requireFieldCalls)
            {
                if (!knownFieldNames.Contains(fieldName))
                {
                    unregistered.Add(fileName + ": \"" + fieldName + "\"");
                }
            }
        }

        Assert.That(
            unregistered,
            Is.Empty,
            "Found RequireField calls across client source with field names not in KnownFields:\n  "
                + string.Join("\n  ", unregistered)
                + "\n\nAdd matching entries to KnownFields for validation."
        );
    }

    [Test]
    public void AllHelperFiles_ExistAndContainRequireFieldCalls()
    {
        // Verify that all expected helper files exist and use RequireField.
        // This catches accidental deletion or refactoring away from the RequireField pattern.
        string[] expectedHelpers = new[]
        {
            "CombatStateHelper.cs",
            "RaidTimeHelper.cs",
            "ExtractionHelper.cs",
            "PlantZoneHelper.cs",
            "HearingSensorHelper.cs",
            "ItemHelpers.cs",
        };

        foreach (var helperName in expectedHelpers)
        {
            var filePath = Path.Combine(HelpersDir, helperName);
            Assert.That(File.Exists(filePath), Is.True, "Expected helper file not found: " + helperName);

            var calls = GetRequireFieldCalls(filePath);
            Assert.That(calls, Is.Not.Empty, helperName + " should contain at least one RequireField call");
        }
    }

    [Test]
    [TestCase("CombatStateHelper.cs", 6)]
    [TestCase("RaidTimeHelper.cs", 1)]
    [TestCase("ExtractionHelper.cs", 2)]
    [TestCase("PlantZoneHelper.cs", 1)]
    [TestCase("HearingSensorHelper.cs", 1)]
    public void NewHelper_HasExpectedRequireFieldCount(string fileName, int expectedCount)
    {
        var filePath = Path.Combine(HelpersDir, fileName);
        Assert.That(File.Exists(filePath), Is.True, "Helper file not found: " + fileName);

        var calls = GetRequireFieldCalls(filePath);
        Assert.That(
            calls.Count,
            Is.EqualTo(expectedCount),
            "Expected " + expectedCount + " RequireField calls in " + fileName + " but found " + calls.Count
        );
    }

    [Test]
    [TestCase("RaidTimeHelper")]
    [TestCase("ExtractionHelper")]
    [TestCase("HearingSensorHelper")]
    public void Helper_HasConsumersOutsideHelpersDirectory(string helperClassName)
    {
        // Verify that the helper is actually referenced (consumed) in at least one .cs file
        // outside the Helpers/ directory. This catches helpers that are defined but never wired in.
        var pattern = new Regex(Regex.Escape(helperClassName) + @"\.", RegexOptions.Compiled);
        var csFiles = Directory.GetFiles(ClientSrcDir, "*.cs", SearchOption.AllDirectories);
        var consumers = new List<string>();

        foreach (var file in csFiles)
        {
            // Skip files inside the Helpers/ directory
            if (file.StartsWith(HelpersDir + Path.DirectorySeparatorChar))
            {
                continue;
            }

            string content = File.ReadAllText(file);
            if (pattern.IsMatch(content))
            {
                consumers.Add(Path.GetRelativePath(RepoRoot, file));
            }
        }

        Assert.That(
            consumers,
            Is.Not.Empty,
            helperClassName + " has no consumers outside the Helpers/ directory. Wire it into the codebase."
        );
    }
}
