using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

[TestFixture]
public class BotLodCalculatorTests
{
    // ── ComputeTier ────────────────────────────────────────────

    [Test]
    public void ComputeTier_BelowReducedThreshold_ReturnsFull()
    {
        // 100^2 = 10000, reduced = 150^2 = 22500
        byte tier = BotLodCalculator.ComputeTier(10000f, 22500f, 90000f);
        Assert.AreEqual(BotLodCalculator.TierFull, tier);
    }

    [Test]
    public void ComputeTier_BetweenThresholds_ReturnsReduced()
    {
        // 200^2 = 40000, between reduced=22500 and minimal=90000
        byte tier = BotLodCalculator.ComputeTier(40000f, 22500f, 90000f);
        Assert.AreEqual(BotLodCalculator.TierReduced, tier);
    }

    [Test]
    public void ComputeTier_AboveMinimalThreshold_ReturnsMinimal()
    {
        // 400^2 = 160000, above minimal=90000
        byte tier = BotLodCalculator.ComputeTier(160000f, 22500f, 90000f);
        Assert.AreEqual(BotLodCalculator.TierMinimal, tier);
    }

    [Test]
    public void ComputeTier_ExactlyAtReducedThreshold_ReturnsReduced()
    {
        byte tier = BotLodCalculator.ComputeTier(22500f, 22500f, 90000f);
        Assert.AreEqual(BotLodCalculator.TierReduced, tier);
    }

    [Test]
    public void ComputeTier_ExactlyAtMinimalThreshold_ReturnsMinimal()
    {
        byte tier = BotLodCalculator.ComputeTier(90000f, 22500f, 90000f);
        Assert.AreEqual(BotLodCalculator.TierMinimal, tier);
    }

    [Test]
    public void ComputeTier_ZeroDistance_ReturnsFull()
    {
        byte tier = BotLodCalculator.ComputeTier(0f, 22500f, 90000f);
        Assert.AreEqual(BotLodCalculator.TierFull, tier);
    }

    [Test]
    public void ComputeTier_JustBelowReducedThreshold_ReturnsFull()
    {
        byte tier = BotLodCalculator.ComputeTier(22499f, 22500f, 90000f);
        Assert.AreEqual(BotLodCalculator.TierFull, tier);
    }

    [Test]
    public void ComputeTier_JustBelowMinimalThreshold_ReturnsReduced()
    {
        byte tier = BotLodCalculator.ComputeTier(89999f, 22500f, 90000f);
        Assert.AreEqual(BotLodCalculator.TierReduced, tier);
    }

    // ── ShouldSkipUpdate ───────────────────────────────────────

    [Test]
    public void ShouldSkipUpdate_TierFull_NeverSkips()
    {
        for (int frame = 0; frame < 10; frame++)
        {
            Assert.IsFalse(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierFull, frame, 2, 4));
        }
    }

    [Test]
    public void ShouldSkipUpdate_TierReduced_Skip2_UpdatesEveryThirdFrame()
    {
        // skip=2 means cycle length = 3: update on frame%3==0, skip on 1 and 2
        Assert.IsFalse(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, 0, 2, 4));
        Assert.IsTrue(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, 1, 2, 4));
        Assert.IsTrue(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, 2, 2, 4));
        Assert.IsFalse(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, 3, 2, 4));
        Assert.IsTrue(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, 4, 2, 4));
        Assert.IsTrue(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, 5, 2, 4));
    }

    [Test]
    public void ShouldSkipUpdate_TierMinimal_Skip4_UpdatesEveryFifthFrame()
    {
        // skip=4 means cycle length = 5: update on frame%5==0
        Assert.IsFalse(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierMinimal, 0, 2, 4));
        Assert.IsTrue(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierMinimal, 1, 2, 4));
        Assert.IsTrue(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierMinimal, 2, 2, 4));
        Assert.IsTrue(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierMinimal, 3, 2, 4));
        Assert.IsTrue(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierMinimal, 4, 2, 4));
        Assert.IsFalse(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierMinimal, 5, 2, 4));
    }

    [Test]
    public void ShouldSkipUpdate_ReducedSkipZero_NeverSkips()
    {
        // skip=0 means cycle length = 1: update every frame
        for (int frame = 0; frame < 10; frame++)
        {
            Assert.IsFalse(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, frame, 0, 4));
        }
    }

    [Test]
    public void ShouldSkipUpdate_MinimalSkipZero_NeverSkips()
    {
        // skip=0 means cycle length = 1: update every frame
        for (int frame = 0; frame < 10; frame++)
        {
            Assert.IsFalse(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierMinimal, frame, 2, 0));
        }
    }

    [Test]
    public void ShouldSkipUpdate_TierReduced_Skip1_UpdatesEveryOtherFrame()
    {
        // skip=1 means cycle length = 2
        Assert.IsFalse(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, 0, 1, 4));
        Assert.IsTrue(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, 1, 1, 4));
        Assert.IsFalse(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, 2, 1, 4));
        Assert.IsTrue(BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, 3, 1, 4));
    }

    // ── Tier Constants ─────────────────────────────────────────

    [Test]
    public void TierConstants_HaveExpectedValues()
    {
        Assert.AreEqual(0, BotLodCalculator.TierFull);
        Assert.AreEqual(1, BotLodCalculator.TierReduced);
        Assert.AreEqual(2, BotLodCalculator.TierMinimal);
    }
}
