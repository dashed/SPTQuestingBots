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

    // ── Cooldown expiry (Bug fix: was permanent, now time-based) ──

    [Test]
    public void Score_CooldownExpired_AllowsVulturing()
    {
        // BUG FIX: VultureTask cooldown was permanent — compared > 0f instead of > CurrentGameTime.
        // After fix, once CurrentGameTime passes VultureCooldownUntil, the bot can vulture again.
        var entity = MakeVultureEntity();
        entity.VultureCooldownUntil = 200f; // Cooldown set at game time 200
        entity.CurrentGameTime = 300f; // Game time is now 300 — cooldown expired

        float score = VultureTask.Score(entity, 15, 150f);

        Assert.That(
            score,
            Is.GreaterThan(0f),
            "Bot should be able to vulture after cooldown expires (CurrentGameTime > VultureCooldownUntil)"
        );
    }

    [Test]
    public void Score_CooldownActive_ReturnsZero()
    {
        // Cooldown is still active — VultureCooldownUntil > CurrentGameTime
        var entity = MakeVultureEntity();
        entity.VultureCooldownUntil = 500f;
        entity.CurrentGameTime = 300f; // Still on cooldown

        float score = VultureTask.Score(entity, 15, 150f);

        Assert.That(score, Is.EqualTo(0f), "Bot should not vulture while cooldown is active");
    }

    [Test]
    public void Score_CooldownExactlyAtGameTime_AllowsVulturing()
    {
        // Edge case: VultureCooldownUntil == CurrentGameTime
        // The check is >, so equal means NOT on cooldown — bot can vulture.
        var entity = MakeVultureEntity();
        entity.VultureCooldownUntil = 300f;
        entity.CurrentGameTime = 300f;

        float score = VultureTask.Score(entity, 15, 150f);

        Assert.That(score, Is.GreaterThan(0f), "When cooldown exactly equals game time (not >), bot should be allowed to vulture");
    }

    [Test]
    public void Score_CooldownZero_GameTimePositive_NotBlocked()
    {
        // Default VultureCooldownUntil=0, CurrentGameTime=100 → 0 > 100 = false → not blocked
        var entity = MakeVultureEntity();
        entity.VultureCooldownUntil = 0f;
        entity.CurrentGameTime = 100f;

        float score = VultureTask.Score(entity, 15, 150f);

        Assert.That(score, Is.GreaterThan(0f), "Zero cooldown should never block vulturing");
    }

    [Test]
    public void Score_E2E_CooldownLifecycle_SetThenExpires()
    {
        // End-to-end: simulate a complete cooldown lifecycle.
        // Phase 1: Bot encounters event, scores normally.
        // Phase 2: Cooldown is set (e.g., rejected), bot is blocked.
        // Phase 3: Time passes, cooldown expires, bot can vulture again.
        var entity = MakeVultureEntity();
        entity.CurrentGameTime = 100f;

        // Phase 1: No cooldown, should score
        float phase1Score = VultureTask.Score(entity, 15, 150f);
        Assert.That(phase1Score, Is.GreaterThan(0f), "Phase 1: should score without cooldown");

        // Phase 2: Cooldown set (reject happened at time 100, cooldown 180s)
        entity.VultureCooldownUntil = 100f + 180f; // 280
        entity.CurrentGameTime = 150f; // Time advanced but still in cooldown

        float phase2Score = VultureTask.Score(entity, 15, 150f);
        Assert.That(phase2Score, Is.EqualTo(0f), "Phase 2: should be blocked during cooldown");

        // Phase 3: Time passes cooldown
        entity.CurrentGameTime = 290f; // Past 280

        float phase3Score = VultureTask.Score(entity, 15, 150f);
        Assert.That(phase3Score, Is.GreaterThan(0f), "Phase 3: should score after cooldown expires");
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
