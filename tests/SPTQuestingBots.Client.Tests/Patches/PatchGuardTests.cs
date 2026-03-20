using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Patches;

/// <summary>
/// Source-scanning tests that verify critical guard clauses exist in patch files.
/// These tests read the raw C# source to ensure guards are not accidentally removed.
/// </summary>
[TestFixture]
public class PatchGuardTests
{
    private static readonly string PatchesRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "SPTQuestingBots.Client", "Patches")
    );

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Assert.That(Directory.Exists(PatchesRoot), Is.True, $"Patches directory not found at {PatchesRoot}");
    }

    #region BotDiedPatch — IsDead guard

    [Test]
    public void BotDiedPatch_HasIsDeadGuard()
    {
        string content = ReadPatchFile("Spawning", "Advanced", "BotDiedPatch.cs");

        // The IsDead guard must appear in the prefix to prevent double-fire of OnBotRemoved
        Assert.That(content, Does.Contain("bot.IsDead"), "BotDiedPatch must check bot.IsDead");
    }

    [Test]
    public void BotDiedPatch_IsDeadGuardReturnsFalse()
    {
        string content = ReadPatchFile("Spawning", "Advanced", "BotDiedPatch.cs");

        // When IsDead is true, the prefix should return false (skip original method)
        // because the bot has already been processed
        Assert.That(
            Regex.IsMatch(content, @"if\s*\(bot\.IsDead\)\s*\{\s*return\s+false;"),
            Is.True,
            "BotDiedPatch IsDead guard must return false to skip the original method"
        );
    }

    [Test]
    public void BotDiedPatch_IsDeadGuardAppearsBeforeIsDeadAssignment()
    {
        string content = ReadPatchFile("Spawning", "Advanced", "BotDiedPatch.cs");

        // The guard (bot.IsDead check) must appear BEFORE the assignment (bot.IsDead = true)
        int guardIndex = content.IndexOf("if (bot.IsDead)");
        int assignIndex = content.IndexOf("bot.IsDead = true");

        Assert.That(guardIndex, Is.GreaterThan(-1), "BotDiedPatch must have IsDead guard");
        Assert.That(assignIndex, Is.GreaterThan(-1), "BotDiedPatch must assign IsDead = true");
        Assert.That(guardIndex, Is.LessThan(assignIndex), "IsDead guard must appear before IsDead assignment to prevent double-fire");
    }

    [Test]
    public void BotDiedPatch_IsDeadGuardAppearsBeforeShouldPlayerBeTreatedAsHuman()
    {
        string content = ReadPatchFile("Spawning", "Advanced", "BotDiedPatch.cs");

        // Guard must be the first check — before ShouldPlayerBeTreatedAsHuman
        int guardIndex = content.IndexOf("bot.IsDead");
        int humanCheckIndex = content.IndexOf("ShouldPlayerBeTreatedAsHuman");

        Assert.That(guardIndex, Is.GreaterThan(-1), "BotDiedPatch must have IsDead guard");
        Assert.That(humanCheckIndex, Is.GreaterThan(-1), "BotDiedPatch must check ShouldPlayerBeTreatedAsHuman");
        Assert.That(guardIndex, Is.LessThan(humanCheckIndex), "IsDead guard must be checked before ShouldPlayerBeTreatedAsHuman");
    }

    #endregion

    #region BotOwnerBrainActivatePatch — ActiveFail guard

    [Test]
    public void BotOwnerBrainActivatePatch_HasActiveFailGuard()
    {
        string content = ReadPatchFile("BotOwnerBrainActivatePatch.cs");

        // The ActiveFail guard must exist to avoid registering a bot whose brain failed to activate
        Assert.That(content, Does.Contain("EBotState.ActiveFail"), "BotOwnerBrainActivatePatch must check for ActiveFail state");
    }

    [Test]
    public void BotOwnerBrainActivatePatch_ActiveFailGuardReturnsEarly()
    {
        string content = ReadPatchFile("BotOwnerBrainActivatePatch.cs");

        // When BotState == ActiveFail, the postfix should return early
        Assert.That(
            Regex.IsMatch(content, @"if\s*\(__instance\.BotState\s*==\s*EBotState\.ActiveFail\)\s*\{\s*return;"),
            Is.True,
            "BotOwnerBrainActivatePatch ActiveFail guard must return early"
        );
    }

    [Test]
    public void BotOwnerBrainActivatePatch_ActiveFailGuardAppearsBeforeRegisterBot()
    {
        string content = ReadPatchFile("BotOwnerBrainActivatePatch.cs");

        // Guard must appear before registerBot call to prevent registering broken bots
        int guardIndex = content.IndexOf("EBotState.ActiveFail");
        int registerIndex = content.IndexOf("registerBot(");

        Assert.That(guardIndex, Is.GreaterThan(-1), "Must have ActiveFail guard");
        Assert.That(registerIndex, Is.GreaterThan(-1), "Must call registerBot");
        Assert.That(
            guardIndex,
            Is.LessThan(registerIndex),
            "ActiveFail guard must appear before registerBot to prevent registering broken bots"
        );
    }

    [Test]
    public void BotOwnerBrainActivatePatch_ActiveFailGuardIsFirstCheckInPostfix()
    {
        string content = ReadPatchFile("BotOwnerBrainActivatePatch.cs");

        // Extract the postfix method body
        int postfixStart = content.IndexOf("PatchPostfix(");
        Assert.That(postfixStart, Is.GreaterThan(-1), "Must have PatchPostfix method");

        string afterPostfix = content.Substring(postfixStart);

        // The ActiveFail check should be the first conditional in the postfix body
        int activeFailIndex = afterPostfix.IndexOf("ActiveFail");
        int firstIfIndex = afterPostfix.IndexOf("if (");
        // Also check "if(" without space
        int firstIfNoSpaceIndex = afterPostfix.IndexOf("if(");

        int firstConditional = firstIfIndex;
        if (firstIfNoSpaceIndex >= 0 && (firstConditional < 0 || firstIfNoSpaceIndex < firstConditional))
            firstConditional = firstIfNoSpaceIndex;

        Assert.That(activeFailIndex, Is.GreaterThan(-1), "Must have ActiveFail check");
        Assert.That(firstConditional, Is.GreaterThan(-1), "Must have at least one if statement");

        // The ActiveFail check should be within the first if statement
        Assert.That(
            activeFailIndex,
            Is.LessThan(firstConditional + 100),
            "ActiveFail guard should be the first conditional check in the postfix"
        );
    }

    #endregion

    private static string ReadPatchFile(params string[] pathSegments)
    {
        string filePath = Path.Combine(new[] { PatchesRoot }.Concat(pathSegments).ToArray());
        Assert.That(File.Exists(filePath), Is.True, $"Patch file not found: {filePath}");
        return File.ReadAllText(filePath);
    }
}
