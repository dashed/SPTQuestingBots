using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI;

[TestFixture]
public class PlayerProximityModifierTests
{
    // ── PlayerProximityModifier basic behavior ────────────────

    [Test]
    public void PlayerProximityModifier_ZeroProximity_ReturnsOne()
    {
        Assert.That(ScoringModifiers.PlayerProximityModifier(0f, BotActionTypeId.GoToObjective), Is.EqualTo(1f));
        Assert.That(ScoringModifiers.PlayerProximityModifier(0f, BotActionTypeId.Ambush), Is.EqualTo(1f));
        Assert.That(ScoringModifiers.PlayerProximityModifier(0f, BotActionTypeId.Loot), Is.EqualTo(1f));
    }

    [Test]
    public void PlayerProximityModifier_NegativeProximity_ReturnsOne()
    {
        Assert.That(ScoringModifiers.PlayerProximityModifier(-0.5f, BotActionTypeId.GoToObjective), Is.EqualTo(1f));
    }

    // ── GoToObjective reduced near player ────────────────────

    [Test]
    public void PlayerProximityModifier_GoToObjective_ReducedNearPlayer()
    {
        float far = ScoringModifiers.PlayerProximityModifier(0f, BotActionTypeId.GoToObjective);
        float close = ScoringModifiers.PlayerProximityModifier(1f, BotActionTypeId.GoToObjective);
        Assert.That(close, Is.LessThan(far));
        Assert.That(close, Is.EqualTo(0.7f).Within(0.01f));
    }

    // ── Ambush boosted near player ──────────────────────────

    [Test]
    public void PlayerProximityModifier_Ambush_BoostedNearPlayer()
    {
        float far = ScoringModifiers.PlayerProximityModifier(0f, BotActionTypeId.Ambush);
        float close = ScoringModifiers.PlayerProximityModifier(1f, BotActionTypeId.Ambush);
        Assert.That(close, Is.GreaterThan(far));
        Assert.That(close, Is.EqualTo(1.4f).Within(0.01f));
    }

    // ── Snipe boosted near player ───────────────────────────

    [Test]
    public void PlayerProximityModifier_Snipe_BoostedNearPlayer()
    {
        float close = ScoringModifiers.PlayerProximityModifier(1f, BotActionTypeId.Snipe);
        Assert.That(close, Is.EqualTo(1.3f).Within(0.01f));
    }

    // ── Investigate boosted near player ─────────────────────

    [Test]
    public void PlayerProximityModifier_Investigate_BoostedNearPlayer()
    {
        float close = ScoringModifiers.PlayerProximityModifier(1f, BotActionTypeId.Investigate);
        Assert.That(close, Is.EqualTo(1.3f).Within(0.01f));
    }

    // ── Loot reduced near player ────────────────────────────

    [Test]
    public void PlayerProximityModifier_Loot_ReducedNearPlayer()
    {
        float far = ScoringModifiers.PlayerProximityModifier(0f, BotActionTypeId.Loot);
        float close = ScoringModifiers.PlayerProximityModifier(1f, BotActionTypeId.Loot);
        Assert.That(close, Is.LessThan(far));
        Assert.That(close, Is.EqualTo(0.7f).Within(0.01f));
    }

    // ── Linger reduced near player ──────────────────────────

    [Test]
    public void PlayerProximityModifier_Linger_ReducedNearPlayer()
    {
        float close = ScoringModifiers.PlayerProximityModifier(1f, BotActionTypeId.Linger);
        Assert.That(close, Is.EqualTo(0.6f).Within(0.01f));
    }

    // ── Patrol reduced near player ──────────────────────────

    [Test]
    public void PlayerProximityModifier_Patrol_ReducedNearPlayer()
    {
        float close = ScoringModifiers.PlayerProximityModifier(1f, BotActionTypeId.Patrol);
        Assert.That(close, Is.EqualTo(0.8f).Within(0.01f));
    }

    // ── HoldPosition boosted near player ────────────────────

    [Test]
    public void PlayerProximityModifier_HoldPosition_BoostedNearPlayer()
    {
        float close = ScoringModifiers.PlayerProximityModifier(1f, BotActionTypeId.HoldPosition);
        Assert.That(close, Is.EqualTo(1.3f).Within(0.01f));
    }

    // ── Vulture boosted near player ─────────────────────────

    [Test]
    public void PlayerProximityModifier_Vulture_BoostedNearPlayer()
    {
        float close = ScoringModifiers.PlayerProximityModifier(1f, BotActionTypeId.Vulture);
        Assert.That(close, Is.EqualTo(1.2f).Within(0.01f));
    }

    // ── Unknown action returns 1 ────────────────────────────

    [Test]
    public void PlayerProximityModifier_UnknownAction_ReturnsOne()
    {
        Assert.That(ScoringModifiers.PlayerProximityModifier(1f, 999), Is.EqualTo(1f));
    }

    // ── Mid-range proximity interpolation ───────────────────

    [Test]
    public void PlayerProximityModifier_HalfProximity_InterpolatesCorrectly()
    {
        float halfAmbush = ScoringModifiers.PlayerProximityModifier(0.5f, BotActionTypeId.Ambush);
        Assert.That(halfAmbush, Is.EqualTo(1.2f).Within(0.01f)); // Lerp(1, 1.4, 0.5) = 1.2
    }

    // ── Clamps above 1.0 ────────────────────────────────────

    [Test]
    public void PlayerProximityModifier_ProximityAboveOne_ClampsToOne()
    {
        float result = ScoringModifiers.PlayerProximityModifier(2f, BotActionTypeId.Ambush);
        Assert.That(result, Is.EqualTo(1.4f).Within(0.01f)); // same as proximity=1.0
    }

    // ── CombinedModifier with player proximity ──────────────

    [Test]
    public void CombinedModifier_WithPlayerProximity_BoostsAmbushNearPlayer()
    {
        float noPlayer = ScoringModifiers.CombinedModifier(0.5f, 0.5f, 0f, BotActionTypeId.Ambush);
        float nearPlayer = ScoringModifiers.CombinedModifier(0.5f, 0.5f, 1f, BotActionTypeId.Ambush);
        Assert.That(nearPlayer, Is.GreaterThan(noPlayer));
    }

    [Test]
    public void CombinedModifier_WithPlayerProximity_ReducesGoToNearPlayer()
    {
        float noPlayer = ScoringModifiers.CombinedModifier(0.5f, 0.5f, 0f, BotActionTypeId.GoToObjective);
        float nearPlayer = ScoringModifiers.CombinedModifier(0.5f, 0.5f, 1f, BotActionTypeId.GoToObjective);
        Assert.That(nearPlayer, Is.LessThan(noPlayer));
    }

    [Test]
    public void CombinedModifier_WithPlayerProximity_RespectsCap()
    {
        // Aggressive bot + late raid + player nearby for Ambush: could exceed cap
        float result = ScoringModifiers.CombinedModifier(0.9f, 0.9f, 1f, BotActionTypeId.Ambush);
        Assert.That(result, Is.LessThanOrEqualTo(ScoringModifiers.MaxCombinedModifier));
    }

    [Test]
    public void CombinedModifier_BackwardCompatible_ThreeArgOverload()
    {
        // 3-arg overload should equal 4-arg with proximity=0
        float threeArg = ScoringModifiers.CombinedModifier(0.5f, 0.5f, BotActionTypeId.GoToObjective);
        float fourArg = ScoringModifiers.CombinedModifier(0.5f, 0.5f, 0f, BotActionTypeId.GoToObjective);
        Assert.That(threeArg, Is.EqualTo(fourArg).Within(0.001f));
    }

    // ── E2E: Entity scoring with player proximity ───────────

    [Test]
    public void E2E_BotNearPlayer_AmbushScoreHigherThanFarBot()
    {
        var nearEntity = new BotEntity(1)
        {
            Aggression = 0.5f,
            RaidTimeNormalized = 0.5f,
            HumanPlayerProximity = 0.8f,
            HasActiveObjective = true,
            CurrentQuestAction = QuestActionId.Ambush,
            IsCloseToObjective = true,
            TaskScores = new float[16],
        };

        var farEntity = new BotEntity(2)
        {
            Aggression = 0.5f,
            RaidTimeNormalized = 0.5f,
            HumanPlayerProximity = 0f,
            HasActiveObjective = true,
            CurrentQuestAction = QuestActionId.Ambush,
            IsCloseToObjective = true,
            TaskScores = new float[16],
        };

        var ambush = new AmbushTask();
        ambush.ScoreEntity(0, nearEntity);
        ambush.ScoreEntity(0, farEntity);

        Assert.That(nearEntity.TaskScores[0], Is.GreaterThan(farEntity.TaskScores[0]));
    }

    [Test]
    public void E2E_BotNearPlayer_GoToObjectiveScoreLowerThanFarBot()
    {
        var nearEntity = new BotEntity(1)
        {
            Aggression = 0.5f,
            RaidTimeNormalized = 0.5f,
            HumanPlayerProximity = 0.8f,
            HasActiveObjective = true,
            CurrentQuestAction = QuestActionId.MoveToPosition,
            DistanceToObjective = 100f,
            TaskScores = new float[16],
        };

        var farEntity = new BotEntity(2)
        {
            Aggression = 0.5f,
            RaidTimeNormalized = 0.5f,
            HumanPlayerProximity = 0f,
            HasActiveObjective = true,
            CurrentQuestAction = QuestActionId.MoveToPosition,
            DistanceToObjective = 100f,
            TaskScores = new float[16],
        };

        var goTo = new GoToObjectiveTask();
        goTo.ScoreEntity(0, nearEntity);
        goTo.ScoreEntity(0, farEntity);

        Assert.That(nearEntity.TaskScores[0], Is.LessThan(farEntity.TaskScores[0]));
    }
}
