using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.BehaviorExtensions;

/// <summary>
/// Source-scanning and integration tests for the sprint blocking system.
/// Verifies danger zone removal, config toggle gating, transition-based logging,
/// helper null-safety patterns, and config.json field presence.
///
/// These tests read actual source files and config.json to prevent regressions.
/// They do not require Unity or game assemblies.
/// </summary>
[TestFixture]
public class SprintBlockingTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string CustomLogicPath = Path.Combine(
        RepoRoot,
        "src",
        "SPTQuestingBots.Client",
        "BehaviorExtensions",
        "CustomLogicDelayedUpdate.cs"
    );

    private static readonly string CombatStateHelperPath = Path.Combine(
        RepoRoot,
        "src",
        "SPTQuestingBots.Client",
        "Helpers",
        "CombatStateHelper.cs"
    );

    private static readonly string RaidTimeHelperPath = Path.Combine(
        RepoRoot,
        "src",
        "SPTQuestingBots.Client",
        "Helpers",
        "RaidTimeHelper.cs"
    );

    private static readonly string HearingSensorHelperPath = Path.Combine(
        RepoRoot,
        "src",
        "SPTQuestingBots.Client",
        "Helpers",
        "HearingSensorHelper.cs"
    );

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

    private static string ReadSource(string path)
    {
        Assert.That(File.Exists(path), Is.True, "Source file not found: " + path);
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Extracts the IsAllowedToSprint method body from the source file.
    /// </summary>
    private static string ExtractIsAllowedToSprint()
    {
        var source = ReadSource(CustomLogicPath);
        var match = Regex.Match(source, @"public bool IsAllowedToSprint\(\)\s*\{(.*?)\n        \}", RegexOptions.Singleline);
        Assert.That(match.Success, Is.True, "Could not extract IsAllowedToSprint method body");
        return match.Groups[1].Value;
    }

    private static JObject LoadSprintingLimitations()
    {
        var configPath = Path.Combine(RepoRoot, "config", "config.json");
        Assert.That(File.Exists(configPath), Is.True, "config/config.json not found");
        var root = JObject.Parse(File.ReadAllText(configPath));
        return (JObject)root["questing"]["sprinting_limitations"];
    }

    #region Danger zone removal

    [Test]
    public void IsAllowedToSprint_DoesNotCallIsInDangerZone()
    {
        var methodBody = ExtractIsAllowedToSprint();

        Assert.That(
            methodBody,
            Does.Not.Contain("IsInDangerZone"),
            "IsAllowedToSprint should not call CombatStateHelper.IsInDangerZone — it was removed because "
                + "BotDangerArea.BlockedCovers has unknown BSG lifecycle and could block sprint permanently"
        );
    }

    [Test]
    public void CombatStateHelper_IsInDangerZone_MethodStillExists()
    {
        // The method is preserved for other potential consumers — only removed from IsAllowedToSprint
        var source = ReadSource(CombatStateHelperPath);

        Assert.That(
            source,
            Does.Contain("public static bool IsInDangerZone(BotOwner bot)"),
            "CombatStateHelper.IsInDangerZone should still exist (not deleted, just no longer called from sprint logic)"
        );
    }

    [Test]
    public void IsAllowedToSprint_StillCallsIsPostCombat()
    {
        var methodBody = ExtractIsAllowedToSprint();

        Assert.That(
            methodBody,
            Does.Contain("CombatStateHelper.IsPostCombat"),
            "IsAllowedToSprint should still call CombatStateHelper.IsPostCombat for post-combat cooldown"
        );
    }

    #endregion

    #region Config toggle gating

    [Test]
    public void IsAllowedToSprint_GatesPostCombatBehindConfigToggle()
    {
        var methodBody = ExtractIsAllowedToSprint();

        Assert.That(
            methodBody,
            Does.Contain("sprintConfig.EnablePostCombatSprintBlock"),
            "Post-combat sprint block should be gated behind EnablePostCombatSprintBlock config toggle"
        );
    }

    [Test]
    public void IsAllowedToSprint_GatesLateRaidBehindConfigToggle()
    {
        var methodBody = ExtractIsAllowedToSprint();

        Assert.That(
            methodBody,
            Does.Contain("sprintConfig.EnableLateRaidSprintBlock"),
            "Late-raid sprint block should be gated behind EnableLateRaidSprintBlock config toggle"
        );
    }

    [Test]
    public void IsAllowedToSprint_GatesSuspicionBehindConfigToggle()
    {
        var methodBody = ExtractIsAllowedToSprint();

        Assert.That(
            methodBody,
            Does.Contain("sprintConfig.EnableSuspicionSprintBlock"),
            "Suspicion sprint block should be gated behind EnableSuspicionSprintBlock config toggle"
        );
    }

    [Test]
    public void IsAllowedToSprint_HasExactlyFourConfigToggleChecks()
    {
        var methodBody = ExtractIsAllowedToSprint();

        int toggleCount = CountOccurrences(methodBody, "sprintConfig.Enable");

        Assert.That(
            toggleCount,
            Is.EqualTo(7),
            "IsAllowedToSprint should have exactly 7 config toggle checks (post-combat, late-raid, suspicion, door-sprint-pause, stamina-exhaustion, physical-condition, overweight)"
        );
    }

    [Test]
    public void IsAllowedToSprint_ChecksToggleBeforePostCombatCall()
    {
        var methodBody = ExtractIsAllowedToSprint();

        int toggleIndex = methodBody.IndexOf("EnablePostCombatSprintBlock");
        int callIndex = methodBody.IndexOf("CombatStateHelper.IsPostCombat");

        Assert.That(toggleIndex, Is.GreaterThan(-1), "EnablePostCombatSprintBlock not found");
        Assert.That(callIndex, Is.GreaterThan(-1), "IsPostCombat call not found");
        Assert.That(
            toggleIndex,
            Is.LessThan(callIndex),
            "Config toggle EnablePostCombatSprintBlock must be checked BEFORE calling IsPostCombat"
        );
    }

    [Test]
    public void IsAllowedToSprint_ChecksToggleBeforeLateRaidCheck()
    {
        var methodBody = ExtractIsAllowedToSprint();

        int toggleIndex = methodBody.IndexOf("EnableLateRaidSprintBlock");
        int callIndex = methodBody.IndexOf("RaidTimeHelper.GetRemainingRaidFraction");

        Assert.That(toggleIndex, Is.GreaterThan(-1), "EnableLateRaidSprintBlock not found");
        Assert.That(callIndex, Is.GreaterThan(-1), "GetRemainingRaidFraction call not found");
        Assert.That(
            toggleIndex,
            Is.LessThan(callIndex),
            "Config toggle EnableLateRaidSprintBlock must be checked BEFORE calling GetRemainingRaidFraction"
        );
    }

    [Test]
    public void IsAllowedToSprint_ChecksToggleBeforeSuspicionCheck()
    {
        var methodBody = ExtractIsAllowedToSprint();

        int toggleIndex = methodBody.IndexOf("EnableSuspicionSprintBlock");
        int callIndex = methodBody.IndexOf("HearingSensorHelper.IsSuspicious");

        Assert.That(toggleIndex, Is.GreaterThan(-1), "EnableSuspicionSprintBlock not found");
        Assert.That(callIndex, Is.GreaterThan(-1), "IsSuspicious call not found");
        Assert.That(
            toggleIndex,
            Is.LessThan(callIndex),
            "Config toggle EnableSuspicionSprintBlock must be checked BEFORE calling IsSuspicious"
        );
    }

    #endregion

    #region Transition-based debug logging

    [Test]
    public void CustomLogicDelayedUpdate_HasWasSprintAllowedField()
    {
        var source = ReadSource(CustomLogicPath);

        Assert.That(
            source,
            Does.Contain("_wasSprintAllowed"),
            "CustomLogicDelayedUpdate should have _wasSprintAllowed field for transition tracking"
        );
    }

    [Test]
    public void IsAllowedToSprint_LogsWithSprintPrefix()
    {
        var methodBody = ExtractIsAllowedToSprint();

        Assert.That(methodBody, Does.Contain("\"[Sprint] \""), "Sprint block logging should use [Sprint] prefix for easy grep filtering");
    }

    [Test]
    public void IsAllowedToSprint_LogsOnlyOnAllowedToBlockedTransition()
    {
        var methodBody = ExtractIsAllowedToSprint();

        Assert.That(
            methodBody,
            Does.Contain("_wasSprintAllowed && !allowed"),
            "Debug logging should only fire on transition from allowed to blocked (not repeated blocked calls)"
        );
    }

    [Test]
    public void IsAllowedToSprint_LogsBlockReason()
    {
        var methodBody = ExtractIsAllowedToSprint();

        Assert.That(methodBody, Does.Contain("blockReason"), "Log message should include the block reason string");
    }

    [Test]
    public void IsAllowedToSprint_UpdatesWasSprintAllowedAfterCheck()
    {
        var methodBody = ExtractIsAllowedToSprint();

        Assert.That(
            methodBody,
            Does.Contain("_wasSprintAllowed = allowed"),
            "IsAllowedToSprint must update _wasSprintAllowed state after computing result"
        );
    }

    [Test]
    public void IsAllowedToSprint_LogCallCountIsExactlyOne()
    {
        var methodBody = ExtractIsAllowedToSprint();

        int logCount = CountOccurrences(methodBody, "LoggingController.LogDebug");

        Assert.That(logCount, Is.EqualTo(1), "IsAllowedToSprint should have exactly one LogDebug call (transition-based, not per-check)");
    }

    #endregion

    #region Block reason tracking (e2e flow)

    [Test]
    public void IsAllowedToSprint_UsesSingleBlockReasonVariable()
    {
        var methodBody = ExtractIsAllowedToSprint();

        Assert.That(
            methodBody,
            Does.Contain("string blockReason = null"),
            "Should use a single blockReason variable to track which check blocked sprint"
        );
    }

    [Test]
    public void IsAllowedToSprint_HasFiveBlockReasonAssignments()
    {
        var methodBody = ExtractIsAllowedToSprint();

        // Count: "blockReason = \"..." pattern (assignments of string literals)
        int assignmentCount = Regex.Matches(methodBody, @"blockReason = ""[^""]+""").Count;

        Assert.That(
            assignmentCount,
            Is.EqualTo(8),
            "Should have exactly 8 block reason assignments: post-combat cooldown, late raid, hearing suspicion, sharp path corner, approaching closed door, stamina exhausted, physical condition, overweight"
        );
    }

    [TestCase("post-combat cooldown")]
    [TestCase("late raid")]
    [TestCase("hearing suspicion")]
    [TestCase("sharp path corner")]
    [TestCase("approaching closed door")]
    public void IsAllowedToSprint_ContainsBlockReason(string expectedReason)
    {
        var methodBody = ExtractIsAllowedToSprint();

        Assert.That(
            methodBody,
            Does.Contain("\"" + expectedReason + "\""),
            "IsAllowedToSprint should contain block reason: " + expectedReason
        );
    }

    [Test]
    public void IsAllowedToSprint_ReturnsSingleAllowedResult()
    {
        var methodBody = ExtractIsAllowedToSprint();

        Assert.That(
            methodBody,
            Does.Contain("bool allowed = blockReason == null"),
            "Sprint decision should derive from single blockReason null check"
        );
        Assert.That(methodBody, Does.Contain("return allowed"), "Should return the allowed variable, not inline logic");
    }

    [Test]
    public void IsAllowedToSprint_HasEarlyReturnsBeforeConfigChecks()
    {
        var methodBody = ExtractIsAllowedToSprint();

        // The early returns (SprintingEnabled, IsSprintDelayed, CanSprintToObjective)
        // should appear BEFORE the blockReason variable
        int blockReasonIndex = methodBody.IndexOf("string blockReason");
        int sprintEnabledIndex = methodBody.IndexOf("SprintingEnabled");
        int sprintDelayedIndex = methodBody.IndexOf("IsSprintDelayed");
        int canSprintIndex = methodBody.IndexOf("CanSprintToObjective");

        Assert.Multiple(() =>
        {
            Assert.That(sprintEnabledIndex, Is.GreaterThan(-1).And.LessThan(blockReasonIndex));
            Assert.That(sprintDelayedIndex, Is.GreaterThan(-1).And.LessThan(blockReasonIndex));
            Assert.That(canSprintIndex, Is.GreaterThan(-1).And.LessThan(blockReasonIndex));
        });
    }

    [Test]
    public void IsAllowedToSprint_ShortCircuitsBlockReasonChecks()
    {
        // Each configurable check should be guarded by "blockReason == null"
        // to skip subsequent checks once a reason is found.
        // We count all "blockReason == null" occurrences, including the final
        // "bool allowed = blockReason == null" assignment.
        var methodBody = ExtractIsAllowedToSprint();

        int nullCheckCount = CountOccurrences(methodBody, "blockReason == null");

        // The first check (post-combat) doesn't need a null guard,
        // but the remaining 7 checks (late-raid, suspicion, path corner, closed door, stamina, physical condition, overweight) do,
        // plus 1 for the final "bool allowed = blockReason == null" assignment = 8 total.
        Assert.That(nullCheckCount, Is.EqualTo(8), "7 guard checks + 1 final assignment should reference 'blockReason == null'");
    }

    #endregion

    #region Helper null-safety patterns

    [Test]
    public void CombatStateHelper_IsPostCombat_ChecksTimeSinceNull()
    {
        var source = ReadSource(CombatStateHelperPath);

        Assert.That(source, Does.Contain("if (timeSince == null)"), "IsPostCombat should check timeSince == null and return false");
    }

    [Test]
    public void CombatStateHelper_IsPostCombat_ChecksTimeSinceRange()
    {
        var source = ReadSource(CombatStateHelperPath);

        Assert.That(
            source,
            Does.Contain("timeSince.Value >= 0f && timeSince.Value <= cooldownSeconds"),
            "IsPostCombat should check that timeSince is within [0, cooldownSeconds] range"
        );
    }

    [Test]
    public void CombatStateHelper_IsPostCombat_ReturnsNullableGetTimeSinceLastCombat()
    {
        // Verify IsPostCombat calls GetTimeSinceLastCombat which returns float?
        var source = ReadSource(CombatStateHelperPath);

        Assert.That(
            source,
            Does.Contain("public static float? GetTimeSinceLastCombat"),
            "GetTimeSinceLastCombat should return float? (nullable) to handle no-combat-yet case"
        );
    }

    [Test]
    public void CombatStateHelper_GetTimeSinceLastCombat_ReturnsNullForNoCombat()
    {
        // When lastSeenTime <= 0, no combat has occurred — should return null
        var source = ReadSource(CombatStateHelperPath);

        Assert.That(
            source,
            Does.Contain("if (lastSeenTime <= 0f)"),
            "GetTimeSinceLastCombat should return null when lastSeenTime <= 0 (no combat occurred)"
        );
    }

    [Test]
    public void RaidTimeHelper_GetRemainingRaidFraction_ReturnsNullable()
    {
        var source = ReadSource(RaidTimeHelperPath);

        Assert.That(
            source,
            Does.Contain("public static float? GetRemainingRaidFraction()"),
            "GetRemainingRaidFraction should return float? (nullable) to handle raid-not-started case"
        );
    }

    [Test]
    public void RaidTimeHelper_GetRemainingRaidFraction_ReturnsNullWhenRaidNotStarted()
    {
        var source = ReadSource(RaidTimeHelperPath);

        Assert.That(
            source,
            Does.Contain("if (!RaidHelpers.HasRaidStarted())"),
            "GetRemainingRaidFraction should check HasRaidStarted and return null if false"
        );
    }

    [Test]
    public void HearingSensorHelper_IsSuspicious_DelegatesToECS()
    {
        var source = ReadSource(HearingSensorHelperPath);

        Assert.That(
            source,
            Does.Contain("BotEntityBridge.GetSensorForBot(bot, BotSensor.IsSuspicious)"),
            "IsSuspicious should delegate to BotEntityBridge.GetSensorForBot for ECS-based lookup"
        );
    }

    [Test]
    public void HearingSensorHelper_GetHearingSensor_HasNullCheckForBot()
    {
        var source = ReadSource(HearingSensorHelperPath);

        Assert.That(source, Does.Contain("bot?.HearingSensor"), "GetHearingSensor should null-propagate on the bot parameter");
    }

    #endregion

    #region config.json integration

    [Test]
    public void ConfigJson_HasEnablePostCombatSprintBlock()
    {
        var sprintConfig = LoadSprintingLimitations();
        var field = sprintConfig["enable_post_combat_sprint_block"];

        Assert.That(field, Is.Not.Null, "enable_post_combat_sprint_block missing from config.json");
        Assert.That(field.Value<bool>(), Is.True, "Default should be true");
    }

    [Test]
    public void ConfigJson_HasEnableLateRaidSprintBlock()
    {
        var sprintConfig = LoadSprintingLimitations();
        var field = sprintConfig["enable_late_raid_sprint_block"];

        Assert.That(field, Is.Not.Null, "enable_late_raid_sprint_block missing from config.json");
        Assert.That(field.Value<bool>(), Is.True, "Default should be true");
    }

    [Test]
    public void ConfigJson_HasEnableSuspicionSprintBlock()
    {
        var sprintConfig = LoadSprintingLimitations();
        var field = sprintConfig["enable_suspicion_sprint_block"];

        Assert.That(field, Is.Not.Null, "enable_suspicion_sprint_block missing from config.json");
        Assert.That(field.Value<bool>(), Is.True, "Default should be true");
    }

    [Test]
    public void ConfigJson_SprintingLimitations_HasAllExpectedFields()
    {
        var sprintConfig = LoadSprintingLimitations();

        Assert.Multiple(() =>
        {
            Assert.That(sprintConfig["enable_debounce_time"], Is.Not.Null, "enable_debounce_time missing");
            Assert.That(sprintConfig["stamina"], Is.Not.Null, "stamina missing");
            Assert.That(sprintConfig["sharp_path_corners"], Is.Not.Null, "sharp_path_corners missing");
            Assert.That(sprintConfig["approaching_closed_doors"], Is.Not.Null, "approaching_closed_doors missing");
            Assert.That(sprintConfig["post_combat_cooldown_seconds"], Is.Not.Null, "post_combat_cooldown_seconds missing");
            Assert.That(sprintConfig["late_raid_no_sprint_threshold"], Is.Not.Null, "late_raid_no_sprint_threshold missing");
            Assert.That(sprintConfig["enable_post_combat_sprint_block"], Is.Not.Null, "enable_post_combat_sprint_block missing");
            Assert.That(sprintConfig["enable_late_raid_sprint_block"], Is.Not.Null, "enable_late_raid_sprint_block missing");
            Assert.That(sprintConfig["enable_suspicion_sprint_block"], Is.Not.Null, "enable_suspicion_sprint_block missing");
        });
    }

    [Test]
    public void ConfigJson_SprintingLimitations_DoesNotContainDangerZoneField()
    {
        // Danger zone was never a config field, but verify nothing was accidentally added
        var sprintConfig = LoadSprintingLimitations();
        var json = sprintConfig.ToString();

        Assert.That(json, Does.Not.Contain("danger_zone"), "sprinting_limitations should not contain any danger_zone fields");
    }

    #endregion

    #region Helpers

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    #endregion
}
