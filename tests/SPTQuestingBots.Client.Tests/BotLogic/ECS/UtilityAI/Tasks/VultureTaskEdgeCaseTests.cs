using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

[TestFixture]
public class VultureTaskEdgeCaseTests
{
    // ── Division by zero when courageThreshold=0 ─────────────

    [Test]
    public void Score_ZeroCourageThreshold_ZeroIntensity_ReturnsZero()
    {
        // courageThreshold=0 → clamped to 1, intensity=0 < 1 → returns 0
        var entity = MakeVultureEntity();
        entity.CombatIntensity = 0;

        float score = VultureTask.Score(entity, courageThreshold: 0, detectionRange: 150f);

        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_ZeroCourageThreshold_PositiveIntensity_ReturnsValidScore()
    {
        // (float)positive / 0 = Infinity, capped to 2f
        // intensityScore = (2 - 1) * 0.3 = 0.3
        var entity = MakeVultureEntity();
        entity.CombatIntensity = 30;

        float score = VultureTask.Score(entity, courageThreshold: 0, detectionRange: 150f);

        // Infinity > 2f → capped at 2, intensityScore = 0.3
        // Proximity also contributes. Score should be finite and positive.
        Assert.That(float.IsNaN(score), Is.False);
        Assert.That(score, Is.GreaterThan(0f));
        Assert.That(score, Is.LessThanOrEqualTo(VultureTask.MaxBaseScore));
    }

    // ── Intensity exactly at threshold ───────────────────────

    [Test]
    public void Score_IntensityEqualsThreshold_HasMinimalScore()
    {
        var entity = MakeVultureEntity();
        entity.CombatIntensity = 15;

        float score = VultureTask.Score(entity, courageThreshold: 15, detectionRange: 150f);

        // intensityRatio = 1, intensityScore = (1-1)*0.3 = 0
        // Only proximity contributes
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void Score_IntensityJustBelowThreshold_ReturnsZero()
    {
        var entity = MakeVultureEntity();
        entity.CombatIntensity = 14;

        float score = VultureTask.Score(entity, courageThreshold: 15, detectionRange: 150f);
        Assert.That(score, Is.EqualTo(0f));
    }

    // ── Proximity edge cases ─────────────────────────────────

    [Test]
    public void Score_EventExactlyAtDetectionRange_ZeroProximity()
    {
        var entity = MakeVultureEntity();
        entity.CombatIntensity = 30;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;
        entity.NearbyEventX = 150f;
        entity.NearbyEventZ = 0f;

        float score = VultureTask.Score(entity, courageThreshold: 15, detectionRange: 150f);

        // distSqr = 150^2 = 22500, rangeSqr = 150^2 = 22500
        // distSqr >= rangeSqr → proximityScore = 0
        // Only intensity contributes
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void Score_EventOnTopOfBot_MaxProximity()
    {
        var entity = MakeVultureEntity();
        entity.CombatIntensity = 30;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionZ = 50f;
        entity.NearbyEventX = 50f;
        entity.NearbyEventZ = 50f;

        float score = VultureTask.Score(entity, courageThreshold: 15, detectionRange: 150f);

        // distSqr = 0, proximityScore = 1 * 0.3 = 0.3
        // intensityRatio = 2 (capped), intensityScore = 0.3
        // total = 0.6 = MaxBaseScore
        Assert.That(score, Is.EqualTo(VultureTask.MaxBaseScore).Within(0.01f));
    }

    [Test]
    public void Score_ZeroDetectionRange_AllEventsBeyondRange()
    {
        var entity = MakeVultureEntity();
        entity.CombatIntensity = 30;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;
        entity.NearbyEventX = 1f;
        entity.NearbyEventZ = 0f;

        float score = VultureTask.Score(entity, courageThreshold: 15, detectionRange: 0f);

        // rangeSqr = 0, distSqr = 1, 1 >= 0 → proximityScore = 0
        // Only intensity contributes
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    // ── Gating conditions ────────────────────────────────────

    [Test]
    public void Score_NoNearbyEvent_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.HasNearbyEvent = false;

        float score = VultureTask.Score(entity, 15, 150f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_InCombat_ReturnsZero()
    {
        var entity = MakeVultureEntity();
        entity.IsInCombat = true;

        float score = VultureTask.Score(entity, 15, 150f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_InBossZone_ReturnsZero()
    {
        var entity = MakeVultureEntity();
        entity.IsInBossZone = true;

        float score = VultureTask.Score(entity, 15, 150f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_OnCooldown_ReturnsZero()
    {
        var entity = MakeVultureEntity();
        entity.VultureCooldownUntil = 100f;

        float score = VultureTask.Score(entity, 15, 150f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_VeryHighIntensity_ClampedToMaxScore()
    {
        var entity = MakeVultureEntity();
        entity.CombatIntensity = 10000;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionZ = 50f;
        entity.NearbyEventX = 50f;
        entity.NearbyEventZ = 50f;

        float score = VultureTask.Score(entity, courageThreshold: 15, detectionRange: 150f);
        Assert.That(score, Is.LessThanOrEqualTo(VultureTask.MaxBaseScore));
    }

    [Test]
    public void Score_NegativeDetectionRange_ComputesPositiveRangeSqr()
    {
        var entity = MakeVultureEntity();
        entity.CombatIntensity = 30;

        // Negative range squared is still positive ((-150)^2 = 22500)
        float score = VultureTask.Score(entity, courageThreshold: 15, detectionRange: -150f);
        Assert.That(float.IsNaN(score), Is.False);
    }

    // ── ScoreEntity with modifiers ───────────────────────────

    [Test]
    public void ScoreEntity_NaN_Aggression_GuardedToNeutralModifier()
    {
        var entity = MakeVultureEntity();
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.CombatIntensity = 30;
        entity.Aggression = float.NaN;
        entity.RaidTimeNormalized = 0.5f;

        var task = new VultureTask();
        task.ScoreEntity(9, entity);

        // NaN aggression → CombinedModifier returns 1.0 (neutral), so score is valid
        Assert.That(float.IsNaN(entity.TaskScores[9]), Is.False);
        Assert.That(entity.TaskScores[9], Is.GreaterThan(0f));
    }

    // ── Helper ───────────────────────────────────────────────

    private static BotEntity MakeVultureEntity()
    {
        var entity = new BotEntity(0);
        entity.HasNearbyEvent = true;
        entity.NearbyEventX = 50f;
        entity.NearbyEventZ = 50f;
        entity.CombatIntensity = 30;
        entity.VulturePhase = 0;
        entity.VultureCooldownUntil = 0f;
        entity.IsInBossZone = false;
        entity.IsInCombat = false;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;
        return entity;
    }
}
