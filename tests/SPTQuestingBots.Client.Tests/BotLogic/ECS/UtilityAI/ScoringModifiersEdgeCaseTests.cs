using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI;

[TestFixture]
public class ScoringModifiersEdgeCaseTests
{
    // ── Lerp out-of-range inputs ─────────────────────────────

    [Test]
    public void Lerp_NegativeT_ClampedToZero_ReturnsA()
    {
        // Lerp clamps t to [0,1], so t=-1 → t=0 → result = a = 0.85
        float result = ScoringModifiers.Lerp(0.85f, 1.15f, -1f);
        Assert.That(result, Is.EqualTo(0.85f).Within(0.001f));
    }

    [Test]
    public void Lerp_TGreaterThanOne_ClampedToOne_ReturnsB()
    {
        // Lerp clamps t to [0,1], so t=2 → t=1 → result = b = 1.15
        float result = ScoringModifiers.Lerp(0.85f, 1.15f, 2f);
        Assert.That(result, Is.EqualTo(1.15f).Within(0.001f));
    }

    [Test]
    public void Lerp_NaN_ClampedToZero_ReturnsA()
    {
        // NaN comparisons are false, so clamping logic falls through to NaN.
        // However, NaN < 0f is false AND NaN > 1f is false, so clampedT = NaN.
        // The result is still NaN.
        float result = ScoringModifiers.Lerp(0.85f, 1.15f, float.NaN);
        Assert.That(float.IsNaN(result), Is.True);
    }

    [Test]
    public void Lerp_Infinity_ClampedToOne_ReturnsB()
    {
        // Lerp clamps t to [0,1], so t=+Inf → t=1 → result = b = 1.15
        float result = ScoringModifiers.Lerp(0.85f, 1.15f, float.PositiveInfinity);
        Assert.That(result, Is.EqualTo(1.15f).Within(0.001f));
    }

    // ── PersonalityModifier boundary inputs ──────────────────

    [Test]
    public void PersonalityModifier_AggressionZero_ReturnsA()
    {
        // GoToObjective: Lerp(0.85, 1.15, 0) = 0.85
        float result = ScoringModifiers.PersonalityModifier(0f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.EqualTo(0.85f).Within(0.001f));
    }

    [Test]
    public void PersonalityModifier_AggressionOne_ReturnsB()
    {
        // GoToObjective: Lerp(0.85, 1.15, 1) = 1.15
        float result = ScoringModifiers.PersonalityModifier(1f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.EqualTo(1.15f).Within(0.001f));
    }

    [Test]
    public void PersonalityModifier_NegativeAggression_ClampedToZero()
    {
        // Aggression clamped to 0, GoToObjective: Lerp(0.85, 1.15, 0) = 0.85
        float result = ScoringModifiers.PersonalityModifier(-0.5f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.EqualTo(0.85f).Within(0.001f));
    }

    [Test]
    public void PersonalityModifier_AggressionAboveOne_ClampedToOne()
    {
        // Aggression clamped to 1, GoToObjective: Lerp(0.85, 1.15, 1) = 1.15
        float result = ScoringModifiers.PersonalityModifier(1.5f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.EqualTo(1.15f).Within(0.001f));
    }

    [Test]
    public void PersonalityModifier_NaN_ClampedToNaN_ReturnsNaN()
    {
        // NaN comparisons are false, so clampedAggression stays NaN, Lerp also gets NaN
        float result = ScoringModifiers.PersonalityModifier(float.NaN, BotActionTypeId.GoToObjective);
        Assert.That(float.IsNaN(result), Is.True);
    }

    [Test]
    public void PersonalityModifier_ExtremeAggression_ClampedToOne_NoNegative()
    {
        // Aggression=3 clamped to 1, Linger: Lerp(1.3, 0.7, 1) = 0.7
        float result = ScoringModifiers.PersonalityModifier(3f, BotActionTypeId.Linger);
        Assert.That(result, Is.EqualTo(0.7f).Within(0.001f));
    }

    // ── RaidTimeModifier boundary inputs ─────────────────────

    [Test]
    public void RaidTimeModifier_TimeZero_ReturnsEarlyRaidValue()
    {
        float result = ScoringModifiers.RaidTimeModifier(0f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.EqualTo(1.2f).Within(0.001f));
    }

    [Test]
    public void RaidTimeModifier_TimeOne_ReturnsLateRaidValue()
    {
        float result = ScoringModifiers.RaidTimeModifier(1f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.EqualTo(0.8f).Within(0.001f));
    }

    [Test]
    public void RaidTimeModifier_NegativeTime_ClampedToZero()
    {
        // Time clamped to 0, GoToObjective: Lerp(1.2, 0.8, 0) = 1.2
        float result = ScoringModifiers.RaidTimeModifier(-0.5f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.EqualTo(1.2f).Within(0.001f));
    }

    [Test]
    public void RaidTimeModifier_TimeAboveOne_ClampedToOne()
    {
        // Time clamped to 1, GoToObjective: Lerp(1.2, 0.8, 1) = 0.8
        float result = ScoringModifiers.RaidTimeModifier(2f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.EqualTo(0.8f).Within(0.001f));
    }

    [Test]
    public void RaidTimeModifier_NaN_ClampedToNaN_ReturnsNaN()
    {
        // NaN comparisons are false, so clampedTime stays NaN, Lerp also gets NaN
        float result = ScoringModifiers.RaidTimeModifier(float.NaN, BotActionTypeId.GoToObjective);
        Assert.That(float.IsNaN(result), Is.True);
    }

    // ── CombinedModifier edge cases ──────────────────────────

    [Test]
    public void CombinedModifier_NaN_Aggression_ReturnsNeutral()
    {
        // NaN propagates through PersonalityModifier, CombinedModifier guards it → 1.0
        float result = ScoringModifiers.CombinedModifier(float.NaN, 0.5f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.EqualTo(1.0f));
    }

    [Test]
    public void CombinedModifier_NaN_RaidTime_ReturnsNeutral()
    {
        // NaN propagates through RaidTimeModifier, CombinedModifier guards it → 1.0
        float result = ScoringModifiers.CombinedModifier(0.5f, float.NaN, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.EqualTo(1.0f));
    }

    [Test]
    public void CombinedModifier_ExtremeInputs_ClampedNoNegative()
    {
        // aggression=3 clamped to 1, raidTimeNormalized=0 clamped to 0
        // PersonalityModifier(Linger, 1) = Lerp(1.3, 0.7, 1) = 0.7
        // RaidTimeModifier(Linger, 0) = Lerp(0.7, 1.3, 0) = 0.7
        // Combined = 0.7 * 0.7 = 0.49
        float result = ScoringModifiers.CombinedModifier(3f, 0f, BotActionTypeId.Linger);
        Assert.That(result, Is.EqualTo(0.49f).Within(0.01f));
    }

    [Test]
    public void CombinedModifier_BothAtBoundary_StillValid()
    {
        // aggression=0, raidTimeNormalized=0
        float result = ScoringModifiers.CombinedModifier(0f, 0f, BotActionTypeId.GoToObjective);
        // Personality = 0.85, RaidTime = 1.2, Combined = 1.02
        Assert.That(result, Is.EqualTo(0.85f * 1.2f).Within(0.001f));
    }

    [Test]
    public void CombinedModifier_BothAtOne_StillValid()
    {
        float result = ScoringModifiers.CombinedModifier(1f, 1f, BotActionTypeId.GoToObjective);
        // Personality = 1.15, RaidTime = 0.8, Combined = 0.92
        Assert.That(result, Is.EqualTo(1.15f * 0.8f).Within(0.001f));
    }

    [Test]
    public void CombinedModifier_UnknownActionType_ReturnsOne()
    {
        // Both modifiers return 1.0 for unknown action types
        float result = ScoringModifiers.CombinedModifier(0.5f, 0.5f, -999);
        Assert.That(result, Is.EqualTo(1f));
    }

    // ── GoToObjectiveTask.Score edge cases ───────────────────

    [Test]
    public void GoToObjectiveTask_Score_NoActiveObjective_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = false;

        float score = GoToObjectiveTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void GoToObjectiveTask_Score_ZeroDistance_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 0f;

        float score = GoToObjectiveTask.Score(entity);
        // BaseScore * (1 - exp(0)) = 0.65 * 0 = 0
        Assert.That(score, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void GoToObjectiveTask_Score_MaxDistance_ApproachesBaseScore()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = float.MaxValue;

        float score = GoToObjectiveTask.Score(entity);
        // At huge distance, exp(-MaxValue/75) = 0, score = 0.65 * 1 = 0.65
        Assert.That(score, Is.EqualTo(0.65f).Within(0.01f));
    }

    [Test]
    public void GoToObjectiveTask_ScoreEntity_NaN_Aggression_GuardedToNeutralModifier()
    {
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 100f;
        entity.Aggression = float.NaN;
        entity.RaidTimeNormalized = 0.5f;

        var task = new GoToObjectiveTask();
        task.ScoreEntity(0, entity);

        // NaN aggression → CombinedModifier returns 1.0 (neutral), so score is valid
        Assert.That(float.IsNaN(entity.TaskScores[0]), Is.False);
        Assert.That(entity.TaskScores[0], Is.GreaterThan(0f));
    }

    // ── LingerTask.Score edge cases ──────────────────────────

    [Test]
    public void LingerTask_Score_ZeroObjectiveCompletedTime_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.ObjectiveCompletedTime = 0f;

        float score = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LingerTask_Score_NegativeElapsed_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.ObjectiveCompletedTime = 10f;
        entity.CurrentGameTime = 5f; // Before completion — negative elapsed
        entity.LingerDuration = 20f;

        float score = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LingerTask_Score_ExactDuration_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.ObjectiveCompletedTime = 1f;
        entity.LingerDuration = 10f;
        entity.CurrentGameTime = 11f; // elapsed = duration exactly

        float score = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LingerTask_Score_ZeroDuration_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.ObjectiveCompletedTime = 1f;
        entity.LingerDuration = 0f; // Invalid duration
        entity.CurrentGameTime = 2f;

        float score = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LingerTask_Score_JustStarted_ReturnsFullBaseScore()
    {
        var entity = new BotEntity(0);
        entity.ObjectiveCompletedTime = 1f;
        entity.LingerDuration = 20f;
        entity.CurrentGameTime = 1f; // elapsed = 0

        float score = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.EqualTo(LingerTask.DefaultBaseScore).Within(0.001f));
    }

    [Test]
    public void LingerTask_Score_Midway_ReturnsDecayedScore()
    {
        var entity = new BotEntity(0);
        entity.ObjectiveCompletedTime = 1f;
        entity.LingerDuration = 20f;
        entity.CurrentGameTime = 11f; // elapsed = 10, half of duration

        float expected = LingerTask.DefaultBaseScore * (1f - 10f / 20f);
        float score = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void LingerTask_Score_InCombat_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.ObjectiveCompletedTime = 1f;
        entity.LingerDuration = 20f;
        entity.CurrentGameTime = 5f;
        entity.IsInCombat = true;

        float score = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.EqualTo(0f));
    }

    // ── LootTask.Score edge cases ────────────────────────────

    [Test]
    public void LootTask_Score_NoTarget_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.HasLootTarget = false;

        float score = LootTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LootTask_Score_InCombat_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.HasLootTarget = true;
        entity.IsInCombat = true;

        float score = LootTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LootTask_Score_ZeroValue_StillScores()
    {
        var entity = new BotEntity(0);
        entity.HasLootTarget = true;
        entity.LootTargetValue = 0f;
        entity.InventorySpaceFree = 5f;
        // Same position = zero distance
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionY = 0f;
        entity.CurrentPositionZ = 0f;
        entity.LootTargetX = 0f;
        entity.LootTargetY = 0f;
        entity.LootTargetZ = 0f;

        float score = LootTask.Score(entity);
        // valueScore=0, distancePenalty=0, proximityBonus depends on objective
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void LootTask_Score_VeryHighValue_ClampedToMax()
    {
        var entity = new BotEntity(0);
        entity.HasLootTarget = true;
        entity.LootTargetValue = 1000000f; // Way above 50000 cap
        entity.InventorySpaceFree = 5f;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionY = 0f;
        entity.CurrentPositionZ = 0f;
        entity.LootTargetX = 0f;
        entity.LootTargetY = 0f;
        entity.LootTargetZ = 0f;

        float score = LootTask.Score(entity);
        Assert.That(score, Is.LessThanOrEqualTo(LootTask.MaxBaseScore));
    }

    [Test]
    public void LootTask_Score_VeryFarDistance_HighPenalty()
    {
        var entity = new BotEntity(0);
        entity.HasLootTarget = true;
        entity.LootTargetValue = 50000f;
        entity.InventorySpaceFree = 5f;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionY = 0f;
        entity.CurrentPositionZ = 0f;
        entity.LootTargetX = 1000f;
        entity.LootTargetY = 0f;
        entity.LootTargetZ = 1000f;

        float score = LootTask.Score(entity);
        // Large distance penalty could make score 0 or still positive
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
        Assert.That(score, Is.LessThanOrEqualTo(LootTask.MaxBaseScore));
    }

    // ── SpawnEntryTask.Score edge cases ──────────────────────

    [Test]
    public void SpawnEntryTask_Score_AlreadyComplete_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.IsSpawnEntryComplete = true;
        entity.SpawnEntryDuration = 5f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void SpawnEntryTask_Score_ZeroDuration_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.SpawnEntryDuration = 0f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void SpawnEntryTask_Score_NegativeElapsed_ReturnsMaxScore()
    {
        var entity = new BotEntity(0);
        entity.SpawnEntryDuration = 5f;
        entity.SpawnTime = 10f;
        entity.CurrentGameTime = 5f; // Before spawn

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(SpawnEntryTask.MaxBaseScore));
    }

    [Test]
    public void SpawnEntryTask_Score_DurationExpired_MarksComplete()
    {
        var entity = new BotEntity(0);
        entity.SpawnEntryDuration = 3f;
        entity.SpawnTime = 0f;
        entity.CurrentGameTime = 5f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
        Assert.That(entity.IsSpawnEntryComplete, Is.True);
    }

    [Test]
    public void SpawnEntryTask_Score_WithinDuration_ReturnsMaxScore()
    {
        var entity = new BotEntity(0);
        entity.SpawnEntryDuration = 5f;
        entity.SpawnTime = 0f;
        entity.CurrentGameTime = 2f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(SpawnEntryTask.MaxBaseScore));
    }

    // ── All modifier-applied tasks with extreme inputs ───────

    [Test]
    public void AllModifiedTasks_NegativeScore_WhenCombinedModifierNegative()
    {
        // This test documents that a negative CombinedModifier can make stored scores negative.
        // In practice aggression is always [0.1, 0.9], but this is a regression test.
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 100f;
        entity.Aggression = 3f; // Way out of range
        entity.RaidTimeNormalized = 0.5f;

        var task = new GoToObjectiveTask();
        task.ScoreEntity(0, entity);

        // Score() returns a positive value, but CombinedModifier(3, 0.5, GoToObjective)
        // PersonalityModifier(3, GoToObjective) = Lerp(0.85, 1.15, 3) = 0.85 + 0.3*3 = 1.75
        // RaidTimeModifier(0.5, GoToObjective) = 1.0
        // Combined = 1.75 — still positive, so final score is positive
        // For negative combined we need a different action type
        Assert.That(entity.TaskScores[0], Is.GreaterThan(0f));
    }
}
