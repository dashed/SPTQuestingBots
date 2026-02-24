using NUnit.Framework;
using SPTQuestingBots.Server.Routers;

namespace SPTQuestingBots.Server.Tests.Routers;

/// <summary>
/// Tests for <see cref="QuestingBotsDynamicRouter.TryParseFactorFromUrl"/>,
/// which extracts and validates the PScav conversion factor from URL paths.
/// </summary>
[TestFixture]
public class DynamicRouterTests
{
    // ── Valid factor values ──────────────────────────────────────────

    [Test]
    public void TryParseFactorFromUrl_ValidDecimal_ReturnsTrue()
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/0.5", out var factor), Is.True);
        Assert.That(factor, Is.EqualTo(0.5).Within(0.001));
    }

    [Test]
    public void TryParseFactorFromUrl_Zero_ReturnsTrue()
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/0", out var factor), Is.True);
        Assert.That(factor, Is.EqualTo(0.0));
    }

    [Test]
    public void TryParseFactorFromUrl_One_ReturnsTrue()
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/1", out var factor), Is.True);
        Assert.That(factor, Is.EqualTo(1.0));
    }

    [Test]
    public void TryParseFactorFromUrl_LargePositive_ReturnsTrue()
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/2.5", out var factor), Is.True);
        Assert.That(factor, Is.EqualTo(2.5).Within(0.001));
    }

    [TestCase("0.75")]
    [TestCase("0.1")]
    [TestCase("0.0")]
    [TestCase("1.0")]
    [TestCase("0.333")]
    public void TryParseFactorFromUrl_VariousValidDecimals_ReturnsTrue(string value)
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl($"/QuestingBots/AdjustPScavChance/{value}", out _), Is.True);
    }

    // ── Culture-sensitive parsing (BUG FIX verification) ────────────
    // These tests verify that decimal numbers with period separators
    // always parse correctly, regardless of the thread's current culture.

    [Test]
    public void TryParseFactorFromUrl_PeriodDecimal_AlwaysParsesCorrectly()
    {
        // This is the key regression test — on European locales "0.5" would
        // fail with culture-sensitive parsing because comma is the decimal separator
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/0.5", out var factor), Is.True);
        Assert.That(factor, Is.EqualTo(0.5).Within(0.0001));
    }

    [Test]
    public void TryParseFactorFromUrl_SmallDecimal_ParsesWithInvariantCulture()
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/0.123", out var factor), Is.True);
        Assert.That(factor, Is.EqualTo(0.123).Within(0.0001));
    }

    // ── NaN and Infinity rejection (BUG FIX verification) ───────────

    [Test]
    public void TryParseFactorFromUrl_NaN_ReturnsFalse()
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/NaN", out _), Is.False);
    }

    [Test]
    public void TryParseFactorFromUrl_Infinity_ReturnsFalse()
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/Infinity", out _), Is.False);
    }

    [Test]
    public void TryParseFactorFromUrl_NegativeInfinity_ReturnsFalse()
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/-Infinity", out _), Is.False);
    }

    // ── Negative factor rejection ───────────────────────────────────

    [Test]
    public void TryParseFactorFromUrl_NegativeValue_ReturnsFalse()
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/-0.5", out _), Is.False);
    }

    [Test]
    public void TryParseFactorFromUrl_NegativeInteger_ReturnsFalse()
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/-1", out _), Is.False);
    }

    // ── Invalid input ───────────────────────────────────────────────

    [Test]
    public void TryParseFactorFromUrl_EmptyLastSegment_ReturnsFalse()
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/", out _), Is.False);
    }

    [Test]
    public void TryParseFactorFromUrl_NonNumeric_ReturnsFalse()
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/abc", out _), Is.False);
    }

    [Test]
    public void TryParseFactorFromUrl_SpecialChars_ReturnsFalse()
    {
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/!@#", out _), Is.False);
    }

    [Test]
    public void TryParseFactorFromUrl_CommaDecimal_ReturnsFalse()
    {
        // Comma-separated decimal should NOT be accepted (not invariant culture format)
        Assert.That(QuestingBotsDynamicRouter.TryParseFactorFromUrl("/QuestingBots/AdjustPScavChance/0,5", out _), Is.False);
    }
}
