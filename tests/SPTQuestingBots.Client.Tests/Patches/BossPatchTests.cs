using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Patches;

/// <summary>
/// Source-scanning tests that verify boss-related patches have correct structure.
/// These tests read raw C# source files to catch common patch mistakes.
/// </summary>
[TestFixture]
public class BossPatchTests
{
    private static readonly string PatchesRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "SPTQuestingBots.Client", "Patches")
    );

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Assert.That(Directory.Exists(PatchesRoot), Is.True, $"Patches directory not found at {PatchesRoot}");
    }

    #region SetNewBossPatch — prefix-only consolidation

    [Test]
    public void SetNewBossPatch_HasPrefixOnly()
    {
        string content = ReadPatchFile("Spawning", "SetNewBossPatch.cs");

        Assert.That(content, Does.Contain("[PatchPrefix]"), "SetNewBossPatch must have a prefix");
        Assert.That(content, Does.Not.Contain("[PatchPostfix]"), "SetNewBossPatch must not have a postfix — all logic should be in prefix");
    }

    [Test]
    public void SetNewBossPatch_PrefixReturnsBool()
    {
        string content = ReadPatchFile("Spawning", "SetNewBossPatch.cs");

        // The prefix must return bool to control whether the original method runs
        Assert.That(
            Regex.IsMatch(content, @"static\s+bool\s+PatchPrefix"),
            Is.True,
            "SetNewBossPatch prefix must return bool to control original method execution"
        );
    }

    [Test]
    public void SetNewBossPatch_PrefixHasRefBoss1()
    {
        string content = ReadPatchFile("Spawning", "SetNewBossPatch.cs");

        // Boss_1 must be passed by ref so the prefix can set it when skipping the original
        Assert.That(
            content.Contains("ref BotOwner ___Boss_1"),
            Is.True,
            "SetNewBossPatch prefix must declare ___Boss_1 as ref to modify the field"
        );
    }

    [Test]
    public void SetNewBossPatch_ClearsBossToFollowBeforeCheck()
    {
        string content = ReadPatchFile("Spawning", "SetNewBossPatch.cs");

        // BossToFollow must be cleared before looking for the new boss
        int clearIndex = content.IndexOf("BossToFollow = null");
        int iamBossIndex = content.IndexOf("IamBoss");

        Assert.That(clearIndex, Is.GreaterThan(-1), "Must clear BossToFollow");
        Assert.That(iamBossIndex, Is.GreaterThan(-1), "Must check IamBoss");
        Assert.That(clearIndex, Is.LessThan(iamBossIndex), "BossToFollow must be cleared before checking IamBoss");
    }

    [Test]
    public void SetNewBossPatch_ReturnsFalseWhenDesignatedBossFound()
    {
        string content = ReadPatchFile("Spawning", "SetNewBossPatch.cs");

        // When a designated boss is found, the prefix must return false to skip
        // the game's random promotion which would create a state mismatch
        Assert.That(content, Does.Contain("return false"), "Must return false to skip original when designated boss found");
    }

    [Test]
    public void SetNewBossPatch_ReturnsTrueAsFallback()
    {
        string content = ReadPatchFile("Spawning", "SetNewBossPatch.cs");

        // When no mod-designated boss is found, let the game handle it
        Assert.That(content, Does.Contain("return true"), "Must return true to let game handle boss selection when no mod-designated boss");
    }

    [Test]
    public void SetNewBossPatch_SetsBoss1WhenSkippingOriginal()
    {
        string content = ReadPatchFile("Spawning", "SetNewBossPatch.cs");

        // When skipping the original method, Boss_1 must be set
        int setBossIndex = content.IndexOf("___Boss_1 =");
        int returnFalseIndex = content.IndexOf("return false");

        Assert.That(setBossIndex, Is.GreaterThan(-1), "Must assign ___Boss_1");
        Assert.That(returnFalseIndex, Is.GreaterThan(-1), "Must return false");
        Assert.That(setBossIndex, Is.LessThan(returnFalseIndex), "___Boss_1 must be set before returning false");
    }

    [Test]
    public void SetNewBossPatch_ExcludesDeadBossFromSelection()
    {
        string content = ReadPatchFile("Spawning", "SetNewBossPatch.cs");

        // The designated boss check must exclude the dead boss by comparing Profile.Id
        Assert.That(
            content.Contains("follower.Profile.Id != boss.Profile.Id"),
            Is.True,
            "Must exclude the dead boss from new boss selection by comparing Profile.Id"
        );
    }

    [Test]
    public void SetNewBossPatch_BreaksOnFirstDesignatedBoss()
    {
        string content = ReadPatchFile("Spawning", "SetNewBossPatch.cs");

        // Should use break to take the first IamBoss follower, not iterate to the last
        // (prevents non-deterministic behavior when multiple followers have IamBoss)
        int iamBossIndex = content.IndexOf("IamBoss");
        int breakIndex = content.IndexOf("break;", iamBossIndex);
        int returnFalseIndex = content.IndexOf("return false");

        Assert.That(breakIndex, Is.GreaterThan(-1), "Must break after finding the designated boss");
        Assert.That(breakIndex, Is.LessThan(returnFalseIndex), "break must come before return false");
    }

    #endregion

    #region GetAllBossPlayersPatch — mod-only filtering

    [Test]
    public void GetAllBossPlayersPatch_FiltersUsingGeneratedBotProfileIDs()
    {
        string content = ReadPatchFile("Spawning", "GetAllBossPlayersPatch.cs");

        // Must use BotGenerator.GetAllGeneratedBotProfileIDs to identify mod bots
        Assert.That(
            content.Contains("GetAllGeneratedBotProfileIDs"),
            Is.True,
            "GetAllBossPlayersPatch must use GetAllGeneratedBotProfileIDs to filter only mod-generated bots"
        );
    }

    [Test]
    public void GetAllBossPlayersPatch_DoesNotFilterAllAIBosses()
    {
        string content = ReadPatchFile("Spawning", "GetAllBossPlayersPatch.cs");

        // Must NOT use bare !p.IsAI which filters out real AI bosses (Killa, Reshala, etc.)
        Assert.That(
            Regex.IsMatch(content, @"Where\([^)]*!p\.IsAI\s*&&\s*\(p\.AIData"),
            Is.False,
            "GetAllBossPlayersPatch must not filter all AI bosses — only mod-generated ones"
        );
    }

    [Test]
    public void GetAllBossPlayersPatch_ChecksIAmBoss()
    {
        string content = ReadPatchFile("Spawning", "GetAllBossPlayersPatch.cs");

        // Must still check IAmBoss to select only boss players
        Assert.That(content.Contains("IAmBoss"), Is.True, "Must check IAmBoss to identify boss players");
    }

    [Test]
    public void GetAllBossPlayersPatch_ChecksIsAIBeforeProfileLookup()
    {
        string content = ReadPatchFile("Spawning", "GetAllBossPlayersPatch.cs");

        // The filter should check p.IsAI before checking profile ID to avoid
        // unnecessarily looking up human players in the generated bot set
        Assert.That(
            content.Contains("p.IsAI && modBotIds.Contains") || content.Contains("p.IsAI && BotGenerator"),
            Is.True,
            "Must check IsAI before profile ID lookup for efficiency"
        );
    }

    [Test]
    public void GetAllBossPlayersPatch_ImportsBotGeneratorNamespace()
    {
        string content = ReadPatchFile("Spawning", "GetAllBossPlayersPatch.cs");

        Assert.That(
            content.Contains("using SPTQuestingBots.Components.Spawning;"),
            Is.True,
            "Must import SPTQuestingBots.Components.Spawning for BotGenerator access"
        );
    }

    [Test]
    public void GetAllBossPlayersPatch_ReturnsFalseToSkipOriginal()
    {
        string content = ReadPatchFile("Spawning", "GetAllBossPlayersPatch.cs");

        // This prefix replaces the original method entirely
        Assert.That(content.Contains("return false"), Is.True, "Must return false to skip original");
    }

    [Test]
    public void GetAllBossPlayersPatch_SetsResult()
    {
        string content = ReadPatchFile("Spawning", "GetAllBossPlayersPatch.cs");

        Assert.That(content.Contains("__result ="), Is.True, "Must set __result");
        Assert.That(content.Contains("ref ") && content.Contains("__result"), Is.True, "Must declare __result as ref");
    }

    #endregion

    private static string ReadPatchFile(params string[] pathSegments)
    {
        string filePath = Path.Combine(new[] { PatchesRoot }.Concat(pathSegments).ToArray());
        Assert.That(File.Exists(filePath), Is.True, $"Patch file not found: {filePath}");
        return File.ReadAllText(filePath);
    }
}
