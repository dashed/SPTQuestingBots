using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS;

[TestFixture]
public class BotPersonalityTests
{
    // ── Constants ───────────────────────────────────────────

    [Test]
    public void BotPersonality_Constants_HaveExpectedValues()
    {
        Assert.That(BotPersonality.Timid, Is.EqualTo((byte)0));
        Assert.That(BotPersonality.Cautious, Is.EqualTo((byte)1));
        Assert.That(BotPersonality.Normal, Is.EqualTo((byte)2));
        Assert.That(BotPersonality.Aggressive, Is.EqualTo((byte)3));
        Assert.That(BotPersonality.Reckless, Is.EqualTo((byte)4));
    }

    // ── GetAggression ───────────────────────────────────────

    [Test]
    public void GetAggression_Timid_Returns01()
    {
        Assert.That(PersonalityHelper.GetAggression(BotPersonality.Timid), Is.EqualTo(0.1f).Within(0.001f));
    }

    [Test]
    public void GetAggression_Cautious_Returns03()
    {
        Assert.That(PersonalityHelper.GetAggression(BotPersonality.Cautious), Is.EqualTo(0.3f).Within(0.001f));
    }

    [Test]
    public void GetAggression_Normal_Returns05()
    {
        Assert.That(PersonalityHelper.GetAggression(BotPersonality.Normal), Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void GetAggression_Aggressive_Returns07()
    {
        Assert.That(PersonalityHelper.GetAggression(BotPersonality.Aggressive), Is.EqualTo(0.7f).Within(0.001f));
    }

    [Test]
    public void GetAggression_Reckless_Returns09()
    {
        Assert.That(PersonalityHelper.GetAggression(BotPersonality.Reckless), Is.EqualTo(0.9f).Within(0.001f));
    }

    [Test]
    public void GetAggression_OutOfRange_ReturnsNormalDefault()
    {
        Assert.That(PersonalityHelper.GetAggression(99), Is.EqualTo(0.5f).Within(0.001f));
    }

    // ── FromDifficulty ──────────────────────────────────────

    [Test]
    public void FromDifficulty_Easy_ReturnsCautious()
    {
        var rng = new System.Random(42);
        Assert.That(PersonalityHelper.FromDifficulty(0, rng), Is.EqualTo(BotPersonality.Cautious));
    }

    [Test]
    public void FromDifficulty_Normal_ReturnsNormal()
    {
        var rng = new System.Random(42);
        Assert.That(PersonalityHelper.FromDifficulty(1, rng), Is.EqualTo(BotPersonality.Normal));
    }

    [Test]
    public void FromDifficulty_Hard_ReturnsAggressive()
    {
        var rng = new System.Random(42);
        Assert.That(PersonalityHelper.FromDifficulty(2, rng), Is.EqualTo(BotPersonality.Aggressive));
    }

    [Test]
    public void FromDifficulty_Impossible_ReturnsReckless()
    {
        var rng = new System.Random(42);
        Assert.That(PersonalityHelper.FromDifficulty(3, rng), Is.EqualTo(BotPersonality.Reckless));
    }

    [Test]
    public void FromDifficulty_Unknown_ReturnsValidPersonality()
    {
        var rng = new System.Random(42);
        byte result = PersonalityHelper.FromDifficulty(99, rng);
        Assert.That(result, Is.LessThanOrEqualTo(BotPersonality.Reckless));
    }

    // ── RandomFallback ──────────────────────────────────────

    [Test]
    public void RandomFallback_AlwaysReturnsValidPersonality()
    {
        var rng = new System.Random(0);
        for (int i = 0; i < 100; i++)
        {
            byte result = PersonalityHelper.RandomFallback(rng);
            Assert.That(result, Is.LessThanOrEqualTo(BotPersonality.Reckless));
        }
    }

    [Test]
    public void RandomFallback_Distribution_ApproximatelyCorrect()
    {
        var rng = new System.Random(12345);
        int[] counts = new int[5];
        int total = 10000;

        for (int i = 0; i < total; i++)
        {
            byte result = PersonalityHelper.RandomFallback(rng);
            counts[result]++;
        }

        // Expected: 10% Timid, 25% Cautious, 35% Normal, 20% Aggressive, 10% Reckless
        // Allow generous tolerance (5%) for random variance
        Assert.That(counts[BotPersonality.Timid] / (float)total, Is.EqualTo(0.10f).Within(0.05f));
        Assert.That(counts[BotPersonality.Cautious] / (float)total, Is.EqualTo(0.25f).Within(0.05f));
        Assert.That(counts[BotPersonality.Normal] / (float)total, Is.EqualTo(0.35f).Within(0.05f));
        Assert.That(counts[BotPersonality.Aggressive] / (float)total, Is.EqualTo(0.20f).Within(0.05f));
        Assert.That(counts[BotPersonality.Reckless] / (float)total, Is.EqualTo(0.10f).Within(0.05f));
    }

    // ── BotEntity fields ────────────────────────────────────

    [Test]
    public void BotEntity_Personality_DefaultsToTimid()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.Personality, Is.EqualTo(BotPersonality.Timid));
    }

    [Test]
    public void BotEntity_Aggression_DefaultsToZero()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.Aggression, Is.EqualTo(0f));
    }

    [Test]
    public void BotEntity_RaidTimeNormalized_DefaultsToZero()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.RaidTimeNormalized, Is.EqualTo(0f));
    }

    [Test]
    public void BotEntity_PersonalityFields_CanBeSet()
    {
        var entity = new BotEntity(0);
        entity.Personality = BotPersonality.Aggressive;
        entity.Aggression = 0.7f;
        entity.RaidTimeNormalized = 0.5f;

        Assert.That(entity.Personality, Is.EqualTo(BotPersonality.Aggressive));
        Assert.That(entity.Aggression, Is.EqualTo(0.7f));
        Assert.That(entity.RaidTimeNormalized, Is.EqualTo(0.5f));
    }
}
