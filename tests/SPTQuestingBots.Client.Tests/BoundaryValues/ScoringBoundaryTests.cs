using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BoundaryValues;

/// <summary>
/// Tests all scoring formulas at boundary values: 0.0, 0.5, 1.0 for aggression
/// and raidTimeNormalized, plus edge cases like all-zero scores and NaN propagation.
/// </summary>
[TestFixture]
public class ScoringBoundaryTests
{
    // ── ScoringModifiers.PersonalityModifier ──────────────────────

    [TestCase(0f, BotActionTypeId.GoToObjective, 0.85f)]
    [TestCase(1f, BotActionTypeId.GoToObjective, 1.15f)]
    [TestCase(0.5f, BotActionTypeId.GoToObjective, 1.0f)]
    [TestCase(0f, BotActionTypeId.Ambush, 1.2f)]
    [TestCase(1f, BotActionTypeId.Ambush, 0.8f)]
    [TestCase(0f, BotActionTypeId.Snipe, 1.2f)]
    [TestCase(1f, BotActionTypeId.Snipe, 0.8f)]
    [TestCase(0f, BotActionTypeId.Linger, 1.3f)]
    [TestCase(1f, BotActionTypeId.Linger, 0.7f)]
    [TestCase(0f, BotActionTypeId.Loot, 1.1f)]
    [TestCase(1f, BotActionTypeId.Loot, 0.9f)]
    [TestCase(0f, BotActionTypeId.Vulture, 0.7f)]
    [TestCase(1f, BotActionTypeId.Vulture, 1.3f)]
    [TestCase(0f, BotActionTypeId.Investigate, 0.8f)]
    [TestCase(1f, BotActionTypeId.Investigate, 1.2f)]
    [TestCase(0f, BotActionTypeId.Patrol, 1.2f)]
    [TestCase(1f, BotActionTypeId.Patrol, 0.8f)]
    public void PersonalityModifier_AtBoundaryAggression_ReturnsExpected(float aggression, int actionTypeId, float expected)
    {
        float result = ScoringModifiers.PersonalityModifier(aggression, actionTypeId);
        Assert.That(result, Is.EqualTo(expected).Within(0.001f));
    }

    [TestCase(-0.5f, BotActionTypeId.GoToObjective)]
    [TestCase(1.5f, BotActionTypeId.GoToObjective)]
    [TestCase(-100f, BotActionTypeId.Ambush)]
    [TestCase(100f, BotActionTypeId.Ambush)]
    public void PersonalityModifier_OutOfRange_ClampedToEndpoints(float aggression, int actionTypeId)
    {
        float result = ScoringModifiers.PersonalityModifier(aggression, actionTypeId);
        float atZero = ScoringModifiers.PersonalityModifier(0f, actionTypeId);
        float atOne = ScoringModifiers.PersonalityModifier(1f, actionTypeId);

        if (aggression < 0f)
            Assert.That(result, Is.EqualTo(atZero).Within(0.001f));
        else
            Assert.That(result, Is.EqualTo(atOne).Within(0.001f));
    }

    [Test]
    public void PersonalityModifier_UnknownActionType_Returns1()
    {
        Assert.That(ScoringModifiers.PersonalityModifier(0.5f, 9999), Is.EqualTo(1f));
    }

    // ── ScoringModifiers.RaidTimeModifier ──────────────────────

    [TestCase(0f, BotActionTypeId.GoToObjective, 1.2f)]
    [TestCase(1f, BotActionTypeId.GoToObjective, 0.8f)]
    [TestCase(0.5f, BotActionTypeId.GoToObjective, 1.0f)]
    [TestCase(0f, BotActionTypeId.Linger, 0.7f)]
    [TestCase(1f, BotActionTypeId.Linger, 1.3f)]
    [TestCase(0f, BotActionTypeId.Loot, 0.8f)]
    [TestCase(1f, BotActionTypeId.Loot, 1.2f)]
    [TestCase(0f, BotActionTypeId.Ambush, 0.9f)]
    [TestCase(1f, BotActionTypeId.Ambush, 1.2f)]
    [TestCase(0f, BotActionTypeId.Patrol, 0.8f)]
    [TestCase(1f, BotActionTypeId.Patrol, 1.2f)]
    public void RaidTimeModifier_AtBoundaryTime_ReturnsExpected(float raidTime, int actionTypeId, float expected)
    {
        float result = ScoringModifiers.RaidTimeModifier(raidTime, actionTypeId);
        Assert.That(result, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void RaidTimeModifier_UnknownActionType_Returns1()
    {
        Assert.That(ScoringModifiers.RaidTimeModifier(0.5f, 9999), Is.EqualTo(1f));
    }

    // ── ScoringModifiers.CombinedModifier ──────────────────────

    [Test]
    public void CombinedModifier_AllZero_ReturnsProduct()
    {
        float result = ScoringModifiers.CombinedModifier(0f, 0f, BotActionTypeId.GoToObjective);
        float expected =
            ScoringModifiers.PersonalityModifier(0f, BotActionTypeId.GoToObjective)
            * ScoringModifiers.RaidTimeModifier(0f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void CombinedModifier_AllOne_ReturnsProduct()
    {
        float result = ScoringModifiers.CombinedModifier(1f, 1f, BotActionTypeId.GoToObjective);
        float expected =
            ScoringModifiers.PersonalityModifier(1f, BotActionTypeId.GoToObjective)
            * ScoringModifiers.RaidTimeModifier(1f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void CombinedModifier_NaN_Returns1()
    {
        // NaN aggression → PersonalityModifier would clamp, but even if multiplication produced NaN
        float result = ScoringModifiers.CombinedModifier(float.NaN, 0.5f, BotActionTypeId.GoToObjective);
        // NaN is clamped by the ternary in PersonalityModifier: NaN < 0 is false, NaN > 1 is false,
        // so it falls through to the raw NaN value. Lerp(a, b, NaN) = a + (b-a)*NaN = NaN.
        // CombinedModifier guard catches NaN → returns 1.0
        Assert.That(result, Is.EqualTo(1.0f));
    }

    [Test]
    public void CombinedModifier_AlwaysPositive()
    {
        // Test all action types at all boundary combinations
        int[] actionTypes =
        {
            BotActionTypeId.GoToObjective,
            BotActionTypeId.Ambush,
            BotActionTypeId.Snipe,
            BotActionTypeId.Linger,
            BotActionTypeId.Loot,
            BotActionTypeId.Vulture,
            BotActionTypeId.Investigate,
            BotActionTypeId.Patrol,
        };
        float[] values = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        foreach (int actionType in actionTypes)
        {
            foreach (float aggression in values)
            {
                foreach (float raidTime in values)
                {
                    float result = ScoringModifiers.CombinedModifier(aggression, raidTime, actionType);
                    Assert.That(result, Is.GreaterThan(0f), $"CombinedModifier({aggression}, {raidTime}, {actionType}) was {result}");
                    Assert.That(float.IsNaN(result), Is.False, $"CombinedModifier({aggression}, {raidTime}, {actionType}) was NaN");
                }
            }
        }
    }

    // ── ScoringModifiers.Lerp ──────────────────────

    [Test]
    public void Lerp_TAtZero_ReturnsA()
    {
        Assert.That(ScoringModifiers.Lerp(0.85f, 1.15f, 0f), Is.EqualTo(0.85f).Within(0.001f));
    }

    [Test]
    public void Lerp_TAtOne_ReturnsB()
    {
        Assert.That(ScoringModifiers.Lerp(0.85f, 1.15f, 1f), Is.EqualTo(1.15f).Within(0.001f));
    }

    [Test]
    public void Lerp_TAtHalf_ReturnsMidpoint()
    {
        Assert.That(ScoringModifiers.Lerp(0.85f, 1.15f, 0.5f), Is.EqualTo(1.0f).Within(0.001f));
    }

    [Test]
    public void Lerp_TBelowZero_ClampedToA()
    {
        Assert.That(ScoringModifiers.Lerp(0.85f, 1.15f, -1f), Is.EqualTo(0.85f).Within(0.001f));
    }

    [Test]
    public void Lerp_TAboveOne_ClampedToB()
    {
        Assert.That(ScoringModifiers.Lerp(0.85f, 1.15f, 2f), Is.EqualTo(1.15f).Within(0.001f));
    }

    // ── GoToObjectiveTask score at boundaries ──────────────────

    [Test]
    public void GoToObjectiveTask_DistanceZero_ScoresZero()
    {
        var entity = CreateEntity();
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 0f;
        entity.IsCloseToObjective = false;

        float score = GoToObjectiveTask.Score(entity);
        // BaseScore * (1 - exp(-0/75)) = 0.65 * 0 = 0
        Assert.That(score, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void GoToObjectiveTask_LargeDistance_ApproachesBaseScore()
    {
        var entity = CreateEntity();
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 10000f;
        entity.IsCloseToObjective = false;

        float score = GoToObjectiveTask.Score(entity);
        // At large distance, exp(-10000/75) ≈ 0, so score ≈ 0.65
        Assert.That(score, Is.EqualTo(GoToObjectiveTask.BaseScore).Within(0.01f));
    }

    [Test]
    public void GoToObjectiveTask_NoObjective_ScoresZero()
    {
        var entity = CreateEntity();
        entity.HasActiveObjective = false;

        float score = GoToObjectiveTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void GoToObjectiveTask_MustUnlockDoor_ScoresZero()
    {
        var entity = CreateEntity();
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.MustUnlockDoor = true;
        entity.DistanceToObjective = 100f;

        float score = GoToObjectiveTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void GoToObjectiveTask_AmbushAndCloseToObjective_ScoresZero()
    {
        var entity = CreateEntity();
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.Ambush;
        entity.IsCloseToObjective = true;
        entity.DistanceToObjective = 5f;

        float score = GoToObjectiveTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    // ── AmbushTask/SnipeTask/PlantItemTask boundary ─────────────

    [Test]
    public void AmbushTask_CloseToObjective_ScoresBaseScore()
    {
        var entity = CreateEntity();
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.Ambush;
        entity.IsCloseToObjective = true;

        float score = AmbushTask.Score(entity);
        Assert.That(score, Is.EqualTo(AmbushTask.BaseScore));
    }

    [Test]
    public void AmbushTask_NotCloseToObjective_ScoresZero()
    {
        var entity = CreateEntity();
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.Ambush;
        entity.IsCloseToObjective = false;

        float score = AmbushTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void AmbushTask_WrongAction_ScoresZero()
    {
        var entity = CreateEntity();
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.IsCloseToObjective = true;

        float score = AmbushTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    // ── LootTask boundary values ──────────────────────────────

    [Test]
    public void LootTask_NoLootTarget_ScoresZero()
    {
        var entity = CreateEntity();
        entity.HasLootTarget = false;

        float score = LootTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LootTask_InCombat_ScoresZero()
    {
        var entity = CreateEntity();
        entity.HasLootTarget = true;
        entity.IsInCombat = true;
        entity.LootTargetValue = 50000f;

        float score = LootTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LootTask_MaxValue_CappedAtMaxBaseScore()
    {
        var entity = CreateEntity();
        entity.HasLootTarget = true;
        entity.LootTargetValue = 1000000f; // extremely high value
        entity.InventorySpaceFree = 10f;
        // Same position as loot — zero distance
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionY = 0f;
        entity.CurrentPositionZ = 0f;
        entity.LootTargetX = 0f;
        entity.LootTargetY = 0f;
        entity.LootTargetZ = 0f;
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 0f;

        float score = LootTask.Score(entity);
        Assert.That(score, Is.LessThanOrEqualTo(LootTask.MaxBaseScore));
    }

    [Test]
    public void LootTask_ZeroValue_ZeroDistance_ScoresZero()
    {
        var entity = CreateEntity();
        entity.HasLootTarget = true;
        entity.LootTargetValue = 0f;
        entity.InventorySpaceFree = 10f;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionY = 0f;
        entity.CurrentPositionZ = 0f;
        entity.LootTargetX = 0f;
        entity.LootTargetY = 0f;
        entity.LootTargetZ = 0f;

        float score = LootTask.Score(entity);
        // valueScore = 0, distancePenalty = 0, proximityBonus = 0 → score = 0
        Assert.That(score, Is.EqualTo(0f).Within(0.001f));
    }

    // ── VultureTask boundary values ──────────────────────────

    [Test]
    public void VultureTask_InCombat_ScoresZero()
    {
        var entity = CreateEntity();
        entity.IsInCombat = true;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 100;

        float score = VultureTask.Score(entity, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void VultureTask_IntensityAtThreshold_ScoresZeroIntensityComponent()
    {
        var entity = CreateEntity();
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 15; // exactly at threshold
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;
        entity.NearbyEventX = 0f; // same position
        entity.NearbyEventZ = 0f;

        float score = VultureTask.Score(entity, 15, 150f);
        // intensityRatio = 15/15 = 1.0, intensityScore = (1-1)*0.3 = 0
        // proximityScore = (1 - 0/150^2)*0.3 = 0.3
        Assert.That(score, Is.EqualTo(0.30f).Within(0.01f));
    }

    [Test]
    public void VultureTask_DetectionRangeZero_ProximityIsZero()
    {
        var entity = CreateEntity();
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 30;
        entity.CurrentPositionX = 10f;
        entity.CurrentPositionZ = 10f;
        entity.NearbyEventX = 0f;
        entity.NearbyEventZ = 0f;

        float score = VultureTask.Score(entity, 15, 0f);
        // detectionRange = 0 → rangeSqr = 0, distSqr >= rangeSqr → proximityScore = 0
        // intensityRatio = 30/15 = 2.0 (capped), intensityScore = (2-1)*0.3 = 0.3
        Assert.That(score, Is.EqualTo(0.3f).Within(0.01f));
    }

    [Test]
    public void VultureTask_CourageThresholdZero_ProtectedByMathMax()
    {
        var entity = CreateEntity();
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 5;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;
        entity.NearbyEventX = 0f;
        entity.NearbyEventZ = 0f;

        float score = VultureTask.Score(entity, 0, 150f);
        // safeCourageThreshold = Max(1, 0) = 1, intensityRatio = 5/1 = 5 → capped to 2
        // intensityScore = (2-1)*0.3 = 0.3, proximityScore = 0.3
        Assert.That(score, Is.EqualTo(0.6f).Within(0.01f));
    }

    // ── LingerTask boundary values ──────────────────────────

    [Test]
    public void LingerTask_JustCompleted_FullScore()
    {
        var entity = CreateEntity();
        entity.ObjectiveCompletedTime = 100f;
        entity.CurrentGameTime = 100f;
        entity.LingerDuration = 10f;

        float score = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        // elapsed = 0, score = 0.45 * (1 - 0) = 0.45
        Assert.That(score, Is.EqualTo(LingerTask.DefaultBaseScore).Within(0.001f));
    }

    [Test]
    public void LingerTask_DurationExpired_ScoresZero()
    {
        var entity = CreateEntity();
        entity.ObjectiveCompletedTime = 100f;
        entity.CurrentGameTime = 110f;
        entity.LingerDuration = 10f;

        float score = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LingerTask_DurationZero_ScoresZero()
    {
        var entity = CreateEntity();
        entity.ObjectiveCompletedTime = 100f;
        entity.CurrentGameTime = 100f;
        entity.LingerDuration = 0f;

        float score = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LingerTask_NegativeDuration_ScoresZero()
    {
        var entity = CreateEntity();
        entity.ObjectiveCompletedTime = 100f;
        entity.CurrentGameTime = 100f;
        entity.LingerDuration = -5f;

        float score = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.EqualTo(0f));
    }

    // ── SpawnEntryTask boundary values ──────────────────────────

    [Test]
    public void SpawnEntryTask_JustSpawned_MaxScore()
    {
        var entity = CreateEntity();
        entity.SpawnTime = 100f;
        entity.CurrentGameTime = 100f;
        entity.SpawnEntryDuration = 5f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(SpawnEntryTask.MaxBaseScore));
    }

    [Test]
    public void SpawnEntryTask_DurationExpired_MarksComplete()
    {
        var entity = CreateEntity();
        entity.SpawnTime = 100f;
        entity.CurrentGameTime = 106f;
        entity.SpawnEntryDuration = 5f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
        Assert.That(entity.IsSpawnEntryComplete, Is.True);
    }

    [Test]
    public void SpawnEntryTask_AlreadyComplete_ScoresZero()
    {
        var entity = CreateEntity();
        entity.IsSpawnEntryComplete = true;
        entity.SpawnEntryDuration = 5f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void SpawnEntryTask_ZeroDuration_ScoresZero()
    {
        var entity = CreateEntity();
        entity.SpawnEntryDuration = 0f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    // ── InvestigateTask boundary values ──────────────────────────

    [Test]
    public void InvestigateTask_IntensityThresholdZero_ProtectedByMathMax()
    {
        var entity = CreateEntity();
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 3;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;
        entity.NearbyEventX = 0f;
        entity.NearbyEventZ = 0f;

        float score = InvestigateTask.Score(entity, 0, 120f);
        // safeIntensityThreshold = Max(1, 0) = 1
        Assert.That(float.IsNaN(score), Is.False);
        Assert.That(score, Is.GreaterThan(0f));
    }

    // ── PickTask with all scores zero ──────────────────────────

    [Test]
    public void PickTask_AllScoresZero_KeepsNoTask()
    {
        var entity = CreateEntity();
        entity.TaskScores = new float[3];
        entity.TaskAssignment = default;

        var tasks = new UtilityTask[] { new AmbushTask(), new SnipeTask(), new PlantItemTask() };
        var manager = new UtilityTaskManager(tasks);

        // All scores are zero
        entity.TaskScores[0] = 0f;
        entity.TaskScores[1] = 0f;
        entity.TaskScores[2] = 0f;

        manager.PickTask(entity);

        // No task should be selected (all 0 <= highestScore which starts at 0)
        Assert.That(entity.TaskAssignment.Task, Is.Null);
    }

    [Test]
    public void PickTask_AllScoresEqual_FirstWins()
    {
        var entity = CreateEntity();
        entity.TaskScores = new float[3];
        entity.TaskAssignment = default;

        var task0 = new AmbushTask();
        var task1 = new SnipeTask();
        var task2 = new PlantItemTask();
        var tasks = new UtilityTask[] { task0, task1, task2 };
        var manager = new UtilityTaskManager(tasks);

        entity.TaskScores[0] = 0.5f;
        entity.TaskScores[1] = 0.5f;
        entity.TaskScores[2] = 0.5f;

        manager.PickTask(entity);

        // First task with score > 0 (initial highestScore) wins
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(task0));
    }

    [Test]
    public void PickTask_NaNScore_Skipped()
    {
        var entity = CreateEntity();
        entity.TaskScores = new float[3];
        entity.TaskAssignment = default;

        var task0 = new AmbushTask();
        var task1 = new SnipeTask();
        var task2 = new PlantItemTask();
        var tasks = new UtilityTask[] { task0, task1, task2 };
        var manager = new UtilityTaskManager(tasks);

        entity.TaskScores[0] = float.NaN;
        entity.TaskScores[1] = 0.3f;
        entity.TaskScores[2] = 0.5f;

        manager.PickTask(entity);

        // NaN is skipped, highest non-NaN is task2 at 0.5
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(task2));
    }

    [Test]
    public void PickTask_CurrentTaskNaN_ResetsToZero()
    {
        var entity = CreateEntity();
        entity.TaskScores = new float[2];

        var task0 = new AmbushTask();
        var task1 = new SnipeTask();
        var tasks = new UtilityTask[] { task0, task1 };
        var manager = new UtilityTaskManager(tasks);

        // Set current task to task0
        entity.TaskAssignment = new UtilityTaskAssignment(task0, 0);
        task0.Activate(entity);

        // Current score is NaN, competing score is small positive
        entity.TaskScores[0] = float.NaN;
        entity.TaskScores[1] = 0.1f;

        manager.PickTask(entity);

        // NaN highestScore resets to 0, task1 at 0.1 > 0 wins
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(task1));
    }

    // ── Hysteresis prevents equal-score switch ──────────────────

    [Test]
    public void PickTask_Hysteresis_PreventsSwitch()
    {
        var entity = CreateEntity();
        entity.TaskScores = new float[2];

        var task0 = new AmbushTask(0.15f);
        var task1 = new SnipeTask(0.15f);
        var tasks = new UtilityTask[] { task0, task1 };
        var manager = new UtilityTaskManager(tasks);

        // Assign task0
        entity.TaskAssignment = new UtilityTaskAssignment(task0, 0);
        task0.Activate(entity);

        // Both score 0.5 — with hysteresis, task0 has effective score 0.65
        entity.TaskScores[0] = 0.5f;
        entity.TaskScores[1] = 0.5f;

        manager.PickTask(entity);

        // Task0 keeps because 0.5 <= 0.5 + 0.15 (hysteresis prevents switch)
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(task0));
    }

    // ── Helper ──────────────────────────────────────────────────

    private static BotEntity CreateEntity()
    {
        return new BotEntity(1);
    }
}
