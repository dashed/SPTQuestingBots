using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

[TestFixture]
public class ZoneFollowerPositionCalculatorTests
{
    [Test]
    public void ZeroCandidates_AllNaN()
    {
        var seeds = new int[] { 42, 99 };
        var outPos = new float[6];

        ZoneFollowerPositionCalculator.DistributeFollowers(new float[] { 1, 2, 3 }, 0, seeds, 2, 5f, outPos);

        for (int i = 0; i < 6; i++)
        {
            Assert.That(outPos[i], Is.NaN, $"outPositions[{i}] should be NaN");
        }
    }

    [Test]
    public void NullCandidates_AllNaN()
    {
        var seeds = new int[] { 42, 99 };
        var outPos = new float[6];

        ZoneFollowerPositionCalculator.DistributeFollowers(null, 3, seeds, 2, 5f, outPos);

        for (int i = 0; i < 6; i++)
        {
            Assert.That(outPos[i], Is.NaN, $"outPositions[{i}] should be NaN");
        }
    }

    [Test]
    public void SingleCandidate_SingleFollower_NearCandidate()
    {
        float cx = 10f,
            cy = 5f,
            cz = 20f;
        var candidates = new float[] { cx, cy, cz };
        var seeds = new int[] { 12345 };
        var outPos = new float[3];
        float jitter = 3f;

        ZoneFollowerPositionCalculator.DistributeFollowers(candidates, 1, seeds, 1, jitter, outPos);

        float dx = outPos[0] - cx;
        float dz = outPos[2] - cz;
        float dist = (float)Math.Sqrt(dx * dx + dz * dz);

        Assert.That(dist, Is.LessThanOrEqualTo(jitter + 0.001f), "Output should be within jitterRadius of candidate");
    }

    [Test]
    public void SingleCandidate_MultipleFollowers_AllNearSameCandidate()
    {
        float cx = 100f,
            cy = 0f,
            cz = 200f;
        var candidates = new float[] { cx, cy, cz };
        var seeds = new int[] { 1, 2, 3, 4 };
        var outPos = new float[12];
        float jitter = 5f;

        ZoneFollowerPositionCalculator.DistributeFollowers(candidates, 1, seeds, 4, jitter, outPos);

        for (int i = 0; i < 4; i++)
        {
            float dx = outPos[i * 3] - cx;
            float dz = outPos[i * 3 + 2] - cz;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            Assert.That(dist, Is.LessThanOrEqualTo(jitter + 0.001f), $"Follower {i} should be within jitterRadius of the single candidate");
        }
    }

    [Test]
    public void MultipleCandidates_MultipleFollowers_RoundRobin()
    {
        // 3 candidates at distinct X positions, Y=0, Z=0
        var candidates = new float[] { 0f, 0f, 0f, 100f, 0f, 0f, 200f, 0f, 0f };

        // Use seeds that yield different offsets mod 3
        // Seed 0 → safeSeed=0, offset=0; Seed 1 → safeSeed=1, offset=1; Seed 2 → offset=2
        var seeds = new int[] { 0, 1, 2 };
        var outPos = new float[9];

        ZoneFollowerPositionCalculator.DistributeFollowers(candidates, 3, seeds, 3, 0f, outPos);

        // With jitter=0, each follower should be exactly at a candidate.
        // Follower 0: candIdx = (0 + 0%3) % 3 = 0 → X=0
        // Follower 1: candIdx = (1 + 1%3) % 3 = 2 → X=200
        // Follower 2: candIdx = (2 + 2%3) % 3 = 1 → X=100
        // Different followers end up at different candidates.
        float[] assignedX = { outPos[0], outPos[3], outPos[6] };
        Array.Sort(assignedX);

        Assert.That(assignedX[0], Is.EqualTo(0f).Within(0.001f), "One follower at candidate 0");
        Assert.That(assignedX[1], Is.EqualTo(100f).Within(0.001f), "One follower at candidate 1");
        Assert.That(assignedX[2], Is.EqualTo(200f).Within(0.001f), "One follower at candidate 2");
    }

    [Test]
    public void JitterRadiusZero_ExactMatch()
    {
        var candidates = new float[] { 10f, 5f, 20f, 30f, 15f, 40f };
        var seeds = new int[] { 42 };
        var outPos = new float[3];

        ZoneFollowerPositionCalculator.DistributeFollowers(candidates, 2, seeds, 1, 0f, outPos);

        // With jitter=0, the XZ output must exactly match a candidate.
        // Seed 42: safeSeed=42, offset=42%2=0, candIdx=(0+0)%2=0
        Assert.That(outPos[0], Is.EqualTo(10f).Within(0.001f), "X should exactly match candidate");
        Assert.That(outPos[2], Is.EqualTo(20f).Within(0.001f), "Z should exactly match candidate");
    }

    [Test]
    public void YUnchanged()
    {
        float candY1 = 7.5f,
            candY2 = 12.3f;
        var candidates = new float[] { 0f, candY1, 0f, 100f, candY2, 100f };
        var seeds = new int[] { 0, 1 };
        var outPos = new float[6];

        ZoneFollowerPositionCalculator.DistributeFollowers(candidates, 2, seeds, 2, 10f, outPos);

        // Follower 0: seed=0, offset=0%2=0, candIdx=(0+0)%2=0 → Y=candY1
        Assert.That(outPos[1], Is.EqualTo(candY1).Within(0.001f), "Y for follower 0 should match candidate Y");

        // Follower 1: seed=1, offset=1%2=1, candIdx=(1+1)%2=0 → Y=candY1
        Assert.That(outPos[4], Is.EqualTo(candY1).Within(0.001f), "Y for follower 1 should match candidate Y");
    }

    [Test]
    public void MoreFollowersThanCandidates_Wraps()
    {
        // 2 candidates, 5 followers — must wrap around without exceptions
        var candidates = new float[] { 0f, 0f, 0f, 50f, 0f, 50f };
        var seeds = new int[] { 0, 0, 0, 0, 0 };
        var outPos = new float[15];

        Assert.DoesNotThrow(() => ZoneFollowerPositionCalculator.DistributeFollowers(candidates, 2, seeds, 5, 1f, outPos));

        // All positions should be near one of the two candidates
        for (int i = 0; i < 5; i++)
        {
            float x = outPos[i * 3];
            float z = outPos[i * 3 + 2];
            float dist0 = (float)Math.Sqrt(x * x + z * z);
            float dist1 = (float)Math.Sqrt((x - 50f) * (x - 50f) + (z - 50f) * (z - 50f));
            float minDist = Math.Min(dist0, dist1);
            Assert.That(minDist, Is.LessThanOrEqualTo(1.001f), $"Follower {i} should be near a candidate");
        }
    }

    [Test]
    public void Deterministic_SameSeedsSameOutput()
    {
        var candidates = new float[] { 10f, 5f, 20f, 30f, 15f, 40f };
        var seeds = new int[] { 777, 888 };
        var outA = new float[6];
        var outB = new float[6];

        ZoneFollowerPositionCalculator.DistributeFollowers(candidates, 2, seeds, 2, 3f, outA);
        ZoneFollowerPositionCalculator.DistributeFollowers(candidates, 2, seeds, 2, 3f, outB);

        for (int i = 0; i < 6; i++)
        {
            Assert.That(outA[i], Is.EqualTo(outB[i]).Within(0.001f), $"Position [{i}] should be identical across calls");
        }
    }

    [Test]
    public void DifferentSeeds_DifferentJitter()
    {
        var candidates = new float[] { 50f, 0f, 50f };
        var seedsA = new int[] { 111 };
        var seedsB = new int[] { 999 };
        var outA = new float[3];
        var outB = new float[3];

        ZoneFollowerPositionCalculator.DistributeFollowers(candidates, 1, seedsA, 1, 5f, outA);
        ZoneFollowerPositionCalculator.DistributeFollowers(candidates, 1, seedsB, 1, 5f, outB);

        // With different seeds, at least one of X/Z should differ
        bool xDiffers = Math.Abs(outA[0] - outB[0]) > 0.001f;
        bool zDiffers = Math.Abs(outA[2] - outB[2]) > 0.001f;
        Assert.That(xDiffers || zDiffers, Is.True, "Different seeds should produce different jitter offsets");
    }

    [Test]
    public void IntMinValue_NoOverflow()
    {
        var candidates = new float[] { 10f, 0f, 20f };
        var seeds = new int[] { int.MinValue };
        var outPos = new float[3];

        Assert.DoesNotThrow(() => ZoneFollowerPositionCalculator.DistributeFollowers(candidates, 1, seeds, 1, 2f, outPos));

        // Output should be valid (not NaN, not infinite)
        Assert.That(float.IsNaN(outPos[0]), Is.False, "X should not be NaN");
        Assert.That(float.IsInfinity(outPos[0]), Is.False, "X should not be Infinity");
        Assert.That(float.IsNaN(outPos[2]), Is.False, "Z should not be NaN");
        Assert.That(float.IsInfinity(outPos[2]), Is.False, "Z should not be Infinity");
    }
}
