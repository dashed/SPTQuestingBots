using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.E2E;

[TestFixture]
[Category("SAINContract")]
public class SAINRepositoryContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SainRepoRoot = FindSainRepoRoot();

    [Test]
    public void CheckedOutSainRepo_ExportsExpectedExternalInteropSurface()
    {
        RequireSainRepo();

        string source = ReadSainSource("SAIN/Interop/SAINExternal.cs");
        string interopHelperSource = ReadSainSource("SAIN/Interop/SAINInterop.cs");

        Assert.Multiple(() =>
        {
            Assert.That(
                source,
                Does.Contain("namespace SAIN.Interop;"),
                "SAIN should expose its public interop surface from SAIN.Interop."
            );
            Assert.That(source, Does.Contain("public static class SAINExternal"), "SAIN should expose a stable SAINExternal entry point.");

            AssertContainsMethodSignature(source, "bool", "IgnoreHearing", "BotOwner", "bool", "bool", "float");
            AssertContainsMethodSignature(source, "string", "GetPersonality", "BotOwner");
            AssertContainsMethodSignature(source, "bool", "ExtractBot", "BotOwner");
            AssertContainsMethodSignature(source, "void", "GetExtractedBots", "List<string>");
            AssertContainsMethodSignature(source, "void", "GetExtractionInfos", "List<ExtractionInfo>");
            AssertContainsMethodSignature(source, "bool", "TrySetExfilForBot", "BotOwner");
            AssertContainsMethodSignature(source, "float", "TimeSinceSenseEnemy", "BotOwner");
            AssertContainsMethodSignature(source, "bool", "IsPathTowardEnemy", "NavMeshPath", "BotOwner", "float", "float");
            AssertContainsMethodSignature(source, "bool", "CanBotQuest", "BotOwner", "Vector3", "float");

            Assert.That(
                interopHelperSource,
                Does.Contain("public class ExtractionInfo"),
                "SAIN should keep exporting the ExtractionInfo DTO for external consumers."
            );
            Assert.That(
                source,
                Does.Contain("public enum ECombatReason"),
                "SAIN should keep exporting the combat-reason enum used by its external surface."
            );
        });
    }

    [Test]
    public void QuestingBots_SainInteropWrapper_Matches_CheckedOutSainContract()
    {
        RequireSainRepo();

        string sainSource = ReadSainSource("SAIN/Interop/SAINExternal.cs");
        string questingBotsSource = ReadQuestingBotsSource("src/SPTQuestingBots.Client/BotLogic/ExternalMods/Interop/SAINInterop.cs");

        Assert.Multiple(() =>
        {
            Assert.That(
                questingBotsSource,
                Does.Contain("ExternalTypeName = \"SAIN.Interop.SAINExternal, SAIN\""),
                "QuestingBots should point at SAIN's real public external type."
            );
            Assert.That(
                questingBotsSource,
                Does.Contain("RuntimeRequiredMethodNames"),
                "QuestingBots should require only the SAIN members it actually needs at runtime."
            );
            Assert.That(
                questingBotsSource,
                Does.Contain("OptionalMethodNames"),
                "QuestingBots should keep non-critical SAIN members optional so upstream drift does not disable extraction/hearing interop."
            );
            Assert.That(
                questingBotsSource,
                Does.Not.Contain("\"GetExtractionInfos\""),
                "QuestingBots should not keep GetExtractionInfos in its runtime or optional SAIN method contract."
            );
            Assert.That(
                questingBotsSource,
                Does.Not.Contain("class ExtractionInfo"),
                "QuestingBots should not mirror SAIN's ExtractionInfo DTO until it can safely map it."
            );
        });

        AssertSourceContainsAll(
            questingBotsSource,
            "ExtractBot",
            "TrySetExfilForBot",
            "IgnoreHearing",
            "IsPathTowardEnemy",
            "TimeSinceSenseEnemy",
            "CanBotQuest",
            "GetExtractedBots",
            "GetPersonality"
        );

        AssertSourceContainsAll(
            sainSource,
            "ExtractBot",
            "TrySetExfilForBot",
            "IgnoreHearing",
            "IsPathTowardEnemy",
            "TimeSinceSenseEnemy",
            "CanBotQuest",
            "GetExtractedBots",
            "GetPersonality"
        );
    }

    [Test]
    public void CheckedOutSainRepo_PreservesIdentityContract()
    {
        RequireSainRepo();

        string sainProjectSource = ReadSainSource("SAIN/SAIN.csproj");
        string assemblyInfoSource = ReadSainSource("SAIN/Plugin/AssemblyInfoClass.cs");
        string pluginSource = ReadSainSource("SAIN/SAINPlugin.cs");
        string questingBotsInteropSource = ReadQuestingBotsSource(
            "src/SPTQuestingBots.Client/BotLogic/ExternalMods/Interop/SAINInterop.cs"
        );
        string questingBotsModInfoSource = ReadQuestingBotsSource(
            "src/SPTQuestingBots.Client/BotLogic/ExternalMods/ModInfo/SAINModInfo.cs"
        );

        Assert.Multiple(() =>
        {
            Assert.That(
                sainProjectSource,
                Does.Contain("<AssemblyName>SAIN</AssemblyName>"),
                "SAIN should keep the assembly name that QuestingBots uses in its reflected type name."
            );
            Assert.That(
                assemblyInfoSource,
                Does.Contain("public const string SAINGUID = \"me.sol.sain\";"),
                "SAIN should keep the plugin GUID QuestingBots uses for detection."
            );
            Assert.That(
                pluginSource,
                Does.Contain("[BepInPlugin(SAINGUID, SAINName, SAINVersion)]"),
                "SAIN should keep exposing its plugin identity through the BepInPlugin attribute."
            );
            Assert.That(
                questingBotsInteropSource,
                Does.Contain("Chainloader.PluginInfos.ContainsKey(\"me.sol.sain\")"),
                "QuestingBots should continue detecting the current SAIN plugin GUID."
            );
            Assert.That(
                questingBotsModInfoSource,
                Does.Contain("public override string GUID { get; } = \"me.sol.sain\";"),
                "QuestingBots should continue advertising the current SAIN plugin GUID in its mod-info abstraction."
            );
        });
    }

    [Test]
    public void CheckedOutSainRepo_DetectsQuestingBots_And_Disables_TimeBasedExtracts()
    {
        RequireSainRepo();

        string assemblyInfoSource = ReadSainSource("SAIN/Plugin/AssemblyInfoClass.cs");
        string modDetectionSource = ReadSainSource("SAIN/Plugin/ModDetection.cs");
        string extractLayerSource = ReadSainSource("SAIN/Layers/Extract/ExtractLayer.cs");
        string peacefulLayerSource = ReadSainSource("SAIN/Layers/Peace/PeacefulLayer.cs");

        Assert.Multiple(() =>
        {
            Assert.That(
                assemblyInfoSource,
                Does.Contain("public const string QuestingBotsGUID = \"com.DanW.QuestingBots\";"),
                "SAIN should keep QuestingBots detection keyed to the current plugin GUID."
            );
            Assert.That(
                modDetectionSource,
                Does.Contain("Chainloader.PluginInfos.ContainsKey(QuestingBotsGUID)")
                    .And.Contain("QuestingBotsLoaded = true;")
                    .And.Contain("Logger.LogInfo($\"SAIN: Questing Bots Detected.\");"),
                "SAIN should continue explicitly detecting QuestingBots at startup."
            );
            Assert.That(
                extractLayerSource,
                Does.Contain("if (ModDetection.QuestingBotsLoaded)").And.Contain("return false;"),
                "SAIN ExtractLayer should keep deferring time-based extracts when QuestingBots is present."
            );
            Assert.That(
                peacefulLayerSource,
                Does.Contain("if (ModDetection.QuestingBotsLoaded)").And.Contain("return false;"),
                "SAIN PeacefulLayer should keep deferring time-based extracts when QuestingBots is present."
            );
        });
    }

    [Test]
    public void CheckedOutSainRepo_MatchesPinnedCiMetadata_WhenConfigured()
    {
        RequireSainRepo();

        string expectedSainVersion = Environment.GetEnvironmentVariable("QB_SAIN_EXPECTED_VERSION");
        string expectedSptVersion = Environment.GetEnvironmentVariable("QB_SAIN_EXPECTED_SPT_VERSION");
        string expectedBigBrainVersion = Environment.GetEnvironmentVariable("QB_SAIN_EXPECTED_BIGBRAIN_VERSION");

        if (
            string.IsNullOrWhiteSpace(expectedSainVersion)
            && string.IsNullOrWhiteSpace(expectedSptVersion)
            && string.IsNullOrWhiteSpace(expectedBigBrainVersion)
        )
        {
            Assert.Ignore("Pinned SAIN metadata expectations not configured. Set QB_SAIN_EXPECTED_* to enforce a CI-pinned baseline.");
        }

        string assemblyInfoSource = ReadSainSource("SAIN/Plugin/AssemblyInfoClass.cs");

        Assert.Multiple(() =>
        {
            if (!string.IsNullOrWhiteSpace(expectedSainVersion))
            {
                Assert.That(
                    assemblyInfoSource,
                    Does.Contain($"public const string SAINVersion = \"{expectedSainVersion}\";"),
                    "The checked-out SAIN repo should match the CI-pinned SAIN version."
                );
            }

            if (!string.IsNullOrWhiteSpace(expectedSptVersion))
            {
                Assert.That(
                    assemblyInfoSource,
                    Does.Contain($"public const string SPTVersion = \"{expectedSptVersion}\";"),
                    "The checked-out SAIN repo should match the CI-pinned SPT compatibility baseline."
                );
            }

            if (!string.IsNullOrWhiteSpace(expectedBigBrainVersion))
            {
                Assert.That(
                    assemblyInfoSource,
                    Does.Contain($"public const string BigBrainVersion = \"{expectedBigBrainVersion}\";"),
                    "The checked-out SAIN repo should match the CI-pinned BigBrain compatibility baseline."
                );
            }
        });
    }

    private static void AssertSourceContainsAll(string source, params string[] expectedSnippets)
    {
        foreach (string snippet in expectedSnippets)
        {
            Assert.That(source, Does.Contain(snippet), $"Missing expected SAIN contract member '{snippet}'.");
        }
    }

    private static void AssertContainsMethodSignature(string source, string returnType, string methodName, params string[] parameterTypes)
    {
        string parameterPattern =
            parameterTypes.Length == 0 ? string.Empty : string.Join(@"\s*,\s*", Array.ConvertAll(parameterTypes, BuildParameterPattern));

        string pattern =
            @"public\s+static\s+"
            + Regex.Escape(returnType)
            + @"\s+"
            + Regex.Escape(methodName)
            + @"\s*\(\s*"
            + parameterPattern
            + @"\s*\)";

        Assert.That(
            Regex.IsMatch(source, pattern, RegexOptions.Multiline),
            Is.True,
            $"Expected SAIN contract signature not found: {returnType} {methodName}({string.Join(", ", parameterTypes)})"
        );
    }

    private static string BuildParameterPattern(string parameterType) => Regex.Escape(parameterType) + @"\s+\w+(?:\s*=\s*[^,\)]+)?";

    private static void RequireSainRepo()
    {
        if (string.IsNullOrWhiteSpace(SainRepoRoot))
        {
            Assert.Ignore(
                "SAIN repo not found. Set QB_SAIN_REPO or checkout SAIN into external/SAIN to enable repo-to-repo contract tests."
            );
        }
    }

    private static string FindRepoRoot()
    {
        string dir = TestContext.CurrentContext.TestDirectory;
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

    private static string FindSainRepoRoot()
    {
        string envPath = Environment.GetEnvironmentVariable("QB_SAIN_REPO");
        string siblingPath = Path.GetFullPath(Path.Combine(RepoRoot, "..", "SAIN"));
        string externalPath = Path.Combine(RepoRoot, "external", "SAIN");

        foreach (string candidate in new[] { envPath, externalPath, siblingPath })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (File.Exists(Path.Combine(candidate, "SAIN", "Interop", "SAINExternal.cs")))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static string ReadQuestingBotsSource(string relativePath)
    {
        string fullPath = Path.Combine(RepoRoot, relativePath);
        Assert.That(File.Exists(fullPath), Is.True, $"QuestingBots source file not found: {fullPath}");
        return File.ReadAllText(fullPath);
    }

    private static string ReadSainSource(string relativePath)
    {
        string fullPath = Path.Combine(SainRepoRoot, relativePath);
        Assert.That(File.Exists(fullPath), Is.True, $"SAIN source file not found: {fullPath}");
        return File.ReadAllText(fullPath);
    }
}
