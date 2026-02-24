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

    // ── CombatStateHelper: all methods handle null fields ──

    [Test]
    public void CombatStateHelper_GetTimeSinceLastCombat_ChecksFieldNull()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "CombatStateHelper.cs"));
        var method = ExtractMethod(source, "public static float? GetTimeSinceLastCombat");
        Assert.That(
            method,
            Does.Contain("_enemyLastSeenTimeRealField == null"),
            "GetTimeSinceLastCombat must check if reflection field is null"
        );
    }

    [Test]
    public void CombatStateHelper_GetLastEnemyPosition_ChecksFieldNull()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "CombatStateHelper.cs"));
        var method = ExtractMethod(source, "public static Vector3? GetLastEnemyPosition");
        Assert.That(
            method,
            Does.Contain("_enemyLastSeenPositionRealField == null"),
            "GetLastEnemyPosition must check if reflection field is null"
        );
    }

    [Test]
    public void CombatStateHelper_IsInDangerZone_ChecksFieldNull()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "CombatStateHelper.cs"));
        var method = ExtractMethod(source, "public static bool IsInDangerZone");
        Assert.That(method, Does.Contain("_dangerAreaField == null"), "IsInDangerZone must check if reflection field is null");
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
    public void RaidTimeHelper_GetGameTimer_ChecksSingletonAndField()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "RaidTimeHelper.cs"));
        var method = ExtractMethod(source, "public static GameTimerClass GetGameTimer");

        Assert.That(method, Does.Contain("_gameTimerField == null"), "GetGameTimer must check if reflection field is null");
        Assert.That(method, Does.Contain("Singleton"), "GetGameTimer must check game instance availability");
    }

    [Test]
    public void RaidTimeHelper_GetRemainingRaidFraction_ChecksRaidStarted()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "RaidTimeHelper.cs"));
        var method = ExtractMethod(source, "public static float? GetRemainingRaidFraction");

        Assert.That(method, Does.Contain("HasRaidStarted"), "GetRemainingRaidFraction must check if raid has started");
    }

    // ── ExtractionHelper: handles null fields ──

    [Test]
    public void ExtractionHelper_HasExfiltrationAssigned_ChecksFieldNull()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "ExtractionHelper.cs"));
        var method = ExtractMethod(source, "public static bool HasExfiltrationAssigned");

        Assert.That(method, Does.Contain("_exfiltrationField == null"), "HasExfiltrationAssigned must check if reflection field is null");
        Assert.That(method, Does.Contain("bot == null"), "HasExfiltrationAssigned must handle null bot");
    }

    [Test]
    public void ExtractionHelper_IsLeaving_ChecksFieldNull()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "ExtractionHelper.cs"));
        var method = ExtractMethod(source, "public static bool IsLeaving");

        Assert.That(method, Does.Contain("_leaveDataField == null"), "IsLeaving must check if reflection field is null");
    }

    // ── PlantZoneHelper: handles null fields ──

    [Test]
    public void PlantZoneHelper_GetPlantZone_ChecksFieldNull()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "PlantZoneHelper.cs"));
        var method = ExtractMethod(source, "public static PlaceItemTrigger GetPlantZone");

        Assert.That(method, Does.Contain("_placeItemZoneField == null"), "GetPlantZone must check if reflection field is null");
    }

    // ── HearingSensorHelper: handles null fields ──

    [Test]
    public void HearingSensorHelper_GetHearingSensor_ChecksFieldNull()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "HearingSensorHelper.cs"));
        var method = ExtractMethod(source, "public static BotHearingSensor GetHearingSensor");

        Assert.That(method, Does.Contain("_hearingSensorField == null"), "GetHearingSensor must check if reflection field is null");
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
    public void ReflectionHelper_KnownFields_Has22OrMoreEntries()
    {
        var source = File.ReadAllText(Path.Combine(HelpersDir, "ReflectionHelper.cs"));

        // Count entries in the KnownFields array
        int count = Regex.Matches(source, @"\(typeof\(").Count;

        // KnownFields should have 22 entries (as documented in memory)
        Assert.That(count, Is.GreaterThanOrEqualTo(22), "KnownFields should have at least 22 entries");
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
