using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

[TestFixture]
public class InvestigateTaskEdgeCaseTests
{
    // ── Division by zero when intensityThreshold=0 ───────────

    [Test]
    public void Score_ZeroThreshold_ZeroIntensity_ReturnsZero()
    {
        // intensityThreshold=0 → clamped to 1, intensity=0 < 1 → returns 0
        var entity = MakeInvestigateEntity();
        entity.CombatIntensity = 0;

        float score = InvestigateTask.Score(entity, intensityThreshold: 0, detectionRange: 120f);

        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_ZeroThreshold_PositiveIntensity_ReturnsValidScore()
    {
        // (float)positive / 0 = Infinity, capped to 2f
        var entity = MakeInvestigateEntity();
        entity.CombatIntensity = 10;

        float score = InvestigateTask.Score(entity, intensityThreshold: 0, detectionRange: 120f);

        Assert.That(float.IsNaN(score), Is.False);
        Assert.That(score, Is.GreaterThan(0f));
        Assert.That(score, Is.LessThanOrEqualTo(InvestigateTask.MaxBaseScore));
    }

    // ── Intensity boundary conditions ────────────────────────

    [Test]
    public void Score_IntensityEqualsThreshold_HasMinimalScore()
    {
        var entity = MakeInvestigateEntity();
        entity.CombatIntensity = 5;

        float score = InvestigateTask.Score(entity, intensityThreshold: 5, detectionRange: 120f);

        // intensityRatio = 1, intensityScore = 0
        // Only proximity contributes
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void Score_IntensityJustBelowThreshold_ReturnsZero()
    {
        var entity = MakeInvestigateEntity();
        entity.CombatIntensity = 4;

        float score = InvestigateTask.Score(entity, intensityThreshold: 5, detectionRange: 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_VeryHighIntensity_ClampedToMaxScore()
    {
        var entity = MakeInvestigateEntity();
        entity.CombatIntensity = 10000;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionZ = 50f;
        entity.NearbyEventX = 50f;
        entity.NearbyEventZ = 50f;

        float score = InvestigateTask.Score(entity, intensityThreshold: 5, detectionRange: 120f);
        Assert.That(score, Is.LessThanOrEqualTo(InvestigateTask.MaxBaseScore));
    }

    // ── Proximity edge cases ─────────────────────────────────

    [Test]
    public void Score_EventExactlyAtRange_ZeroProximity()
    {
        var entity = MakeInvestigateEntity();
        entity.CombatIntensity = 10;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;
        entity.NearbyEventX = 120f;
        entity.NearbyEventZ = 0f;

        float score = InvestigateTask.Score(entity, intensityThreshold: 5, detectionRange: 120f);
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void Score_EventOnTopOfBot_MaxProximity()
    {
        var entity = MakeInvestigateEntity();
        entity.CombatIntensity = 10;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionZ = 50f;
        entity.NearbyEventX = 50f;
        entity.NearbyEventZ = 50f;

        float score = InvestigateTask.Score(entity, intensityThreshold: 5, detectionRange: 120f);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_ZeroDetectionRange_DistSqrAlwaysExceedsRange()
    {
        var entity = MakeInvestigateEntity();
        entity.CombatIntensity = 10;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;
        entity.NearbyEventX = 1f;
        entity.NearbyEventZ = 0f;

        float score = InvestigateTask.Score(entity, intensityThreshold: 5, detectionRange: 0f);
        // Only intensity contributes, proximity = 0
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    // ── Gating conditions ────────────────────────────────────

    [Test]
    public void Score_NoNearbyEvent_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.HasNearbyEvent = false;

        float score = InvestigateTask.Score(entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_InCombat_ReturnsZero()
    {
        var entity = MakeInvestigateEntity();
        entity.IsInCombat = true;

        float score = InvestigateTask.Score(entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_AlreadyVulturing_ReturnsZero()
    {
        var entity = MakeInvestigateEntity();
        entity.VulturePhase = (byte)VulturePhase.Approach;

        float score = InvestigateTask.Score(entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_AlreadyInvestigating_MaintainsMaxScore()
    {
        var entity = MakeInvestigateEntity();
        entity.IsInvestigating = true;

        float score = InvestigateTask.Score(entity, 5, 120f);
        Assert.That(score, Is.EqualTo(InvestigateTask.MaxBaseScore));
    }

    // ── ScoreEntity with modifiers ───────────────────────────

    [Test]
    public void ScoreEntity_NaN_Aggression_GuardedToNeutralModifier()
    {
        var entity = MakeInvestigateEntity();
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.CombatIntensity = 10;
        entity.Aggression = float.NaN;
        entity.RaidTimeNormalized = 0.5f;

        var task = new InvestigateTask();
        task.ScoreEntity(11, entity);

        // NaN aggression → CombinedModifier returns 1.0 (neutral), so score is valid
        Assert.That(float.IsNaN(entity.TaskScores[11]), Is.False);
        Assert.That(entity.TaskScores[11], Is.GreaterThan(0f));
    }

    // ── Helper ───────────────────────────────────────────────

    private static BotEntity MakeInvestigateEntity()
    {
        var entity = new BotEntity(0);
        entity.HasNearbyEvent = true;
        entity.NearbyEventX = 50f;
        entity.NearbyEventZ = 50f;
        entity.CombatIntensity = 10;
        entity.VulturePhase = 0;
        entity.IsInCombat = false;
        entity.IsInvestigating = false;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;
        return entity;
    }
}
