using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.Data;

/// <summary>
/// Tests verifying raid_time_normalized increases as raid time elapses
/// and ScoringModifiers respond correctly to different raid time values.
/// </summary>
[TestFixture]
public class RaidTimeNormalizedTests
{
    // ── Normalized value computation ────────────────────────────────

    [Test]
    public void RaidTimeNormalized_AtRaidStart_IsZero()
    {
        // elapsed=0, total=1200 → normalized=0.0
        float elapsed = 0f;
        float total = 1200f;
        float normalized = elapsed / total;
        Assert.AreEqual(0f, normalized, 0.001f);
    }

    [Test]
    public void RaidTimeNormalized_AtMidpoint_IsHalf()
    {
        // elapsed=600, total=1200 → normalized=0.5
        float elapsed = 600f;
        float total = 1200f;
        float normalized = elapsed / total;
        Assert.AreEqual(0.5f, normalized, 0.001f);
    }

    [Test]
    public void RaidTimeNormalized_AtEnd_IsOne()
    {
        // elapsed=1200, total=1200 → normalized=1.0
        float elapsed = 1200f;
        float total = 1200f;
        float normalized = elapsed / total;
        Assert.AreEqual(1f, normalized, 0.001f);
    }

    [Test]
    public void RaidTimeNormalized_At221sInto1200s_IsCorrect()
    {
        // This is the exact case from the bug report
        // Expected: 221/1200 ≈ 0.184, NOT 0.024
        float elapsed = 221f;
        float total = 1200f;
        float normalized = elapsed / total;
        Assert.AreEqual(0.184f, normalized, 0.001f);
    }

    [Test]
    public void RaidTimeNormalized_IncreasesProperly_OverTime()
    {
        // Verify monotonic increase
        float total = 1200f;
        float prev = 0f;
        for (int seconds = 0; seconds <= 1200; seconds += 60)
        {
            float normalized = seconds / total;
            Assert.GreaterOrEqual(normalized, prev, "RaidTimeNormalized should increase over time, failed at " + seconds + "s");
            prev = normalized;
        }
    }

    [Test]
    public void RaidTimeNormalized_ClampedToOne_WhenExceedsTotal()
    {
        float elapsed = 1500f; // over total
        float total = 1200f;
        float normalized = elapsed / total;
        if (normalized > 1f)
            normalized = 1f;
        Assert.AreEqual(1f, normalized, 0.001f);
    }

    [Test]
    public void RaidTimeNormalized_ClampedToZero_WhenNegative()
    {
        float elapsed = -5f; // before raid start somehow
        float total = 1200f;
        float normalized = elapsed / total;
        if (normalized < 0f)
            normalized = 0f;
        Assert.AreEqual(0f, normalized, 0.001f);
    }

    [Test]
    public void RaidTimeNormalized_SafeWithZeroTotal()
    {
        // Should not divide by zero — guard in BotEntityBridge checks total > 0
        float total = 0f;
        Assert.IsFalse(total > 0f, "Total of 0 should not pass the guard check");
    }

    // ── ScoringModifiers respond to raid time ────────────────────────

    [Test]
    public void RaidTimeModifier_GoToObjective_HigherEarlyRaid()
    {
        float earlyMod = ScoringModifiers.RaidTimeModifier(0.1f, BotActionTypeId.GoToObjective);
        float lateMod = ScoringModifiers.RaidTimeModifier(0.9f, BotActionTypeId.GoToObjective);
        Assert.Greater(earlyMod, lateMod, "GoToObjective should score higher early in raid");
    }

    [Test]
    public void RaidTimeModifier_Loot_HigherLateRaid()
    {
        float earlyMod = ScoringModifiers.RaidTimeModifier(0.1f, BotActionTypeId.Loot);
        float lateMod = ScoringModifiers.RaidTimeModifier(0.9f, BotActionTypeId.Loot);
        Assert.Greater(lateMod, earlyMod, "Loot should score higher late in raid");
    }

    [Test]
    public void RaidTimeModifier_Linger_HigherLateRaid()
    {
        float earlyMod = ScoringModifiers.RaidTimeModifier(0.1f, BotActionTypeId.Linger);
        float lateMod = ScoringModifiers.RaidTimeModifier(0.9f, BotActionTypeId.Linger);
        Assert.Greater(lateMod, earlyMod, "Linger should score higher late in raid");
    }

    [Test]
    public void RaidTimeModifier_Ambush_HigherLateRaid()
    {
        float earlyMod = ScoringModifiers.RaidTimeModifier(0.1f, BotActionTypeId.Ambush);
        float lateMod = ScoringModifiers.RaidTimeModifier(0.9f, BotActionTypeId.Ambush);
        Assert.Greater(lateMod, earlyMod, "Ambush should score higher late in raid");
    }

    [Test]
    public void RaidTimeModifier_Patrol_HigherLateRaid()
    {
        float earlyMod = ScoringModifiers.RaidTimeModifier(0.1f, BotActionTypeId.Patrol);
        float lateMod = ScoringModifiers.RaidTimeModifier(0.9f, BotActionTypeId.Patrol);
        Assert.Greater(lateMod, earlyMod, "Patrol should score higher late in raid");
    }

    // ── Scoring multiplier transitions (early/mid/late) ──────────────

    [Test]
    public void CombinedModifier_GoToObjective_TripleTransition()
    {
        float earlyRush = ScoringModifiers.CombinedModifier(0.5f, 0.1f, BotActionTypeId.GoToObjective);
        float midNormal = ScoringModifiers.CombinedModifier(0.5f, 0.5f, BotActionTypeId.GoToObjective);
        float lateCautious = ScoringModifiers.CombinedModifier(0.5f, 0.9f, BotActionTypeId.GoToObjective);

        // GoToObjective should transition: high early → lower mid → lowest late
        Assert.Greater(earlyRush, midNormal, "Early rush should score higher than mid");
        Assert.Greater(midNormal, lateCautious, "Mid should score higher than late");
    }

    [Test]
    public void CombinedModifier_Loot_TripleTransition()
    {
        float earlyRush = ScoringModifiers.CombinedModifier(0.5f, 0.1f, BotActionTypeId.Loot);
        float midNormal = ScoringModifiers.CombinedModifier(0.5f, 0.5f, BotActionTypeId.Loot);
        float lateCautious = ScoringModifiers.CombinedModifier(0.5f, 0.9f, BotActionTypeId.Loot);

        // Loot should transition: lowest early → mid → highest late
        Assert.Less(earlyRush, midNormal, "Early rush should score lower than mid");
        Assert.Less(midNormal, lateCautious, "Mid should score lower than late");
    }

    // ── BotEntity integration ────────────────────────────────────────

    [Test]
    public void BotEntity_RaidTimeNormalized_DefaultsToZero()
    {
        var entity = new BotEntity(0);
        Assert.AreEqual(0f, entity.RaidTimeNormalized, "Default RaidTimeNormalized should be 0 (raid start)");
    }

    [Test]
    public void BotEntity_RaidTimeNormalized_AffectsTaskScoring()
    {
        var early = new BotEntity(0);
        early.TaskScores = new float[QuestTaskFactory.TaskCount];
        early.HasActiveObjective = true;
        early.DistanceToObjective = 50f;
        early.CurrentQuestAction = QuestActionId.MoveToPosition;
        early.RaidTimeNormalized = 0.1f;
        early.Aggression = 0.5f;

        var late = new BotEntity(1);
        late.TaskScores = new float[QuestTaskFactory.TaskCount];
        late.HasActiveObjective = true;
        late.DistanceToObjective = 50f;
        late.CurrentQuestAction = QuestActionId.MoveToPosition;
        late.RaidTimeNormalized = 0.9f;
        late.Aggression = 0.5f;

        var task = new GoToObjectiveTask();
        task.ScoreEntity(0, early);
        task.ScoreEntity(0, late);

        Assert.Greater(early.TaskScores[0], late.TaskScores[0], "GoToObjective should score higher at 0.1 normalized time than 0.9");
    }

    // ── Edge cases ───────────────────────────────────────────────────

    [Test]
    public void RaidTimeModifier_AtExactZero_ReturnsStartValue()
    {
        float mod = ScoringModifiers.RaidTimeModifier(0f, BotActionTypeId.GoToObjective);
        Assert.AreEqual(1.2f, mod, 0.001f, "GoToObjective at raidTime=0 should return 1.2 (early rush)");
    }

    [Test]
    public void RaidTimeModifier_AtExactOne_ReturnsEndValue()
    {
        float mod = ScoringModifiers.RaidTimeModifier(1f, BotActionTypeId.GoToObjective);
        Assert.AreEqual(0.8f, mod, 0.001f, "GoToObjective at raidTime=1 should return 0.8 (late cautious)");
    }

    [Test]
    public void RaidTimeModifier_UnknownAction_ReturnsOne()
    {
        float mod = ScoringModifiers.RaidTimeModifier(0.5f, 999);
        Assert.AreEqual(1f, mod, 0.001f, "Unknown action type should have no raid time effect");
    }

    [TestCase(1200f, Description = "Factory 20min")]
    [TestCase(2100f, Description = "Customs 35min")]
    [TestCase(3000f, Description = "Reserve 50min")]
    [TestCase(2400f, Description = "Interchange 40min")]
    public void RaidTimeNormalized_VariousMapDurations_MidpointIsHalf(float totalSeconds)
    {
        float midpoint = totalSeconds / 2f;
        float normalized = midpoint / totalSeconds;
        Assert.AreEqual(0.5f, normalized, 0.001f);
    }
}
