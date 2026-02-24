using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.BehaviorExtensions;

/// <summary>
/// Source-scanning tests for BigBrain action and base-class bugs.
///
/// Bug 1: FindNearbyDoors passed EDoorState.Open, making IsNearAndMovingTowardClosedDoor
///        always iterate zero closed doors and return false (sprint-into-door never blocked).
/// Bug 2: VultureAction, LingerAction, InvestigateAction, and UnlockDoorAction used
///        `new System.Random()` per call, giving identical results for bots in the same
///        millisecond. PatrolAction already had the correct SharedRandom pattern.
/// Bug 3: AmbushAction (and SnipeAction by inheritance) counted travel time toward ambush
///        duration because the action elapsed timer was not restarted during travel and
///        CheckMinElapsedActionTime was called before the close-to-objective check.
/// </summary>
[TestFixture]
public class ActionBugTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string CustomLogicPath = Path.Combine(
        RepoRoot,
        "src",
        "SPTQuestingBots.Client",
        "BehaviorExtensions",
        "CustomLogicDelayedUpdate.cs"
    );

    private static readonly string AmbushActionPath = Path.Combine(
        RepoRoot,
        "src",
        "SPTQuestingBots.Client",
        "BotLogic",
        "Objective",
        "AmbushAction.cs"
    );

    private static readonly string VultureActionPath = Path.Combine(
        RepoRoot,
        "src",
        "SPTQuestingBots.Client",
        "BotLogic",
        "Objective",
        "VultureAction.cs"
    );

    private static readonly string LingerActionPath = Path.Combine(
        RepoRoot,
        "src",
        "SPTQuestingBots.Client",
        "BotLogic",
        "Objective",
        "LingerAction.cs"
    );

    private static readonly string InvestigateActionPath = Path.Combine(
        RepoRoot,
        "src",
        "SPTQuestingBots.Client",
        "BotLogic",
        "Objective",
        "InvestigateAction.cs"
    );

    private static readonly string UnlockDoorActionPath = Path.Combine(
        RepoRoot,
        "src",
        "SPTQuestingBots.Client",
        "BotLogic",
        "Objective",
        "UnlockDoorAction.cs"
    );

    private static readonly string PatrolActionPath = Path.Combine(
        RepoRoot,
        "src",
        "SPTQuestingBots.Client",
        "BotLogic",
        "Objective",
        "PatrolAction.cs"
    );

    private static readonly string PlantItemActionPath = Path.Combine(
        RepoRoot,
        "src",
        "SPTQuestingBots.Client",
        "BotLogic",
        "Objective",
        "PlantItemAction.cs"
    );

    private static readonly string SnipeActionPath = Path.Combine(
        RepoRoot,
        "src",
        "SPTQuestingBots.Client",
        "BotLogic",
        "Objective",
        "SnipeAction.cs"
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

    #region Bug 1: FindNearbyDoors door state parameter

    [Test]
    public void FindNearbyDoors_AcceptsDoorStateParameter()
    {
        // FindNearbyDoors should have an EDoorState parameter (with default EDoorState.Open)
        var source = ReadSource(CustomLogicPath);

        Assert.That(
            source,
            Does.Contain("FindNearbyDoors(float distance, EDoorState doorState"),
            "FindNearbyDoors should accept a doorState parameter so callers can request specific states"
        );
    }

    [Test]
    public void FindNearbyDoors_DefaultsToOpenState()
    {
        // The default value should remain EDoorState.Open for backward compatibility
        // (CloseNearbyDoorsAction depends on this)
        var source = ReadSource(CustomLogicPath);

        Assert.That(
            source,
            Does.Contain("EDoorState doorState = EDoorState.Open"),
            "FindNearbyDoors should default to EDoorState.Open for backward compatibility"
        );
    }

    [Test]
    public void IsNearAndMovingTowardClosedDoor_QueriesShutDoors()
    {
        // The method should query for Shut doors, not Open doors
        var source = ReadSource(CustomLogicPath);

        // Extract the IsNearAndMovingTowardClosedDoor method body
        var match = Regex.Match(
            source,
            @"public bool IsNearAndMovingTowardClosedDoor\(.*?\)\s*\{(.*?)\n        \}",
            RegexOptions.Singleline
        );
        Assert.That(match.Success, Is.True, "Could not extract IsNearAndMovingTowardClosedDoor method body");
        var methodBody = match.Groups[1].Value;

        Assert.That(
            methodBody,
            Does.Contain("EDoorState.Shut"),
            "IsNearAndMovingTowardClosedDoor should query for EDoorState.Shut doors, not Open"
        );
    }

    [Test]
    public void IsNearAndMovingTowardClosedDoor_DoesNotSkipOpenDoors()
    {
        // Previously the method filtered for Open doors then skipped them, finding nothing.
        // After the fix, it queries Shut doors directly and does not need to skip Open.
        var source = ReadSource(CustomLogicPath);

        var match = Regex.Match(
            source,
            @"public bool IsNearAndMovingTowardClosedDoor\(.*?\)\s*\{(.*?)\n        \}",
            RegexOptions.Singleline
        );
        Assert.That(match.Success, Is.True, "Could not extract method body");
        var methodBody = match.Groups[1].Value;

        Assert.That(
            methodBody,
            Does.Not.Contain("door.DoorState == EDoorState.Open"),
            "IsNearAndMovingTowardClosedDoor should not filter out Open doors from its results "
                + "(it should query Shut doors directly instead)"
        );
    }

    [Test]
    public void FindNearbyDoors_PassesDoorStateToLocationData()
    {
        // The doorState parameter should be forwarded to FindAllDoorsNearPosition
        var source = ReadSource(CustomLogicPath);

        var match = Regex.Match(source, @"public IEnumerable<Door> FindNearbyDoors\(.*?\)\s*\{(.*?)\n        \}", RegexOptions.Singleline);
        Assert.That(match.Success, Is.True, "Could not extract FindNearbyDoors method body");
        var methodBody = match.Groups[1].Value;

        Assert.That(
            methodBody,
            Does.Contain("doorState"),
            "FindNearbyDoors should forward the doorState parameter to FindAllDoorsNearPosition"
        );

        // It should NOT have a hardcoded EDoorState.Open anymore
        Assert.That(
            methodBody,
            Does.Not.Contain("EDoorState.Open"),
            "FindNearbyDoors body should use the doorState parameter, not a hardcoded EDoorState.Open"
        );
    }

    #endregion

    #region Bug 2: Shared static Random in action classes

    [Test]
    public void VultureAction_UsesSharedStaticRandom()
    {
        var source = ReadSource(VultureActionPath);

        Assert.That(
            source,
            Does.Contain("private static readonly System.Random SharedRandom"),
            "VultureAction should declare a shared static Random field"
        );
    }

    [Test]
    public void VultureAction_DoesNotCreateInlineRandom()
    {
        var source = ReadSource(VultureActionPath);

        // Check for inline construction (outside the static field initializer)
        int fieldIndex = source.IndexOf("private static readonly System.Random SharedRandom = new System.Random()");
        Assert.That(fieldIndex, Is.GreaterThan(-1), "SharedRandom field initializer should exist");

        // After the field declaration, there should be no other 'new System.Random()'
        string afterField = source.Substring(fieldIndex + 80);
        Assert.That(
            afterField,
            Does.Not.Contain("new System.Random()"),
            "VultureAction should not create inline System.Random instances — use SharedRandom instead"
        );
    }

    [Test]
    public void LingerAction_UsesSharedStaticRandom()
    {
        var source = ReadSource(LingerActionPath);

        Assert.That(
            source,
            Does.Contain("private static readonly System.Random SharedRandom"),
            "LingerAction should declare a shared static Random field"
        );
    }

    [Test]
    public void LingerAction_DoesNotCreateInlineRandom()
    {
        var source = ReadSource(LingerActionPath);

        int fieldIndex = source.IndexOf("private static readonly System.Random SharedRandom = new System.Random()");
        Assert.That(fieldIndex, Is.GreaterThan(-1), "SharedRandom field initializer should exist");

        string afterField = source.Substring(fieldIndex + 80);
        Assert.That(
            afterField,
            Does.Not.Contain("new System.Random()"),
            "LingerAction should not create inline System.Random instances — use SharedRandom instead"
        );
    }

    [Test]
    public void InvestigateAction_UsesSharedStaticRandom()
    {
        var source = ReadSource(InvestigateActionPath);

        Assert.That(
            source,
            Does.Contain("private static readonly System.Random SharedRandom"),
            "InvestigateAction should declare a shared static Random field"
        );
    }

    [Test]
    public void InvestigateAction_DoesNotCreateInlineRandom()
    {
        var source = ReadSource(InvestigateActionPath);

        int fieldIndex = source.IndexOf("private static readonly System.Random SharedRandom = new System.Random()");
        Assert.That(fieldIndex, Is.GreaterThan(-1), "SharedRandom field initializer should exist");

        string afterField = source.Substring(fieldIndex + 80);
        Assert.That(
            afterField,
            Does.Not.Contain("new System.Random()"),
            "InvestigateAction should not create inline System.Random instances — use SharedRandom instead"
        );
    }

    [Test]
    public void UnlockDoorAction_UsesSharedStaticRandom()
    {
        var source = ReadSource(UnlockDoorActionPath);

        Assert.That(
            source,
            Does.Contain("private static readonly System.Random SharedRandom"),
            "UnlockDoorAction should declare a shared static Random field"
        );
    }

    [Test]
    public void UnlockDoorAction_DoesNotCreateInlineRandom()
    {
        var source = ReadSource(UnlockDoorActionPath);

        int fieldIndex = source.IndexOf("private static readonly System.Random SharedRandom = new System.Random()");
        Assert.That(fieldIndex, Is.GreaterThan(-1), "SharedRandom field initializer should exist");

        string afterField = source.Substring(fieldIndex + 80);
        Assert.That(
            afterField,
            Does.Not.Contain("new System.Random()"),
            "UnlockDoorAction should not create inline System.Random instances — use SharedRandom instead"
        );
    }

    [Test]
    public void PatrolAction_AlreadyUsesSharedRandom()
    {
        // Verify PatrolAction was already correct (regression guard)
        var source = ReadSource(PatrolActionPath);

        Assert.That(
            source,
            Does.Contain("private static readonly System.Random SharedRandom"),
            "PatrolAction should continue using SharedRandom (pre-existing correct pattern)"
        );
    }

    [Test]
    public void AllActionFiles_NoInlineSystemRandomCreation()
    {
        // Comprehensive check: scan all action .cs files under BotLogic/Objective/
        // for `new System.Random()` that is NOT part of a static field initializer
        var objectiveDir = Path.Combine(RepoRoot, "src", "SPTQuestingBots.Client", "BotLogic", "Objective");

        foreach (string file in Directory.GetFiles(objectiveDir, "*Action.cs"))
        {
            var source = File.ReadAllText(file);
            var fileName = Path.GetFileName(file);

            // Count total occurrences of 'new System.Random()'
            int totalOccurrences = CountOccurrences(source, "new System.Random()");

            // Count field initializer occurrences (allowed)
            int fieldInitOccurrences = CountOccurrences(source, "static readonly System.Random SharedRandom = new System.Random()");

            Assert.That(
                totalOccurrences,
                Is.LessThanOrEqualTo(fieldInitOccurrences),
                fileName
                    + " contains inline 'new System.Random()' construction outside of a static field initializer. "
                    + "Total: "
                    + totalOccurrences
                    + ", field inits: "
                    + fieldInitOccurrences
            );
        }
    }

    #endregion

    #region Bug 3: AmbushAction timer counts travel time

    [Test]
    public void AmbushAction_RestartsTimerDuringTravel()
    {
        // AmbushAction should restart the action elapsed timer when the bot is NOT
        // close to its objective (traveling) so travel time doesn't count toward
        // the ambush wait duration. PlantItemAction already does this correctly.
        //
        // The Update method has TWO IsCloseToObjective checks: the first is for
        // steering (before canUpdate throttle), the second is in the throttled
        // section where path recalculation happens. We need the second one.
        var source = ReadSource(AmbushActionPath);
        string throttledSection = ExtractThrottledSection(source);

        // Find the "not close to objective" branch in the throttled section
        int notCloseIndex = throttledSection.IndexOf("!ObjectiveManager.IsCloseToObjective()");
        Assert.That(notCloseIndex, Is.GreaterThan(-1), "Should have IsCloseToObjective check in throttled section");

        // After the not-close check, RestartActionElapsedTime should appear before the return
        string afterNotClose = throttledSection.Substring(notCloseIndex);
        int returnIndex = afterNotClose.IndexOf("return;");
        Assert.That(returnIndex, Is.GreaterThan(-1), "Should have return after not-close branch");

        string travelBranch = afterNotClose.Substring(0, returnIndex);

        Assert.That(
            travelBranch,
            Does.Contain("RestartActionElapsedTime()"),
            "AmbushAction should call RestartActionElapsedTime() during travel to prevent "
                + "counting travel time toward the ambush wait duration"
        );
    }

    [Test]
    public void AmbushAction_ChecksMinTimeOnlyWhenCloseToObjective()
    {
        // CheckMinElapsedActionTime should NOT be called before the IsCloseToObjective check
        // in the throttled section. It should only be called when the bot is at the ambush position.
        var source = ReadSource(AmbushActionPath);
        string throttledSection = ExtractThrottledSection(source);

        int checkMinTimeIndex = throttledSection.IndexOf("CheckMinElapsedActionTime()");
        int closeToObjectiveIndex = throttledSection.IndexOf("!ObjectiveManager.IsCloseToObjective()");

        Assert.That(checkMinTimeIndex, Is.GreaterThan(-1), "CheckMinElapsedActionTime call should exist in throttled section");
        Assert.That(closeToObjectiveIndex, Is.GreaterThan(-1), "IsCloseToObjective check should exist in throttled section");

        Assert.That(
            checkMinTimeIndex,
            Is.GreaterThan(closeToObjectiveIndex),
            "CheckMinElapsedActionTime must be called AFTER the IsCloseToObjective check "
                + "(not before), so that elapsed time is only checked when the bot is at the "
                + "ambush position. Before the fix, it was called before the check, causing "
                + "bots to complete the objective during travel."
        );
    }

    [Test]
    public void PlantItemAction_AlsoRestartsTimerDuringTravel()
    {
        // Verify PlantItemAction already has the correct pattern (regression guard)
        var source = ReadSource(PlantItemActionPath);
        string throttledSection = ExtractThrottledSection(source);

        // The not-close branch should contain RestartActionElapsedTime
        int notCloseIndex = throttledSection.IndexOf("!ObjectiveManager.IsCloseToObjective()");
        Assert.That(notCloseIndex, Is.GreaterThan(-1), "PlantItemAction throttled section should have IsCloseToObjective check");

        string afterNotClose = throttledSection.Substring(notCloseIndex);
        int returnIndex = afterNotClose.IndexOf("return;");
        Assert.That(returnIndex, Is.GreaterThan(-1), "Should have return after not-close branch");

        string travelBranch = afterNotClose.Substring(0, returnIndex);

        Assert.That(
            travelBranch,
            Does.Contain("RestartActionElapsedTime()"),
            "PlantItemAction should continue to restart timer during travel (pre-existing correct pattern)"
        );
    }

    [Test]
    public void SnipeAction_InheritsAmbushAction()
    {
        // SnipeAction extends AmbushAction, so the AmbushAction fix applies to SnipeAction too
        var source = ReadSource(SnipeActionPath);

        Assert.That(source, Does.Contain(": AmbushAction"), "SnipeAction should inherit from AmbushAction and benefit from the timer fix");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Extracts the portion of the Update method AFTER the canUpdate() throttle check.
    /// This is the "expensive" section that runs at reduced frequency. Both AmbushAction
    /// and PlantItemAction have the pattern:
    ///   if (!canUpdate()) { return; }
    ///   // ... throttled logic ...
    /// We want to examine only the throttled section to avoid matching the per-frame
    /// steering code that also checks IsCloseToObjective.
    /// </summary>
    private static string ExtractThrottledSection(string source)
    {
        int canUpdateIndex = source.IndexOf("if (!canUpdate())");
        Assert.That(canUpdateIndex, Is.GreaterThan(-1), "Source should contain canUpdate() throttle check");

        // The throttled section starts after the "return;" following canUpdate()
        string afterThrottle = source.Substring(canUpdateIndex);
        int returnIndex = afterThrottle.IndexOf("return;");
        Assert.That(returnIndex, Is.GreaterThan(-1), "Should have return; after canUpdate check");

        return afterThrottle.Substring(returnIndex + "return;".Length);
    }

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
