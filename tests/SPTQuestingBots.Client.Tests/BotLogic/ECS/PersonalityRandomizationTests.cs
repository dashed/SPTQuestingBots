using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS;

[TestFixture]
public class PersonalityRandomizationTests
{
    [Test]
    public void FromDifficulty_Normal_ProducesVariety()
    {
        var rng = new System.Random(42);
        var counts = new Dictionary<byte, int>();

        for (int i = 0; i < 1000; i++)
        {
            byte personality = PersonalityHelper.FromDifficulty(1, rng); // normal difficulty
            if (!counts.ContainsKey(personality))
                counts[personality] = 0;
            counts[personality]++;
        }

        // Should get at least 2 distinct personalities (not all Normal)
        Assert.That(counts.Count, Is.GreaterThanOrEqualTo(2), "Normal difficulty should produce at least 2 distinct personalities");

        // Normal (center) should be the most common (~60%)
        Assert.That(counts[BotPersonality.Normal], Is.GreaterThan(500), "Normal should be the majority (~60%)");
    }

    [Test]
    public void FromDifficulty_Normal_CenterIsMostCommon()
    {
        var rng = new System.Random(123);
        int centerCount = 0;
        int adjacentCount = 0;

        for (int i = 0; i < 1000; i++)
        {
            byte p = PersonalityHelper.FromDifficulty(1, rng);
            if (p == BotPersonality.Normal)
                centerCount++;
            else
                adjacentCount++;
        }

        Assert.That(centerCount, Is.GreaterThan(adjacentCount), "Center personality should be more common than adjacent");
    }

    [Test]
    public void FromDifficulty_Easy_ProducesCautiousAndAdjacent()
    {
        var rng = new System.Random(42);
        var seen = new HashSet<byte>();

        for (int i = 0; i < 1000; i++)
            seen.Add(PersonalityHelper.FromDifficulty(0, rng));

        Assert.That(seen, Does.Contain(BotPersonality.Cautious), "Easy difficulty should produce Cautious");
        Assert.That(seen, Does.Contain(BotPersonality.Timid).Or.Contain(BotPersonality.Normal),
            "Easy difficulty should produce adjacent personalities (Timid or Normal)");
    }

    [Test]
    public void FromDifficulty_Hard_ProducesAggressiveAndAdjacent()
    {
        var rng = new System.Random(42);
        var seen = new HashSet<byte>();

        for (int i = 0; i < 1000; i++)
            seen.Add(PersonalityHelper.FromDifficulty(2, rng));

        Assert.That(seen, Does.Contain(BotPersonality.Aggressive));
        Assert.That(seen, Does.Contain(BotPersonality.Normal).Or.Contain(BotPersonality.Reckless));
    }

    [Test]
    public void FromDifficulty_Impossible_ProducesRecklessAndAdjacent()
    {
        var rng = new System.Random(42);
        var seen = new HashSet<byte>();

        for (int i = 0; i < 1000; i++)
            seen.Add(PersonalityHelper.FromDifficulty(3, rng));

        Assert.That(seen, Does.Contain(BotPersonality.Reckless));
    }

    [Test]
    public void FromDifficulty_NeverProducesOutOfRange()
    {
        var rng = new System.Random(42);

        for (int difficulty = 0; difficulty <= 3; difficulty++)
        {
            for (int i = 0; i < 1000; i++)
            {
                byte p = PersonalityHelper.FromDifficulty(difficulty, rng);
                Assert.That(p, Is.InRange((byte)0, (byte)4),
                    $"Personality {p} from difficulty {difficulty} is out of range");
            }
        }
    }

    [Test]
    public void FromDifficulty_ClampsAtBoundaries()
    {
        var rng = new System.Random(42);

        // Easy (Cautious=1): center-1=0 (Timid, valid), center+1=2 (Normal, valid)
        // Impossible (Reckless=4): center-1=3 (Aggressive, valid), center+1=5 → clamped to 4 (Reckless)
        for (int i = 0; i < 500; i++)
        {
            byte p = PersonalityHelper.FromDifficulty(3, rng);
            Assert.That(p, Is.LessThanOrEqualTo(BotPersonality.Reckless));
        }
    }

    [Test]
    public void FromDifficulty_UnknownDifficulty_UsesRandomFallback()
    {
        var rng = new System.Random(42);
        var seen = new HashSet<byte>();

        for (int i = 0; i < 1000; i++)
            seen.Add(PersonalityHelper.FromDifficulty(99, rng));

        // RandomFallback should produce variety
        Assert.That(seen.Count, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void GetAggression_AllPersonalities_ReturnExpectedValues()
    {
        Assert.That(PersonalityHelper.GetAggression(BotPersonality.Timid), Is.EqualTo(0.1f).Within(0.01f));
        Assert.That(PersonalityHelper.GetAggression(BotPersonality.Cautious), Is.EqualTo(0.3f).Within(0.01f));
        Assert.That(PersonalityHelper.GetAggression(BotPersonality.Normal), Is.EqualTo(0.5f).Within(0.01f));
        Assert.That(PersonalityHelper.GetAggression(BotPersonality.Aggressive), Is.EqualTo(0.7f).Within(0.01f));
        Assert.That(PersonalityHelper.GetAggression(BotPersonality.Reckless), Is.EqualTo(0.9f).Within(0.01f));
    }
}
