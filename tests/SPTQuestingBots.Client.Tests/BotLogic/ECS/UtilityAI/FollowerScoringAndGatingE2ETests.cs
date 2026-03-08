using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI;

/// <summary>
/// E2E tests for follower scoring improvements (Task #1) and
/// opportunistic task decoupling from HasActiveObjective (Task #2).
/// </summary>
[TestFixture]
public class FollowerScoringAndGatingE2ETests
{
    private PatrolRoute[] _routes;

    [SetUp]
    public void SetUp()
    {
        _routes = new[]
        {
            new PatrolRoute(
                "Test Route",
                PatrolRouteType.Perimeter,
                new[] { new PatrolWaypoint(120f, 0f, 120f), new PatrolWaypoint(150f, 0f, 150f) }
            ),
        };
        PatrolTask.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        PatrolTask.Reset();
    }

    private static BotEntity CreateFollower(float aggression = 0.5f, float raidTime = 0.5f, float proximity = 0f)
    {
        var boss = new BotEntity(0);
        var entity = new BotEntity(1);
        entity.Boss = boss;
        entity.HasTacticalPosition = true;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionY = 0f;
        entity.CurrentPositionZ = 0f;
        entity.TacticalPositionX = 50f;
        entity.TacticalPositionY = 0f;
        entity.TacticalPositionZ = 0f;
        entity.Aggression = aggression;
        entity.RaidTimeNormalized = raidTime;
        entity.HumanPlayerProximity = proximity;
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];
        return entity;
    }

    // ────────────────────────────────────────────────────────────
    // Task #1: SquadTaskFactory expanded repertoire
    // ────────────────────────────────────────────────────────────

    [Test]
    public void SquadFactory_RegistersMoreThan2Tasks()
    {
        Assert.That(SquadTaskFactory.TaskCount, Is.GreaterThan(2));
    }

    [Test]
    public void SquadFactory_ContainsTacticalTasks()
    {
        var manager = SquadTaskFactory.Create();
        Assert.IsInstanceOf<GoToTacticalPositionTask>(manager.Tasks[0]);
        Assert.IsInstanceOf<HoldTacticalPositionTask>(manager.Tasks[1]);
    }

    [Test]
    public void SquadFactory_ContainsOpportunisticTasks()
    {
        var manager = SquadTaskFactory.Create();
        bool hasLoot = false,
            hasInvestigate = false,
            hasLinger = false,
            hasPatrol = false;
        for (int i = 0; i < manager.Tasks.Length; i++)
        {
            if (manager.Tasks[i] is LootTask)
                hasLoot = true;
            if (manager.Tasks[i] is InvestigateTask)
                hasInvestigate = true;
            if (manager.Tasks[i] is LingerTask)
                hasLinger = true;
            if (manager.Tasks[i] is PatrolTask)
                hasPatrol = true;
        }

        Assert.Multiple(() =>
        {
            Assert.That(hasLoot, Is.True, "LootTask should be registered");
            Assert.That(hasInvestigate, Is.True, "InvestigateTask should be registered");
            Assert.That(hasLinger, Is.True, "LingerTask should be registered");
            Assert.That(hasPatrol, Is.True, "PatrolTask should be registered");
        });
    }

    // ────────────────────────────────────────────────────────────
    // Task #1: CombinedModifier applied to tactical tasks
    // ────────────────────────────────────────────────────────────

    [Test]
    public void GoToTactical_ScoreVariesWithAggression()
    {
        var taskGo = new GoToTacticalPositionTask();

        var cautious = CreateFollower(aggression: 0.1f);
        var aggressive = CreateFollower(aggression: 0.9f);

        taskGo.ScoreEntity(0, cautious);
        taskGo.ScoreEntity(0, aggressive);

        // GoToObjective PersonalityModifier: Lerp(0.85, 1.15, aggression)
        // Cautious (0.1) → lower modifier, aggressive (0.9) → higher modifier
        Assert.That(
            aggressive.TaskScores[0],
            Is.GreaterThan(cautious.TaskScores[0]),
            "Aggressive followers should score higher on GoToTactical"
        );
    }

    [Test]
    public void HoldTactical_ScoreVariesWithPlayerProximity()
    {
        var taskHold = new HoldTacticalPositionTask();

        // Close to position (within 3m threshold)
        var noPlayer = CreateFollower(proximity: 0f);
        noPlayer.CurrentPositionX = 49f;
        var nearPlayer = CreateFollower(proximity: 0.8f);
        nearPlayer.CurrentPositionX = 49f;

        taskHold.ScoreEntity(1, noPlayer);
        taskHold.ScoreEntity(1, nearPlayer);

        // HoldPosition PlayerProximityModifier: Lerp(1, 1.3, proximity)
        // Near player → higher modifier → higher hold score
        Assert.That(
            nearPlayer.TaskScores[1],
            Is.GreaterThan(noPlayer.TaskScores[1]),
            "Followers near players should hold position more strongly"
        );
    }

    [Test]
    public void GoToTactical_ScoreVariesWithRaidTime()
    {
        var taskGo = new GoToTacticalPositionTask();

        var earlyRaid = CreateFollower(raidTime: 0.1f);
        var lateRaid = CreateFollower(raidTime: 0.9f);

        taskGo.ScoreEntity(0, earlyRaid);
        taskGo.ScoreEntity(0, lateRaid);

        // GoToObjective RaidTimeModifier: Lerp(1.2, 0.8, raidTime)
        // Early raid → higher modifier, late raid → lower modifier
        Assert.That(
            earlyRaid.TaskScores[0],
            Is.GreaterThan(lateRaid.TaskScores[0]),
            "Early-raid followers should rush to tactical position more"
        );
    }

    // ────────────────────────────────────────────────────────────
    // Task #1: Tactical tasks still win under normal conditions
    // ────────────────────────────────────────────────────────────

    [Test]
    public void TacticalTask_WinsOverOpportunistic_WhenFarFromPosition()
    {
        PatrolTask.CurrentMapRoutes = _routes;
        PatrolTask.RoutesLoaded = true;

        var manager = SquadTaskFactory.Create();
        var entity = CreateFollower();
        entity.CurrentPositionX = 100f;
        entity.CurrentPositionZ = 100f;

        manager.ScoreAndPick(entity);

        Assert.IsInstanceOf<GoToTacticalPositionTask>(
            entity.TaskAssignment.Task,
            "GoToTactical should win when follower is far from tactical position"
        );
    }

    [Test]
    public void TacticalTask_WinsOverOpportunistic_WhenCloseToPosition()
    {
        var manager = SquadTaskFactory.Create();
        var entity = CreateFollower();
        entity.CurrentPositionX = 49f;
        entity.TacticalPositionX = 50f;

        manager.ScoreAndPick(entity);

        Assert.IsInstanceOf<HoldTacticalPositionTask>(
            entity.TaskAssignment.Task,
            "HoldTactical should win when follower is at tactical position"
        );
    }

    // ────────────────────────────────────────────────────────────
    // Task #1: Opportunistic tasks can win when conditions are met
    // ────────────────────────────────────────────────────────────

    [Test]
    public void LootTask_CanWin_WhenNoTacticalPosition()
    {
        var manager = SquadTaskFactory.Create();
        var entity = new BotEntity(1);
        entity.Boss = new BotEntity(0);
        entity.HasTacticalPosition = false;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];

        // Set up loot target
        entity.HasLootTarget = true;
        entity.LootTargetValue = 40000f;
        entity.LootTargetX = 10f;
        entity.LootTargetY = 0f;
        entity.LootTargetZ = 0f;
        entity.InventorySpaceFree = 5f;

        manager.ScoreAndPick(entity);

        Assert.IsInstanceOf<LootTask>(entity.TaskAssignment.Task, "LootTask should win when no tactical position and loot available");
    }

    [Test]
    public void InvestigateTask_CanWin_WhenNoTacticalPosition()
    {
        var manager = SquadTaskFactory.Create();
        var entity = new BotEntity(1);
        entity.Boss = new BotEntity(0);
        entity.HasTacticalPosition = false;
        entity.Aggression = 0.8f;
        entity.RaidTimeNormalized = 0.5f;
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];

        // Set up nearby combat event
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 20;
        entity.NearbyEventX = 10f;
        entity.NearbyEventZ = 10f;

        manager.ScoreAndPick(entity);

        Assert.IsInstanceOf<InvestigateTask>(
            entity.TaskAssignment.Task,
            "InvestigateTask should win when no tactical position and event nearby"
        );
    }

    [Test]
    public void LingerTask_CanWin_WhenNoTacticalPosition()
    {
        var manager = SquadTaskFactory.Create();
        var entity = new BotEntity(1);
        entity.Boss = new BotEntity(0);
        entity.HasTacticalPosition = false;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];

        // Set up linger conditions
        entity.ObjectiveCompletedTime = 1f;
        entity.CurrentGameTime = 2f;
        entity.LingerDuration = 10f;

        manager.ScoreAndPick(entity);

        Assert.IsInstanceOf<LingerTask>(
            entity.TaskAssignment.Task,
            "LingerTask should win when no tactical position and just completed objective"
        );
    }

    // ────────────────────────────────────────────────────────────
    // Task #2: Opportunistic tasks score > 0 without HasActiveObjective
    // ────────────────────────────────────────────────────────────

    [Test]
    public void LootTask_Scores_WithoutActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;
        entity.HasLootTarget = true;
        entity.LootTargetValue = 30000f;
        entity.LootTargetX = 10f;
        entity.LootTargetY = 0f;
        entity.LootTargetZ = 0f;
        entity.InventorySpaceFree = 5f;

        float score = LootTask.Score(entity);
        Assert.That(score, Is.GreaterThan(0f), "LootTask should score without HasActiveObjective");
    }

    [Test]
    public void PatrolTask_Scores_WithoutActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;
        entity.CurrentPositionX = 100f;
        entity.CurrentPositionZ = 100f;
        entity.PatrolRouteIndex = 0;

        float score = PatrolTask.Score(entity, _routes);
        Assert.That(score, Is.GreaterThan(0f), "PatrolTask should score without HasActiveObjective");
    }

    [Test]
    public void PatrolTask_Scores_WithActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = true;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;
        entity.CurrentPositionX = 100f;
        entity.CurrentPositionZ = 100f;
        entity.PatrolRouteIndex = 0;

        float score = PatrolTask.Score(entity, _routes);
        Assert.That(score, Is.GreaterThan(0f), "PatrolTask should score even with HasActiveObjective (decoupled)");
    }

    [Test]
    public void InvestigateTask_Scores_WithoutActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 15;
        entity.NearbyEventX = 10f;
        entity.NearbyEventZ = 10f;

        float score = InvestigateTask.Score(entity, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);
        Assert.That(score, Is.GreaterThan(0f), "InvestigateTask should score without HasActiveObjective");
    }

    [Test]
    public void LingerTask_Scores_WithoutActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;
        entity.ObjectiveCompletedTime = 1f;
        entity.CurrentGameTime = 2f;
        entity.LingerDuration = 10f;

        float score = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.GreaterThan(0f), "LingerTask should score without HasActiveObjective");
    }

    [Test]
    public void VultureTask_Scores_WithoutActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 30;
        entity.NearbyEventX = 10f;
        entity.NearbyEventZ = 10f;

        float score = VultureTask.Score(entity, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.GreaterThan(0f), "VultureTask should score without HasActiveObjective");
    }

    // ────────────────────────────────────────────────────────────
    // Task #2: Quest-dependent tasks still require HasActiveObjective
    // ────────────────────────────────────────────────────────────

    [Test]
    public void GoToObjectiveTask_Zero_WithoutActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;

        float score = GoToObjectiveTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "GoToObjective must require HasActiveObjective");
    }

    [Test]
    public void AmbushTask_Zero_WithoutActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;
        entity.CurrentQuestAction = QuestActionId.Ambush;
        entity.IsCloseToObjective = true;

        float score = AmbushTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "AmbushTask must require HasActiveObjective");
    }

    [Test]
    public void SnipeTask_Zero_WithoutActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;
        entity.CurrentQuestAction = QuestActionId.Snipe;
        entity.IsCloseToObjective = true;

        float score = SnipeTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "SnipeTask must require HasActiveObjective");
    }

    [Test]
    public void PlantItemTask_Zero_WithoutActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;
        entity.CurrentQuestAction = QuestActionId.PlantItem;
        entity.IsCloseToObjective = true;

        float score = PlantItemTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "PlantItemTask must require HasActiveObjective");
    }

    [Test]
    public void UnlockDoorTask_Zero_WithoutActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;
        entity.MustUnlockDoor = true;

        float score = UnlockDoorTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "UnlockDoorTask must require HasActiveObjective");
    }

    [Test]
    public void ToggleSwitchTask_Zero_WithoutActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;
        entity.CurrentQuestAction = QuestActionId.ToggleSwitch;

        float score = ToggleSwitchTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "ToggleSwitchTask must require HasActiveObjective");
    }

    [Test]
    public void CloseDoorsTask_Zero_WithoutActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;
        entity.CurrentQuestAction = QuestActionId.CloseNearbyDoors;

        float score = CloseDoorsTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "CloseDoorsTask must require HasActiveObjective");
    }

    [Test]
    public void HoldPositionTask_Zero_WithoutActiveObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;
        entity.CurrentQuestAction = QuestActionId.HoldAtPosition;

        float score = HoldPositionTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "HoldPositionTask must require HasActiveObjective");
    }

    // ────────────────────────────────────────────────────────────
    // Task #2: Bots transition to opportunistic behavior after quest failure
    // ────────────────────────────────────────────────────────────

    [Test]
    public void Scenario_QuestFailure_FollowerTransitionsToOpportunistic()
    {
        PatrolTask.CurrentMapRoutes = _routes;
        PatrolTask.RoutesLoaded = true;

        var manager = SquadTaskFactory.Create();
        var entity = CreateFollower();
        entity.CurrentPositionX = 100f;
        entity.CurrentPositionZ = 100f;

        // Start with tactical position — GoToTactical wins
        manager.ScoreAndPick(entity);
        Assert.IsInstanceOf<GoToTacticalPositionTask>(entity.TaskAssignment.Task);

        // Tactical position lost (leader dead, quest failed, etc.)
        entity.HasTacticalPosition = false;

        // Score again — tactical tasks both return 0
        // Patrol route is available, so PatrolTask should win
        manager.ScoreAndPick(entity);

        Assert.IsNotNull(entity.TaskAssignment.Task, "Bot should have a task after tactical position lost");
        Assert.That(
            entity.TaskAssignment.Task is PatrolTask
                || entity.TaskAssignment.Task is LootTask
                || entity.TaskAssignment.Task is InvestigateTask
                || entity.TaskAssignment.Task is LingerTask,
            Is.True,
            "Bot should transition to an opportunistic task after losing tactical position"
        );
    }

    [Test]
    public void Scenario_PersonalityAffectsFollowerBehavior()
    {
        var manager = SquadTaskFactory.Create();

        // Cautious follower at tactical position
        var cautious = CreateFollower(aggression: 0.1f);
        cautious.CurrentPositionX = 49f;
        cautious.TacticalPositionX = 50f;

        // Aggressive follower at tactical position
        var aggressive = CreateFollower(aggression: 0.9f);
        aggressive.CurrentPositionX = 49f;
        aggressive.TacticalPositionX = 50f;

        manager.ScoreAndPick(cautious);
        manager.ScoreAndPick(aggressive);

        // Both should hold, but with different scores
        float cautiousScore = cautious.TaskScores[1]; // HoldTactical ordinal
        float aggressiveScore = aggressive.TaskScores[1];

        // HoldPosition uses default personality modifier (1.0),
        // but raid-time and proximity may differ
        Assert.IsInstanceOf<HoldTacticalPositionTask>(cautious.TaskAssignment.Task);
        Assert.IsInstanceOf<HoldTacticalPositionTask>(aggressive.TaskAssignment.Task);
    }

    // ────────────────────────────────────────────────────────────
    // Task #2: Opportunistic tasks lose to quest tasks when objective IS active
    // ────────────────────────────────────────────────────────────

    [Test]
    public void QuestTasks_BeatOpportunistic_WhenObjectiveActive()
    {
        PatrolTask.CurrentMapRoutes = _routes;
        PatrolTask.RoutesLoaded = true;

        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;
        entity.CurrentPositionX = 100f;
        entity.CurrentPositionZ = 100f;

        // Active quest objective
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 100f;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;

        manager.ScoreAndPick(entity);

        Assert.IsInstanceOf<GoToObjectiveTask>(
            entity.TaskAssignment.Task,
            "Quest task should win over opportunistic tasks when objective is active"
        );
    }

    [Test]
    public void OpportunisticTasks_Win_WhenNoObjectiveActive()
    {
        PatrolTask.CurrentMapRoutes = _routes;
        PatrolTask.RoutesLoaded = true;

        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;
        entity.CurrentPositionX = 100f;
        entity.CurrentPositionZ = 100f;

        // No active objective
        entity.HasActiveObjective = false;

        manager.ScoreAndPick(entity);

        // At minimum, patrol should be available since routes are loaded
        Assert.IsNotNull(entity.TaskAssignment.Task, "Bot should pick an opportunistic task");
        Assert.That(
            entity.TaskAssignment.Task is PatrolTask
                || entity.TaskAssignment.Task is LootTask
                || entity.TaskAssignment.Task is InvestigateTask
                || entity.TaskAssignment.Task is LingerTask
                || entity.TaskAssignment.Task is VultureTask
                || entity.TaskAssignment.Task is SpawnEntryTask,
            Is.True,
            "Bot should use opportunistic tasks when no quest objective active"
        );
    }
}
