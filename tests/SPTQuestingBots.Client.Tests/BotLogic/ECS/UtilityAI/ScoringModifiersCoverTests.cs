using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI;

[TestFixture]
public class ScoringModifiersCoverTests
{
    // ── ComputeCoverInfluence ────────────────────────────────────

    [Test]
    public void ComputeCoverInfluence_InCover_Returns1()
    {
        float result = ScoringModifiers.ComputeCoverInfluence(true, false);
        Assert.That(result, Is.EqualTo(1f));
    }

    [Test]
    public void ComputeCoverInfluence_InCoverWithEnemy_Returns1()
    {
        float result = ScoringModifiers.ComputeCoverInfluence(true, true);
        Assert.That(result, Is.EqualTo(1f));
    }

    [Test]
    public void ComputeCoverInfluence_ExposedWithEnemy_Returns0()
    {
        float result = ScoringModifiers.ComputeCoverInfluence(false, true);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void ComputeCoverInfluence_NoCoverNoEnemy_ReturnsNeutral()
    {
        float result = ScoringModifiers.ComputeCoverInfluence(false, false);
        Assert.That(result, Is.EqualTo(0.5f));
    }

    // ── CoverModifier: neutral state ────────────────────────────

    [Test]
    public void CoverModifier_NeutralState_Returns1()
    {
        Assert.That(ScoringModifiers.CoverModifier(0.5f, BotActionTypeId.Ambush), Is.EqualTo(1f));
        Assert.That(ScoringModifiers.CoverModifier(0.5f, BotActionTypeId.GoToObjective), Is.EqualTo(1f));
        Assert.That(ScoringModifiers.CoverModifier(0.5f, BotActionTypeId.Snipe), Is.EqualTo(1f));
        Assert.That(ScoringModifiers.CoverModifier(0.5f, BotActionTypeId.HoldPosition), Is.EqualTo(1f));
        Assert.That(ScoringModifiers.CoverModifier(0.5f, BotActionTypeId.Investigate), Is.EqualTo(1f));
        Assert.That(ScoringModifiers.CoverModifier(0.5f, BotActionTypeId.Loot), Is.EqualTo(1f));
    }

    // ── CoverModifier: in cover boosts hold tasks ────────────────

    [Test]
    public void CoverModifier_InCover_BoostsAmbush()
    {
        float result = ScoringModifiers.CoverModifier(1f, BotActionTypeId.Ambush);
        Assert.That(result, Is.GreaterThan(1f));
    }

    [Test]
    public void CoverModifier_InCover_BoostsSnipe()
    {
        float result = ScoringModifiers.CoverModifier(1f, BotActionTypeId.Snipe);
        Assert.That(result, Is.GreaterThan(1f));
    }

    [Test]
    public void CoverModifier_InCover_BoostsHoldPosition()
    {
        float result = ScoringModifiers.CoverModifier(1f, BotActionTypeId.HoldPosition);
        Assert.That(result, Is.GreaterThan(1f));
    }

    [Test]
    public void CoverModifier_InCover_ReducesGoToObjective()
    {
        float result = ScoringModifiers.CoverModifier(1f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.LessThan(1f));
    }

    // ── CoverModifier: exposed boosts movement ───────────────────

    [Test]
    public void CoverModifier_Exposed_BoostsGoToObjective()
    {
        float result = ScoringModifiers.CoverModifier(0f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.GreaterThan(1f));
    }

    [Test]
    public void CoverModifier_Exposed_BoostsInvestigate()
    {
        float result = ScoringModifiers.CoverModifier(0f, BotActionTypeId.Investigate);
        Assert.That(result, Is.GreaterThan(1f));
    }

    [Test]
    public void CoverModifier_Exposed_ReducesAmbush()
    {
        float result = ScoringModifiers.CoverModifier(0f, BotActionTypeId.Ambush);
        Assert.That(result, Is.LessThan(1f));
    }

    [Test]
    public void CoverModifier_Exposed_ReducesLoot()
    {
        float result = ScoringModifiers.CoverModifier(0f, BotActionTypeId.Loot);
        Assert.That(result, Is.LessThan(1f));
    }

    // ── CoverModifier: unknown task returns 1 ───────────────────

    [Test]
    public void CoverModifier_UnknownTask_Returns1()
    {
        Assert.That(ScoringModifiers.CoverModifier(0f, 999), Is.EqualTo(1f));
        Assert.That(ScoringModifiers.CoverModifier(1f, 999), Is.EqualTo(1f));
    }

    // ── CoverModifier: clamping ─────────────────────────────────

    [Test]
    public void CoverModifier_NegativeInput_ClampsToZero()
    {
        float result = ScoringModifiers.CoverModifier(-0.5f, BotActionTypeId.Ambush);
        float expected = ScoringModifiers.CoverModifier(0f, BotActionTypeId.Ambush);
        Assert.That(result, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void CoverModifier_OverOneInput_ClampsToOne()
    {
        float result = ScoringModifiers.CoverModifier(1.5f, BotActionTypeId.Ambush);
        float expected = ScoringModifiers.CoverModifier(1f, BotActionTypeId.Ambush);
        Assert.That(result, Is.EqualTo(expected).Within(0.001f));
    }

    // ── DogFightModifier ────────────────────────────────────────

    [Test]
    public void DogFightModifier_NotInDogFight_Returns1()
    {
        Assert.That(ScoringModifiers.DogFightModifier(false, 0.5f, BotActionTypeId.Investigate), Is.EqualTo(1f));
    }

    [Test]
    public void DogFightModifier_InDogFight_BoostsInvestigate()
    {
        float result = ScoringModifiers.DogFightModifier(true, 0.5f, BotActionTypeId.Investigate);
        Assert.That(result, Is.GreaterThan(1f));
    }

    [Test]
    public void DogFightModifier_InDogFight_BoostsVulture()
    {
        float result = ScoringModifiers.DogFightModifier(true, 0.7f, BotActionTypeId.Vulture);
        Assert.That(result, Is.GreaterThan(1f));
    }

    [Test]
    public void DogFightModifier_InDogFight_ReducesLoot()
    {
        float result = ScoringModifiers.DogFightModifier(true, 0.5f, BotActionTypeId.Loot);
        Assert.That(result, Is.LessThan(1f));
    }

    [Test]
    public void DogFightModifier_InDogFight_ReducesGoTo()
    {
        float result = ScoringModifiers.DogFightModifier(true, 0.5f, BotActionTypeId.GoToObjective);
        Assert.That(result, Is.LessThan(1f));
    }

    [Test]
    public void DogFightModifier_InDogFight_ReducesLinger()
    {
        float result = ScoringModifiers.DogFightModifier(true, 0.5f, BotActionTypeId.Linger);
        Assert.That(result, Is.LessThan(1f));
    }

    [Test]
    public void DogFightModifier_InDogFight_AggressionScales()
    {
        float lowAggression = ScoringModifiers.DogFightModifier(true, 0.1f, BotActionTypeId.Investigate);
        float highAggression = ScoringModifiers.DogFightModifier(true, 0.9f, BotActionTypeId.Investigate);
        Assert.That(highAggression, Is.GreaterThan(lowAggression));
    }

    [Test]
    public void DogFightModifier_InDogFight_UnknownTask_Returns1()
    {
        Assert.That(ScoringModifiers.DogFightModifier(true, 0.5f, 999), Is.EqualTo(1f));
    }

    // ── Full CombinedModifier with cover + dogfight ─────────────

    [Test]
    public void CombinedModifier_WithCoverAndDogFight_DiffersFromBaseline()
    {
        float baseline = ScoringModifiers.CombinedModifier(0.5f, 0.5f, 0f, BotActionTypeId.Ambush);
        float withCover = ScoringModifiers.CombinedModifier(0.5f, 0.5f, 0f, 1f, false, BotActionTypeId.Ambush);
        Assert.That(withCover, Is.Not.EqualTo(baseline));
    }

    [Test]
    public void CombinedModifier_WithDogFight_AffectsLoot()
    {
        float noDogFight = ScoringModifiers.CombinedModifier(0.5f, 0.5f, 0f, 0.5f, false, BotActionTypeId.Loot);
        float withDogFight = ScoringModifiers.CombinedModifier(0.5f, 0.5f, 0f, 0.5f, true, BotActionTypeId.Loot);
        Assert.That(withDogFight, Is.LessThan(noDogFight));
    }

    [Test]
    public void CombinedModifier_NeutralCoverNoDogFight_MatchesOldOverload()
    {
        float old = ScoringModifiers.CombinedModifier(0.5f, 0.5f, 0f, BotActionTypeId.Ambush);
        float full = ScoringModifiers.CombinedModifier(0.5f, 0.5f, 0f, 0.5f, false, BotActionTypeId.Ambush);
        Assert.That(full, Is.EqualTo(old).Within(0.001f));
    }

    [Test]
    public void CombinedModifier_FullOverload_ClampsToMax()
    {
        // All boosting factors at max for a boosted task type
        float result = ScoringModifiers.CombinedModifier(0.0f, 1.0f, 1.0f, 1.0f, false, BotActionTypeId.Ambush);
        Assert.That(result, Is.LessThanOrEqualTo(ScoringModifiers.MaxCombinedModifier));
    }

    [Test]
    public void CombinedModifier_FullOverload_HandlesNaN()
    {
        float result = ScoringModifiers.CombinedModifier(float.NaN, 0.5f, 0f, 0.5f, false, BotActionTypeId.Ambush);
        Assert.That(result, Is.EqualTo(1f));
    }

    // ── Entity-level integration ─────────────────────────────────

    [Test]
    public void Entity_IsInCover_DefaultsFalse()
    {
        var entity = new SPTQuestingBots.BotLogic.ECS.BotEntity(0);
        Assert.That(entity.IsInCover, Is.False);
    }

    [Test]
    public void Entity_IsInDogFight_DefaultsFalse()
    {
        var entity = new SPTQuestingBots.BotLogic.ECS.BotEntity(0);
        Assert.That(entity.IsInDogFight, Is.False);
    }

    [Test]
    public void Entity_HasPushCapability_DefaultsFalse()
    {
        var entity = new SPTQuestingBots.BotLogic.ECS.BotEntity(0);
        Assert.That(entity.HasPushCapability, Is.False);
    }

    [Test]
    public void Entity_DogFightThresholds_DefaultZero()
    {
        var entity = new SPTQuestingBots.BotLogic.ECS.BotEntity(0);
        Assert.That(entity.DogFightIn, Is.EqualTo(0f));
        Assert.That(entity.DogFightOut, Is.EqualTo(0f));
    }
}
