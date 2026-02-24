using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.DataFlow;

/// <summary>
/// Tests that verify score value bounds, modifier multiplication chains,
/// and hysteresis behavior across the utility AI scoring pipeline.
/// </summary>
[TestFixture]
public class ScoreBoundsTests
{
    // ── CombinedModifier Bounds ──────────────────────────────────

    [Test]
    public void CombinedModifier_MaxAggressionMaxTime_ClampedToMaxBound()
    {
        // Linger has the highest theoretical range: PersonalityModifier [0.7, 1.3] * RaidTimeModifier [0.7, 1.3]
        // Worst case: 1.3 * 1.3 = 1.69 → should be clamped to MaxCombinedModifier
        float result = ScoringModifiers.CombinedModifier(0f, 1f, BotActionTypeId.Linger);

        // After fix: clamped to MaxCombinedModifier (1.5)
        Assert.That(result, Is.LessThanOrEqualTo(ScoringModifiers.MaxCombinedModifier));
    }

    [Test]
    public void CombinedModifier_AllTaskTypes_NeverExceedMax()
    {
        int[] actionTypes = new[]
        {
            BotActionTypeId.GoToObjective,
            BotActionTypeId.Ambush,
            BotActionTypeId.Snipe,
            BotActionTypeId.Loot,
            BotActionTypeId.Vulture,
            BotActionTypeId.Investigate,
            BotActionTypeId.Linger,
            BotActionTypeId.Patrol,
        };

        // Sweep all aggression x raidTime combinations at 0.1 increments
        for (float aggression = 0f; aggression <= 1f; aggression += 0.1f)
        {
            for (float raidTime = 0f; raidTime <= 1f; raidTime += 0.1f)
            {
                foreach (int actionType in actionTypes)
                {
                    float modifier = ScoringModifiers.CombinedModifier(aggression, raidTime, actionType);
                    Assert.That(
                        modifier,
                        Is.LessThanOrEqualTo(ScoringModifiers.MaxCombinedModifier),
                        $"CombinedModifier({aggression:F1}, {raidTime:F1}, {actionType}) = {modifier} exceeds max"
                    );
                    Assert.That(
                        modifier,
                        Is.GreaterThanOrEqualTo(0f),
                        $"CombinedModifier({aggression:F1}, {raidTime:F1}, {actionType}) = {modifier} is negative"
                    );
                }
            }
        }
    }

    [Test]
    public void CombinedModifier_NaN_ReturnsOne()
    {
        float result = ScoringModifiers.CombinedModifier(float.NaN, 0.5f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.EqualTo(1.0f));
    }

    [Test]
    public void CombinedModifier_NegativeAggression_ClampsToZero()
    {
        // Negative aggression should be clamped to 0 inside PersonalityModifier
        float result = ScoringModifiers.CombinedModifier(-1f, 0.5f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.GreaterThan(0f));
        Assert.That(result, Is.LessThanOrEqualTo(ScoringModifiers.MaxCombinedModifier));
    }

    [Test]
    public void CombinedModifier_ExtremeValues_NeverNaN()
    {
        float[] extremes = { float.MinValue, -1000f, -1f, 0f, 1f, 1000f, float.MaxValue, float.PositiveInfinity, float.NegativeInfinity };
        foreach (float aggression in extremes)
        {
            foreach (float raidTime in extremes)
            {
                float result = ScoringModifiers.CombinedModifier(aggression, raidTime, BotActionTypeId.GoToObjective);
                Assert.That(float.IsNaN(result), Is.False, $"CombinedModifier({aggression}, {raidTime}) returned NaN");
                Assert.That(result, Is.GreaterThanOrEqualTo(0f));
            }
        }
    }

    // ── Final Score Bounds (base * CombinedModifier) ─────────────

    [Test]
    public void GoToObjectiveTask_FinalScore_NeverExceedsOne()
    {
        // Worst case: far from objective (base ~0.65), max direction bias (+0.05), max modifier
        var entity = CreateEntity();
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 1000f; // Very far → base score near 0.65
        entity.IsCloseToObjective = false;
        entity.MustUnlockDoor = false;
        entity.Aggression = 0.9f; // Reckless
        entity.RaidTimeNormalized = 0.0f; // Early raid → max GoToObjective RaidTimeModifier

        var task = new GoToObjectiveTask();
        entity.TaskScores = new float[1];
        task.ScoreEntity(0, entity);

        Assert.That(entity.TaskScores[0], Is.LessThanOrEqualTo(1.0f), "GoToObjective final score exceeds 1.0");
        Assert.That(entity.TaskScores[0], Is.GreaterThan(0f));
    }

    [Test]
    public void AmbushTask_FinalScore_NeverExceedsOne()
    {
        var entity = CreateEntity();
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.Ambush;
        entity.IsCloseToObjective = true;
        entity.Aggression = 0.0f; // Max ambush personality
        entity.RaidTimeNormalized = 1.0f; // Max ambush raid time

        var task = new AmbushTask();
        entity.TaskScores = new float[1];
        task.ScoreEntity(0, entity);

        Assert.That(entity.TaskScores[0], Is.LessThanOrEqualTo(1.0f), "Ambush final score exceeds 1.0");
    }

    [Test]
    public void LingerTask_FinalScore_NeverExceedsOne()
    {
        var entity = CreateEntity();
        entity.ObjectiveCompletedTime = 10f;
        entity.CurrentGameTime = 10.1f;
        entity.LingerDuration = 30f;
        entity.IsInCombat = false;
        entity.Aggression = 0.0f; // Max linger personality
        entity.RaidTimeNormalized = 1.0f; // Max linger raid time

        var task = new LingerTask();
        entity.TaskScores = new float[1];
        task.ScoreEntity(0, entity);

        Assert.That(entity.TaskScores[0], Is.LessThanOrEqualTo(1.0f), "Linger final score exceeds 1.0");
    }

    [Test]
    public void PatrolTask_FinalScore_NeverExceedsOne()
    {
        var entity = CreateEntity();
        entity.IsInCombat = false;
        entity.HasActiveObjective = false;
        entity.Aggression = 0.0f; // Max patrol personality
        entity.RaidTimeNormalized = 1.0f; // Max patrol raid time
        entity.PatrolRouteIndex = 0;
        entity.PatrolWaypointIndex = 0;
        entity.CurrentPositionX = 1000f; // Far from waypoint

        var route = new PatrolRoute("test", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(0, 0, 0, 2f) });

        var task = new PatrolTask();
        entity.TaskScores = new float[1];
        float score = PatrolTask.Score(entity, new[] { route });
        float modifier = ScoringModifiers.CombinedModifier(entity.Aggression, entity.RaidTimeNormalized, BotActionTypeId.Patrol);
        float final_ = score * modifier;

        Assert.That(final_, Is.LessThanOrEqualTo(1.0f), $"Patrol final score {final_} exceeds 1.0 (base={score}, mod={modifier})");
    }

    // ── SpawnEntry vs GoToObjective Priority ─────────────────────

    [Test]
    public void SpawnEntryTask_AlwaysBeatsGoToObjective_MaxModifier()
    {
        // SpawnEntry should ALWAYS win over GoToObjective at spawn time.
        // GoToObjective max theoretical: 0.70 * 1.38 = 0.966
        // SpawnEntry: 1.0 (no modifier)
        var entity = CreateEntity();
        entity.IsSpawnEntryComplete = false;
        entity.SpawnEntryDuration = 5f;
        entity.SpawnTime = 0f;
        entity.CurrentGameTime = 1f;

        float spawnScore = SpawnEntryTask.Score(entity);

        // Simulate max GoToObjective score
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 1000f;
        entity.IsCloseToObjective = false;
        entity.MustUnlockDoor = false;
        entity.Aggression = 0.9f;
        entity.RaidTimeNormalized = 0.0f;

        float goToBase = GoToObjectiveTask.Score(entity);
        float goToModifier = ScoringModifiers.CombinedModifier(0.9f, 0.0f, BotActionTypeId.GoToObjective);
        float goToFinal = goToBase * goToModifier;

        Assert.That(
            spawnScore,
            Is.GreaterThan(goToFinal),
            $"SpawnEntry ({spawnScore}) must beat GoToObjective ({goToFinal}) = base({goToBase}) * mod({goToModifier})"
        );
    }

    [Test]
    public void SpawnEntryTask_MaxScore_IsOnePointZero()
    {
        Assert.That(SpawnEntryTask.MaxBaseScore, Is.EqualTo(1.0f));
    }

    // ── PickTask Hysteresis with High Scores ─────────────────────

    [Test]
    public void PickTask_CurrentTaskWithHysteresis_PreventsSwitchToLowerScore()
    {
        // When a task has hysteresis, a competitor must exceed current + hysteresis
        var goTo = new GoToObjectiveTask(); // hysteresis=0.25
        var ambush = new AmbushTask(); // hysteresis=0.15
        var manager = new UtilityTaskManager(new UtilityTask[] { goTo, ambush });

        var entity = CreateEntity();
        entity.TaskScores = new float[2];

        // Manually assign GoToObjective as current task
        entity.TaskAssignment = new UtilityTaskAssignment(goTo, 0);
        goTo.Activate(entity);

        // GoToObjective scores 0.60, Ambush scores 0.70
        entity.TaskScores[0] = 0.60f;
        entity.TaskScores[1] = 0.70f;

        // PickTask: GoToObjective effective = 0.60 + 0.25 = 0.85
        // Ambush must beat 0.85 with raw 0.70 → doesn't switch
        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(goTo), "Should NOT switch — hysteresis protects current task");
    }

    [Test]
    public void PickTask_CompetitorExceedsHysteresis_Switches()
    {
        var goTo = new GoToObjectiveTask(); // hysteresis=0.25
        var ambush = new AmbushTask(); // hysteresis=0.15
        var manager = new UtilityTaskManager(new UtilityTask[] { goTo, ambush });

        var entity = CreateEntity();
        entity.TaskScores = new float[2];

        entity.TaskAssignment = new UtilityTaskAssignment(goTo, 0);
        goTo.Activate(entity);

        // GoToObjective scores 0.40, Ambush scores 0.80
        entity.TaskScores[0] = 0.40f;
        entity.TaskScores[1] = 0.80f;

        // PickTask: GoToObjective effective = 0.40 + 0.25 = 0.65
        // Ambush 0.80 > 0.65 → switches
        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(ambush), "Should switch — competitor exceeds hysteresis");
    }

    [Test]
    public void PickTask_NaNScore_SkippedInComparison()
    {
        var goTo = new GoToObjectiveTask();
        var ambush = new AmbushTask();
        var manager = new UtilityTaskManager(new UtilityTask[] { goTo, ambush });

        var entity = CreateEntity();
        entity.TaskScores = new float[2];
        entity.TaskScores[0] = float.NaN; // NaN score
        entity.TaskScores[1] = 0.50f;

        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(ambush), "NaN score should be skipped");
    }

    [Test]
    public void PickTask_AllZeroScores_NoTaskSelected()
    {
        var goTo = new GoToObjectiveTask();
        var ambush = new AmbushTask();
        var manager = new UtilityTaskManager(new UtilityTask[] { goTo, ambush });

        var entity = CreateEntity();
        entity.TaskScores = new float[2];
        entity.TaskScores[0] = 0f;
        entity.TaskScores[1] = 0f;

        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.Null, "No task should be selected when all scores are 0");
    }

    [Test]
    public void PickTask_NegativeScores_NoTaskSelected()
    {
        var goTo = new GoToObjectiveTask();
        var manager = new UtilityTaskManager(new UtilityTask[] { goTo });

        var entity = CreateEntity();
        entity.TaskScores = new float[1];
        entity.TaskScores[0] = -0.5f;

        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.Null, "Negative score should not select a task");
    }

    // ── Score Multiplication Chain Verification ──────────────────

    [TestCase(0.0f, 0.0f, BotActionTypeId.GoToObjective)]
    [TestCase(0.5f, 0.5f, BotActionTypeId.GoToObjective)]
    [TestCase(1.0f, 1.0f, BotActionTypeId.GoToObjective)]
    [TestCase(0.0f, 1.0f, BotActionTypeId.Linger)]
    [TestCase(1.0f, 0.0f, BotActionTypeId.Vulture)]
    public void CombinedModifier_IsProductOfPersonalityAndRaidTime_ClampedToMax(float aggression, float raidTime, int actionType)
    {
        float personality = ScoringModifiers.PersonalityModifier(aggression, actionType);
        float raid = ScoringModifiers.RaidTimeModifier(raidTime, actionType);
        float expected = personality * raid;
        if (expected > ScoringModifiers.MaxCombinedModifier)
            expected = ScoringModifiers.MaxCombinedModifier;

        float combined = ScoringModifiers.CombinedModifier(aggression, raidTime, actionType);
        Assert.That(combined, Is.EqualTo(expected).Within(0.001f));
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static BotEntity CreateEntity()
    {
        return new BotEntity(0) { IsActive = true };
    }
}
