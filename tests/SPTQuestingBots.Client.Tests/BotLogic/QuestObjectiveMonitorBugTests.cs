using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic;

/// <summary>
/// Bug-catching tests for Quest/Objective system, BotMonitors, and Dynamic Objectives.
/// Uses source-scanning for Unity-dependent code and direct tests for pure C# logic.
/// </summary>
[TestFixture]
public class QuestObjectiveMonitorBugTests
{
    private static readonly string SrcRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "SPTQuestingBots.Client")
    );

    // ── Bug 1: GetQuestObjectivesNearPosition null-guard on GetFirstStepPosition ──

    [Test]
    public void BotJobAssignmentFactory_GetQuestObjectivesNearPosition_GuardsNullFirstStepPosition()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Controllers", "BotJobAssignmentFactory.cs"));

        // The fix adds a null check before accessing .Value
        Assert.That(
            source,
            Does.Contain("firstPos.HasValue"),
            "GetQuestObjectivesNearPosition must null-check GetFirstStepPosition() before accessing .Value"
        );

        // Verify it does NOT directly call .GetFirstStepPosition().Value in the same statement
        Assert.That(
            source,
            Does.Not.Contain("objective.GetFirstStepPosition().Value"),
            "GetQuestObjectivesNearPosition must not directly dereference GetFirstStepPosition().Value"
        );
    }

    // ── Bug 2: JobAssignment.ToString null-guard on QuestAssignment ──

    [Test]
    public void JobAssignment_ToString_GuardsNullQuestAssignment()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Models", "Questing", "JobAssignment.cs"));

        // The fix uses QuestAssignment?.Name ?? "???"
        Assert.That(
            source,
            Does.Contain("QuestAssignment?.Name"),
            "JobAssignment.ToString must use null-conditional on QuestAssignment.Name"
        );
    }

    // ── Bug 3: ConfigController.TryDeserializeObject null-guard on json ──

    [Test]
    public void ConfigController_TryDeserializeObject_GuardsNullJson()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Controllers", "ConfigController.cs"));

        Assert.That(
            source,
            Does.Contain("string.IsNullOrEmpty(json)"),
            "TryDeserializeObject must check for null/empty json using string.IsNullOrEmpty"
        );

        Assert.That(
            source,
            Does.Not.Contain("json.Length == 0"),
            "TryDeserializeObject must not use json.Length == 0 which throws on null"
        );
    }

    // ── Bug 4: GetUSECChance - must only cache on successful deserialization ──

    [Test]
    public void ConfigController_GetUSECChance_OnlyCachesOnSuccess()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Controllers", "ConfigController.cs"));

        // The fix wraps the cache assignment in an if(TryDeserializeObject) block
        Assert.That(
            source,
            Does.Contain("if (TryDeserializeObject(json, errorMessage, out Configuration.USECChanceResponse _usecChance))"),
            "GetUSECChance must only cache USECChance when deserialization succeeds"
        );
    }

    // ── Bug 5: BotHearingMonitor.shouldIgnoreSound - default case for non-standard sounds ──

    [Test]
    public void BotHearingMonitor_ShouldIgnoreSound_HasDefaultCase()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "BotHearingMonitor.cs"));

        Assert.That(
            source,
            Does.Contain("default:"),
            "shouldIgnoreSound switch must have a default case to handle non-standard sound types"
        );
    }

    // ── Bug 6: RemoveBlacklistedQuestObjectives - empty quest check moved outside inner loop ──

    [Test]
    public void BotJobAssignmentFactory_RemoveBlacklisted_EmptyQuestCheckOutsideInnerLoop()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Controllers", "BotJobAssignmentFactory.cs"));

        // Extract the RemoveBlacklistedQuestObjectives method body
        int methodStart = source.IndexOf("public static void RemoveBlacklistedQuestObjectives");
        Assert.That(methodStart, Is.GreaterThan(-1), "Method must exist");

        // Find the inner foreach end and the empty-quest check
        string methodArea = source.Substring(methodStart, 3500);

        // The NumberOfObjectives == 0 check should come AFTER the inner foreach closing brace,
        // not inside it. We verify the check is NOT preceded by "continue;" or other inner-loop
        // patterns within 50 chars.
        int emptyCheck = methodArea.IndexOf("quest.NumberOfObjectives == 0");
        Assert.That(emptyCheck, Is.GreaterThan(-1), "Empty quest check must exist");

        // Count open/close braces before the check to determine nesting depth
        // The fix moves this check to be at the same nesting level as the inner foreach,
        // not inside it.
        string beforeCheck = methodArea.Substring(0, emptyCheck);
        int innerForeachStart = beforeCheck.LastIndexOf("foreach (QuestObjective objective");
        Assert.That(innerForeachStart, Is.GreaterThan(-1), "Inner foreach must exist before check");

        string betweenForeachAndCheck = beforeCheck.Substring(innerForeachStart);
        int openBraces = betweenForeachAndCheck.Count(c => c == '{');
        int closeBraces = betweenForeachAndCheck.Count(c => c == '}');

        // The inner foreach opens one brace. If the check is outside the inner foreach,
        // close braces should >= open braces (inner foreach is closed before the check).
        Assert.That(
            closeBraces,
            Is.GreaterThanOrEqualTo(openBraces),
            "Empty quest check must be outside the inner foreach loop (braces should be balanced)"
        );
    }

    // ── Bug 7: TimeWhenBotStarted returns StartTime, not EndTime ──

    [Test]
    public void BotJobAssignmentFactory_TimeWhenBotStarted_ReturnsStartTime()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Controllers", "BotJobAssignmentFactory.cs"));

        // Find the TimeWhenBotStarted method
        int methodStart = source.IndexOf("TimeWhenBotStarted");
        Assert.That(methodStart, Is.GreaterThan(-1), "TimeWhenBotStarted method must exist");

        string methodArea = source.Substring(methodStart, 700);

        Assert.That(
            methodArea,
            Does.Contain("matchingAssignments.Last().StartTime"),
            "TimeWhenBotStarted must return StartTime, not EndTime"
        );
        Assert.That(methodArea, Does.Not.Contain("matchingAssignments.Last().EndTime"), "TimeWhenBotStarted must not return EndTime");
    }

    // ── Bug 8: BotObjectiveManager.HasCompletePath null-guard ──

    [Test]
    public void BotObjectiveManager_HasCompletePath_GuardsNullAssignment()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Components", "BotObjectiveManager.cs"));

        Assert.That(source, Does.Contain("assignment?.HasCompletePath"), "HasCompletePath must use null-conditional on assignment");
    }

    // ── Bug 9: BotObjectiveManager methods null-guard assignment ──

    [Test]
    public void BotObjectiveManager_StartJobAssignment_GuardsNullAssignment()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Components", "BotObjectiveManager.cs"));

        Assert.That(source, Does.Contain("assignment?.Start()"), "StartJobAssigment must use null-conditional on assignment");
    }

    [Test]
    public void BotObjectiveManager_ReportIncompletePath_GuardsNullAssignment()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Components", "BotObjectiveManager.cs"));

        Assert.That(
            source,
            Does.Contain("if (assignment != null)").And.Contain("assignment.HasCompletePath = false"),
            "ReportIncompletePath must guard against null assignment"
        );
    }

    [Test]
    public void BotObjectiveManager_ToString_GuardsNullAssignment()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Components", "BotObjectiveManager.cs"));

        Assert.That(source, Does.Contain("assignment?.QuestAssignment"), "ToString must use null-conditional on assignment");
    }

    // ── Bug 10: QuestHelpers.LocateQuestItems - null/empty target guard ──

    [Test]
    public void QuestHelpers_LocateQuestItems_GuardsNullTarget()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Helpers", "QuestHelpers.cs"));

        Assert.That(
            source,
            Does.Contain("conditionFindItem?.target != null && conditionFindItem.target.Length > 0"),
            "LocateQuestItems must guard against null/empty target array before indexing"
        );
    }

    // ── CombatEventClustering: centroid calculation with zero members (structural) ──

    [Test]
    public void ClusterEvents_AlwaysHasAtLeastOneMember_NoDivByZero()
    {
        // A cluster is always seeded by at least one event, so memberCount >= 1.
        // Verify by clustering one event and checking valid centroid.
        var events = new[]
        {
            new CombatEvent
            {
                X = 100f,
                Y = 10f,
                Z = 200f,
                Time = 5f,
                Power = 100f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
        };
        var output = new CombatEventClustering.ClusterResult[10];
        int count = CombatEventClustering.ClusterEvents(events, 1, 10f, 120f, 2500f, output, 10);

        Assert.That(count, Is.EqualTo(1));
        Assert.That(float.IsNaN(output[0].X), Is.False, "Centroid X must not be NaN");
        Assert.That(float.IsNaN(output[0].Y), Is.False, "Centroid Y must not be NaN");
        Assert.That(float.IsNaN(output[0].Z), Is.False, "Centroid Z must not be NaN");
    }

    // ── QuestScorer: division by zero when maxOverallDistance is 0 ──

    [Test]
    public void QuestScorer_ZeroMaxDistance_NoNaN()
    {
        var config = new QuestScoringConfig(1f, 1f, 1f, 0, 0, 45f, 1f);
        var rng = new System.Random(42);

        double score = QuestScorer.ScoreQuest(50f, 0f, 0, 50f, false, 30f, config, rng);

        Assert.That(double.IsNaN(score), Is.False, "Score must not be NaN when maxOverallDistance is 0");
        Assert.That(double.IsInfinity(score), Is.False, "Score must not be Infinity when maxOverallDistance is 0");
    }

    // ── QuestScorer: exfil angle division by zero when MaxExfilAngle == 180 ──

    [Test]
    public void QuestScorer_MaxExfilAngle180_NoDivByZero()
    {
        var config = new QuestScoringConfig(0f, 0f, 1f, 0, 0, 180f, 1f);
        var rng = new System.Random(42);

        double score = QuestScorer.ScoreQuest(50f, 100f, 0, 50f, false, 180f, config, rng);

        Assert.That(double.IsNaN(score), Is.False, "Score must not be NaN with MaxExfilAngle=180");
        Assert.That(double.IsInfinity(score), Is.False, "Score must not be Infinity with MaxExfilAngle=180");
    }

    // ── QuestScorer: negative count returns -1 ──

    [Test]
    public void QuestScorer_NegativeCount_ReturnsNegativeOne()
    {
        var scores = new[] { 1.0, 2.0, 3.0 };
        Assert.That(QuestScorer.SelectHighestIndex(scores, -1), Is.EqualTo(-1));
    }

    // ── DynamicObjectiveGenerator structural: null/empty guards ──

    [Test]
    public void DynamicObjectiveGenerator_GenerateFirefight_GuardsNullAndEmpty()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "ECS", "Systems", "DynamicObjectiveGenerator.cs"));

        Assert.That(
            source,
            Does.Contain("events == null || eventCount <= 0 || maxQuests <= 0"),
            "GenerateFirefightObjectives must guard null events, zero count, and zero maxQuests"
        );
    }

    [Test]
    public void DynamicObjectiveGenerator_GenerateCorpse_GuardsNullAndEmpty()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "ECS", "Systems", "DynamicObjectiveGenerator.cs"));

        // The method GenerateCorpseObjectives should have the same guard
        int methodStart = source.IndexOf("GenerateCorpseObjectives");
        Assert.That(methodStart, Is.GreaterThan(-1));
        string methodArea = source.Substring(methodStart, 500);

        Assert.That(
            methodArea,
            Does.Contain("events == null || eventCount <= 0 || maxQuests <= 0"),
            "GenerateCorpseObjectives must guard null events, zero count, and zero maxQuests"
        );
    }

    [Test]
    public void DynamicObjectiveGenerator_GenerateBuildingClear_GuardsNullAndEmpty()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "ECS", "Systems", "DynamicObjectiveGenerator.cs"));

        int methodStart = source.IndexOf("GenerateBuildingClearObjectives");
        Assert.That(methodStart, Is.GreaterThan(-1));
        string methodArea = source.Substring(methodStart, 500);

        Assert.That(
            methodArea,
            Does.Contain("indoorPositions == null || positionCount <= 0 || maxQuests <= 0"),
            "GenerateBuildingClearObjectives must guard null positions, zero count, and zero maxQuests"
        );
    }

    // ── CombatEventClustering: edge cases ──

    [Test]
    public void ClusterEvents_AllExpired_ReturnsZero()
    {
        var events = new[]
        {
            new CombatEvent
            {
                X = 100f,
                Y = 0f,
                Z = 200f,
                Time = 1f,
                Power = 100f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 300f,
                Y = 0f,
                Z = 400f,
                Time = 2f,
                Power = 100f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
        };
        var output = new CombatEventClustering.ClusterResult[10];
        // currentTime=200, maxAge=10 -> all events are expired (age > 10)
        int count = CombatEventClustering.ClusterEvents(events, 2, 200f, 10f, 2500f, output, 10);
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void ClusterEvents_AllInactive_ReturnsZero()
    {
        var events = new[]
        {
            new CombatEvent
            {
                X = 100f,
                Y = 0f,
                Z = 200f,
                Time = 5f,
                Power = 100f,
                Type = CombatEventType.Gunshot,
                IsActive = false,
            },
        };
        var output = new CombatEventClustering.ClusterResult[10];
        int count = CombatEventClustering.ClusterEvents(events, 1, 10f, 120f, 2500f, output, 10);
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void ClusterEvents_AllDeaths_ReturnsZero()
    {
        var events = new[]
        {
            new CombatEvent
            {
                X = 100f,
                Y = 0f,
                Z = 200f,
                Time = 5f,
                Power = 50f,
                Type = CombatEventType.Death,
                IsActive = true,
            },
        };
        var output = new CombatEventClustering.ClusterResult[10];
        int count = CombatEventClustering.ClusterEvents(events, 1, 10f, 120f, 2500f, output, 10);
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void ClusterEvents_NullOutputBuffer_ReturnsZero()
    {
        var events = new[]
        {
            new CombatEvent
            {
                X = 100f,
                Y = 0f,
                Z = 200f,
                Time = 5f,
                Power = 100f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
        };
        int count = CombatEventClustering.ClusterEvents(events, 1, 10f, 120f, 2500f, null, 10);
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void FilterDeathEvents_NullOutputBuffer_ReturnsZero()
    {
        var events = new[]
        {
            new CombatEvent
            {
                X = 100f,
                Y = 0f,
                Z = 200f,
                Time = 5f,
                Power = 50f,
                Type = CombatEventType.Death,
                IsActive = true,
            },
        };
        int count = CombatEventClustering.FilterDeathEvents(events, 1, 10f, 120f, null);
        Assert.That(count, Is.EqualTo(0));
    }

    // ── BotMonitorController: decision monitor structural checks ──

    [Test]
    public void BotQuestingDecisionMonitor_HasInvestigateTypo_Preserved()
    {
        // The enum value is "Investigtate" (typo), verify it exists so we don't accidentally break references
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "BotMonitorController.cs"));
        // The decision monitor is in a separate file
        var decisionSource = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "BotQuestingDecisionMonitor.cs"));
        Assert.That(
            decisionSource,
            Does.Contain("Investigtate"),
            "BotQuestingDecision.Investigtate enum value must exist (known typo, preserved for compatibility)"
        );
    }

    // ── BotMonitor: AbstractBotMonitor has BotOwner, ObjectiveManager, BotMonitor refs ──

    [Test]
    public void AbstractBotMonitor_ExposesRequiredDependencies()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "AbstractBotMonitor.cs"));

        Assert.That(source, Does.Contain("BotOwner BotOwner"), "Must expose BotOwner");
        Assert.That(source, Does.Contain("BotObjectiveManager ObjectiveManager"), "Must expose ObjectiveManager");
        Assert.That(source, Does.Contain("BotMonitorController BotMonitor"), "Must expose BotMonitorController");
    }

    // ── QuestHelpers.ClearCache is called at raid start ──

    [Test]
    public void QuestHelpers_ClearCache_CalledAtRaidStart()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Components", "LocationData.cs"));

        Assert.That(
            source,
            Does.Contain("QuestHelpers.ClearCache()"),
            "QuestHelpers.ClearCache must be called during location initialization"
        );
    }

    // ── BotQuestingMonitor: follower distance check filters dead followers ──

    [Test]
    public void BotQuestingMonitor_ShouldWaitForFollowers_FiltersDead()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "BotQuestingMonitor.cs"));

        Assert.That(source, Does.Contain("!f.IsDead"), "shouldWaitForFollowers must filter out dead followers");
    }
}
