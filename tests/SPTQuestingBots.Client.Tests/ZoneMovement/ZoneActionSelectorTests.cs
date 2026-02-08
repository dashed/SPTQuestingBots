using System;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Core;
using SPTQuestingBots.ZoneMovement.Selection;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class ZoneActionSelectorTests
{
    [Test]
    public void SelectActionIndex_Container_ReturnsValidAction()
    {
        var rng = new Random(42);
        int action = ZoneActionSelector.SelectActionIndex(PoiCategory.Container, rng);

        Assert.That(action, Is.InRange(0, 4));
    }

    [Test]
    public void SelectActionIndex_Synthetic_AlwaysMoveToPosition()
    {
        var rng = new Random(42);

        // Synthetic should always return MoveToPosition (0)
        for (int i = 0; i < 100; i++)
        {
            int action = ZoneActionSelector.SelectActionIndex(PoiCategory.Synthetic, rng);
            Assert.That(action, Is.EqualTo(ZoneActionSelector.Actions.MoveToPosition));
        }
    }

    [Test]
    public void SelectActionIndex_NullRng_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ZoneActionSelector.SelectActionIndex(PoiCategory.Container, null));
    }

    [Test]
    public void SelectActionIndex_Container_DistributionMatchesWeights()
    {
        var rng = new Random(12345);
        int ambushCount = 0,
            snipeCount = 0,
            holdCount = 0,
            plantCount = 0;
        const int trials = 10000;

        for (int i = 0; i < trials; i++)
        {
            int action = ZoneActionSelector.SelectActionIndex(PoiCategory.Container, rng);
            switch (action)
            {
                case ZoneActionSelector.Actions.Ambush:
                    ambushCount++;
                    break;
                case ZoneActionSelector.Actions.Snipe:
                    snipeCount++;
                    break;
                case ZoneActionSelector.Actions.HoldAtPosition:
                    holdCount++;
                    break;
                case ZoneActionSelector.Actions.PlantItem:
                    plantCount++;
                    break;
            }
        }

        // Container: 60% Ambush, 20% Snipe, 10% Hold, 10% Plant
        // Allow ±5% margin
        Assert.That(ambushCount / (float)trials, Is.EqualTo(0.60f).Within(0.05f), "Ambush");
        Assert.That(snipeCount / (float)trials, Is.EqualTo(0.20f).Within(0.05f), "Snipe");
        Assert.That(holdCount / (float)trials, Is.EqualTo(0.10f).Within(0.05f), "Hold");
        Assert.That(plantCount / (float)trials, Is.EqualTo(0.10f).Within(0.05f), "Plant");
    }

    [Test]
    public void SelectActionIndex_Exfil_DistributionMatchesWeights()
    {
        var rng = new Random(54321);
        int snipeCount = 0,
            ambushCount = 0,
            holdCount = 0;
        const int trials = 10000;

        for (int i = 0; i < trials; i++)
        {
            int action = ZoneActionSelector.SelectActionIndex(PoiCategory.Exfil, rng);
            switch (action)
            {
                case ZoneActionSelector.Actions.Snipe:
                    snipeCount++;
                    break;
                case ZoneActionSelector.Actions.Ambush:
                    ambushCount++;
                    break;
                case ZoneActionSelector.Actions.HoldAtPosition:
                    holdCount++;
                    break;
            }
        }

        // Exfil: 60% Snipe, 30% Ambush, 10% Hold
        Assert.That(snipeCount / (float)trials, Is.EqualTo(0.60f).Within(0.05f), "Snipe");
        Assert.That(ambushCount / (float)trials, Is.EqualTo(0.30f).Within(0.05f), "Ambush");
        Assert.That(holdCount / (float)trials, Is.EqualTo(0.10f).Within(0.05f), "Hold");
    }

    [Test]
    public void SelectActionIndex_SpawnPoint_MostlyMoveToPosition()
    {
        var rng = new Random(99);
        int moveCount = 0;
        const int trials = 10000;

        for (int i = 0; i < trials; i++)
        {
            int action = ZoneActionSelector.SelectActionIndex(PoiCategory.SpawnPoint, rng);
            if (action == ZoneActionSelector.Actions.MoveToPosition)
                moveCount++;
        }

        // SpawnPoint: 90% MoveToPosition
        Assert.That(moveCount / (float)trials, Is.EqualTo(0.90f).Within(0.05f));
    }

    [Test]
    public void SelectActionIndex_AllCategories_ReturnValidRange()
    {
        var rng = new Random(1);
        foreach (PoiCategory category in Enum.GetValues(typeof(PoiCategory)))
        {
            int action = ZoneActionSelector.SelectActionIndex(category, rng);
            Assert.That(action, Is.InRange(0, 4), $"Category {category} returned out-of-range action {action}");
        }
    }

    [Test]
    public void GetHoldDuration_MoveToPosition_ReturnsZero()
    {
        var (min, max) = ZoneActionSelector.GetHoldDuration(ZoneActionSelector.Actions.MoveToPosition);

        Assert.That(min, Is.EqualTo(0f));
        Assert.That(max, Is.EqualTo(0f));
    }

    [Test]
    public void GetHoldDuration_Ambush_ReturnsLongDuration()
    {
        var (min, max) = ZoneActionSelector.GetHoldDuration(ZoneActionSelector.Actions.Ambush);

        Assert.That(min, Is.EqualTo(30f));
        Assert.That(max, Is.EqualTo(120f));
        Assert.That(max, Is.GreaterThan(min));
    }

    [Test]
    public void GetHoldDuration_AllActions_MinLessThanOrEqualMax()
    {
        for (int i = 0; i <= 4; i++)
        {
            var (min, max) = ZoneActionSelector.GetHoldDuration(i);
            Assert.That(min, Is.LessThanOrEqualTo(max), $"Action index {i}");
        }
    }

    [Test]
    public void GetHoldDuration_InvalidIndex_ReturnsZero()
    {
        var (min, max) = ZoneActionSelector.GetHoldDuration(99);

        Assert.That(min, Is.EqualTo(0f));
        Assert.That(max, Is.EqualTo(0f));
    }

    [Test]
    public void GetWeightsForCategory_AllCategories_SumTo100()
    {
        foreach (PoiCategory category in Enum.GetValues(typeof(PoiCategory)))
        {
            var weights = ZoneActionSelector.GetWeightsForCategory(category);

            // Last cumulative weight should be 100
            Assert.That(weights[weights.Length - 1].cumulative, Is.EqualTo(100), $"Category {category} weights don't sum to 100");
        }
    }

    [Test]
    public void GetWeightsForCategory_AllCategories_CumulativeIsIncreasing()
    {
        foreach (PoiCategory category in Enum.GetValues(typeof(PoiCategory)))
        {
            var weights = ZoneActionSelector.GetWeightsForCategory(category);

            for (int i = 1; i < weights.Length; i++)
            {
                Assert.That(
                    weights[i].cumulative,
                    Is.GreaterThan(weights[i - 1].cumulative),
                    $"Category {category}: weights not strictly increasing at index {i}"
                );
            }
        }
    }
}
