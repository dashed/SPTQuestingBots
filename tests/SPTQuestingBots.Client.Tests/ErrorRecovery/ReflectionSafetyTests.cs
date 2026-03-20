using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.ErrorRecovery;

/// <summary>
/// Verifies that all reflection-based helper classes handle null FieldInfo
/// propagation safely — when game fields are renamed in updates, the mod
/// should log errors and return safe defaults, not throw NREs.
/// </summary>
[TestFixture]
public class ReflectionSafetyTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string HelpersDir = Path.Combine(RepoRoot, "src", "SPTQuestingBots.Client", "Helpers");

    private static string FindRepoRoot()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return TestContext.CurrentContext.TestDirectory;
    }

    // ── CombatStateHelper: all methods handle null bot via direct property access ──

    [Test]
    public void CombatStateHelper_UsesDirectPropertyAccess()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "CombatStateHelper.cs"));

        // Should NOT contain reflection field references (migrated to direct access)
        Assert.That(
            source,
            Does.Not.Contain("_enemyLastSeenTimeRealField"),
            "CombatStateHelper should use direct property access, not reflection"
        );
        Assert.That(source, Does.Not.Contain("_dangerAreaField"), "CombatStateHelper should use direct property access, not reflection");

        // Should use direct property access
        Assert.That(source, Does.Contain("bot.BotsGroup.EnemyLastSeenTimeReal"), "Should access EnemyLastSeenTimeReal directly");
        Assert.That(source, Does.Contain("bot.DangerArea"), "Should access DangerArea directly");
    }

    [Test]
    public void CombatStateHelper_AllPublicMethods_CheckBotNull()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "CombatStateHelper.cs"));

        // Each public method should check for bot null — either directly
        // or by delegating to a method that does the null check
        var directCheckMethods = new[] { "GetTimeSinceLastCombat", "GetLastEnemyPosition", "IsInDangerZone" };

        foreach (var name in directCheckMethods)
        {
            var method = ExtractMethod(source, name);
            Assert.That(
                method,
                Does.Contain("bot?").Or.Contain("bot == null").Or.Contain("bot != null"),
                $"{name} must handle null bot parameter"
            );
        }

        // IsPostCombat delegates to GetTimeSinceLastCombat which does the null check
        var isPostCombat = ExtractMethod(source, "IsPostCombat");
        Assert.That(
            isPostCombat,
            Does.Contain("GetTimeSinceLastCombat"),
            "IsPostCombat must delegate to GetTimeSinceLastCombat (which handles null)"
        );
    }

    // ── RaidTimeHelper: handles null game instance ──

    [Test]
    public void RaidTimeHelper_GetGameTimer_ChecksSingleton()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "RaidTimeHelper.cs"));
        var method = ExtractMethod(source, "public static GameTimerClass GetGameTimer");

        Assert.That(source, Does.Not.Contain("_gameTimerField"), "RaidTimeHelper should use direct property access, not reflection");
        Assert.That(method, Does.Contain("Singleton"), "GetGameTimer must check game instance availability");
        Assert.That(method, Does.Contain("GameTimer"), "GetGameTimer must access GameTimer property directly");
    }

    [Test]
    public void RaidTimeHelper_GetRemainingRaidFraction_ChecksRaidStarted()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "RaidTimeHelper.cs"));
        var method = ExtractMethod(source, "public static float? GetRemainingRaidFraction");

        Assert.That(method, Does.Contain("HasRaidStarted"), "GetRemainingRaidFraction must check if raid has started");
    }

    // ── ExtractionHelper: uses direct property access ──

    [Test]
    public void ExtractionHelper_UsesDirectPropertyAccess()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "ExtractionHelper.cs"));

        Assert.That(source, Does.Not.Contain("_exfiltrationField"), "ExtractionHelper should use direct property access, not reflection");
        Assert.That(source, Does.Not.Contain("_leaveDataField"), "ExtractionHelper should use direct property access, not reflection");
        Assert.That(source, Does.Contain("bot?.Exfiltration"), "Should access Exfiltration directly");
        Assert.That(source, Does.Contain("bot?.LeaveData"), "Should access LeaveData directly");
    }

    // ── PlantZoneHelper: uses direct property access ──

    [Test]
    public void PlantZoneHelper_UsesDirectPropertyAccess()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "PlantZoneHelper.cs"));

        Assert.That(source, Does.Not.Contain("_placeItemZoneField"), "PlantZoneHelper should use direct property access, not reflection");
        Assert.That(source, Does.Contain("PlaceItemZone"), "Should access PlaceItemZone directly");
    }

    // ── HearingSensorHelper: uses direct property access ──

    [Test]
    public void HearingSensorHelper_GetHearingSensor_HasNullCheckForBot()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "HearingSensorHelper.cs"));

        Assert.That(
            source,
            Does.Not.Contain("_hearingSensorField"),
            "HearingSensorHelper should use direct property access, not reflection"
        );
        Assert.That(source, Does.Contain("bot?.HearingSensor"), "Should access HearingSensor directly");
    }

    // ── ReflectionHelper.RequireField: logs error, returns null ──

    [Test]
    public void ReflectionHelper_RequireField_LogsOnMissing()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "ReflectionHelper.cs"));
        var method = ExtractMethod(source, "public static FieldInfo RequireField");

        Assert.That(method, Does.Contain("field == null"), "RequireField must check for null field");
        Assert.That(method, Does.Contain("LogError"), "RequireField must log an error when field is missing");
        Assert.That(method, Does.Contain("return field"), "RequireField must return the field (null or valid)");
    }

    // ── KnownFields: ValidateAllReflectionFields checks all entries ──

    [Test]
    public void ReflectionHelper_ValidateAllReflectionFields_IteratesAllKnownFields()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "ReflectionHelper.cs"));
        var method = ExtractMethod(source, "public static int ValidateAllReflectionFields");

        Assert.That(method, Does.Contain("foreach"), "ValidateAllReflectionFields must iterate all known fields");
        Assert.That(method, Does.Contain("failures++"), "ValidateAllReflectionFields must count failures");
        Assert.That(method, Does.Contain("return failures"), "ValidateAllReflectionFields must return failure count");
    }

    [Test]
    public void ReflectionHelper_KnownFields_Has10OrMoreEntries()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "ReflectionHelper.cs"));

        // Count entries in the KnownFields array — typeof( may be separated from ( by whitespace due to formatting
        int count = Regex.Matches(source, @"typeof\(\w+\)\s*,\s*""[^""]+""").Count;

        // KnownFields was reduced from 22 to 10 after migrating 12 fields to direct property access
        Assert.That(count, Is.GreaterThanOrEqualTo(10), "KnownFields should have at least 10 entries");
    }

    // ── Helpers ──

    private static string ExtractMethod(string source, string signature)
    {
        int start = source.IndexOf(signature);
        if (start < 0)
            return "";

        int braceCount = 0;
        bool foundFirst = false;
        int end = start;
        for (int i = start; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                braceCount++;
                foundFirst = true;
            }
            else if (source[i] == '}')
            {
                braceCount--;
                if (foundFirst && braceCount == 0)
                {
                    end = i + 1;
                    break;
                }
            }
        }
        return source.Substring(start, end - start);
    }
}
