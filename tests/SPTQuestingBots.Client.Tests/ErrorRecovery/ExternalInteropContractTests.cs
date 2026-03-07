using System.IO;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.ErrorRecovery;

[TestFixture]
public class ExternalInteropContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Test]
    public void SAINInterop_DeclaresRequiredAndOptionalContracts_WithExactSignatureValidation()
    {
        string source = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/ExternalMods/Interop/SAINInterop.cs");

        Assert.Multiple(() =>
        {
            Assert.That(
                source,
                Does.Contain("ExternalTypeName = \"SAIN.Interop.SAINExternal, SAIN\""),
                "SAIN interop should lock the reflected external type name."
            );
            Assert.That(
                source,
                Does.Contain("RuntimeRequiredMethodNames"),
                "SAIN interop should centralize its runtime-critical reflected contract."
            );
            Assert.That(
                source,
                Does.Contain("OptionalMethodNames"),
                "SAIN interop should track optional reflected members separately from runtime-critical ones."
            );
            Assert.That(
                source,
                Does.Contain("Type.GetType(ExternalTypeName)"),
                "SAIN interop should resolve the external type through the contract constant."
            );
            Assert.That(
                source,
                Does.Contain("Missing required SAIN interop members: "),
                "SAIN interop should fail loudly when a required member or signature disappears."
            );
            Assert.That(
                source,
                Does.Contain("BindingFlags.Public | BindingFlags.Static").And.Contain("method.ReturnType != returnType"),
                "SAIN interop should validate exact static signatures, not just method names."
            );
            Assert.That(source, Does.Contain("return _SAINInteropAvailable;"), "SAIN interop init should cache the contract result.");
        });

        AssertSourceContainsAll(source, "ExtractBot", "TrySetExfilForBot", "IgnoreHearing");
        AssertSourceContainsAll(source, "IsPathTowardEnemy", "TimeSinceSenseEnemy", "CanBotQuest", "GetExtractedBots", "GetPersonality");

        Assert.That(
            source,
            Does.Not.Contain("\"GetExtractionInfos\""),
            "QuestingBots should not keep GetExtractionInfos in its required or optional SAIN method contract."
        );
        Assert.That(
            source,
            Does.Not.Contain("class ExtractionInfo"),
            "QuestingBots should not mirror SAIN's ExtractionInfo DTO until it has a safe DTO-mapping layer."
        );
    }

    [Test]
    public void SAINHearingFunction_ForwardsIgnoreUnderFireFlag()
    {
        string source = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/ExternalMods/Functions/Hearing/SAINHearingFunction.cs");

        Assert.That(
            source,
            Does.Contain("IgnoreHearing(BotOwner, value, ignoreUnderFire, duration)"),
            "SAIN hearing interop should forward the ignore-under-fire flag instead of hard-coding false."
        );
    }

    [Test]
    public void LootingBotsInterop_DeclaresExpectedTypeAndRequiredMembers()
    {
        string source = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/ExternalMods/Interop/LootingBotsInterop.cs");

        Assert.Multiple(() =>
        {
            Assert.That(
                source,
                Does.Contain("ExternalTypeName = \"LootingBots.External, skwizzy.LootingBots\""),
                "LootingBots interop should lock the reflected external type name."
            );
            Assert.That(
                source,
                Does.Contain("RequiredMethodNames = [\"ForceBotToScanLoot\", \"PreventBotFromLooting\"]"),
                "LootingBots interop should centralize the reflected member contract."
            );
            Assert.That(
                source,
                Does.Contain("Missing required LootingBots interop methods: "),
                "LootingBots interop should fail loudly when a required member disappears."
            );
            Assert.That(source, Does.Contain("return _LootingBotsInteropAvailable;"), "LootingBots init should cache the contract result.");
        });
    }

    private static void AssertSourceContainsAll(string source, params string[] expectedSnippets)
    {
        foreach (string snippet in expectedSnippets)
        {
            Assert.That(source, Does.Contain('"' + snippet + '"'), $"Missing expected reflected member '{snippet}'.");
        }
    }

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

    private static string ReadSourceFile(string relativePath)
    {
        string fullPath = Path.Combine(RepoRoot, relativePath);
        Assert.That(File.Exists(fullPath), Is.True, $"Source file not found: {fullPath}");
        return File.ReadAllText(fullPath);
    }
}
