using NUnit.Framework;

namespace SPTQuestingBots.Server.Tests.Services;

/// <summary>
/// Tests validating the bot cap adjustment logic, specifically that
/// the floor guard prevents negative bot caps.
/// </summary>
[TestFixture]
public class BotCapAdjustmentTests
{
    /// <summary>
    /// Simulates the fixed bot cap adjustment formula:
    /// <c>Math.Max(0, currentCap + fixedAdjustment)</c>.
    /// This is the formula used in <c>BotLocationService.UseEftBotCaps</c>.
    /// </summary>
    private static int ApplyBotCapAdjustment(int currentCap, int fixedAdjustment)
    {
        return Math.Max(0, currentCap + fixedAdjustment);
    }

    [Test]
    public void ApplyBotCapAdjustment_PositiveAdjustment_IncreasesCapAboveZero()
    {
        Assert.That(ApplyBotCapAdjustment(10, 5), Is.EqualTo(15));
    }

    [Test]
    public void ApplyBotCapAdjustment_NegativeAdjustment_DecreasesCapButNotBelowZero()
    {
        // Cap is 10, adjustment is -15, result should be 0 not -5
        Assert.That(ApplyBotCapAdjustment(10, -15), Is.EqualTo(0));
    }

    [Test]
    public void ApplyBotCapAdjustment_LargeNegativeAdjustment_FloorsAtZero()
    {
        Assert.That(ApplyBotCapAdjustment(5, -100), Is.EqualTo(0));
    }

    [Test]
    public void ApplyBotCapAdjustment_ExactNegation_ResultsInZero()
    {
        Assert.That(ApplyBotCapAdjustment(10, -10), Is.EqualTo(0));
    }

    [Test]
    public void ApplyBotCapAdjustment_ZeroAdjustment_NoChange()
    {
        Assert.That(ApplyBotCapAdjustment(10, 0), Is.EqualTo(10));
    }

    [Test]
    public void ApplyBotCapAdjustment_ZeroCap_NegativeAdjustment_StaysZero()
    {
        Assert.That(ApplyBotCapAdjustment(0, -5), Is.EqualTo(0));
    }

    [Test]
    public void ApplyBotCapAdjustment_ZeroCap_PositiveAdjustment_IncreasesCap()
    {
        Assert.That(ApplyBotCapAdjustment(0, 3), Is.EqualTo(3));
    }

    /// <summary>
    /// Demonstrates that without the Math.Max(0, ...) guard, the old formula
    /// <c>currentCap + fixedAdjustment</c> could produce negative values.
    /// </summary>
    [Test]
    public void WithoutFloorGuard_NegativeAdjustment_WouldGoNegative()
    {
        int currentCap = 10;
        int fixedAdjustment = -15;
        int unfixedResult = currentCap + fixedAdjustment;

        Assert.That(unfixedResult, Is.LessThan(0), "Without floor guard, result is negative");
        Assert.That(ApplyBotCapAdjustment(currentCap, fixedAdjustment), Is.EqualTo(0), "With floor guard, result is 0");
    }
}
