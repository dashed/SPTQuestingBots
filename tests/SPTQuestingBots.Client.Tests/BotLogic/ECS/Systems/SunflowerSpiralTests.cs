using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

[TestFixture]
public class SunflowerSpiralTests
{
    [Test]
    public void Generate_CountZero_ReturnsZero()
    {
        var buf = new float[32];
        int result = SunflowerSpiral.Generate(0f, 0f, 10f, 0, buf);
        Assert.AreEqual(0, result);
    }

    [Test]
    public void Generate_CountOne_ReturnsOneAtCenter()
    {
        var buf = new float[2];
        int result = SunflowerSpiral.Generate(5f, 7f, 10f, 1, buf);

        Assert.AreEqual(1, result);
        // i=0 → r = innerRadius * sqrt(0/1) = 0, so position is at center
        Assert.AreEqual(5f, buf[0], 1e-5f, "X should be at center");
        Assert.AreEqual(7f, buf[1], 1e-5f, "Z should be at center");
    }

    [Test]
    public void Generate_Count16_ReturnsAllPositionsWithinRadius()
    {
        float cx = 10f,
            cz = 20f,
            radius = 15f;
        int count = 16;
        var buf = new float[count * 2];

        int result = SunflowerSpiral.Generate(cx, cz, radius, count, buf);
        Assert.AreEqual(count, result);

        for (int i = 0; i < count; i++)
        {
            float dx = buf[i * 2] - cx;
            float dz = buf[i * 2 + 1] - cz;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            Assert.LessOrEqual(dist, radius + 0.001f, $"Point {i} at distance {dist} exceeds radius {radius}");
        }
    }

    [Test]
    public void Generate_PositionsAreEvenlyDistributed()
    {
        float cx = 0f,
            cz = 0f,
            radius = 100f;
        int count = 64;
        var buf = new float[count * 2];

        SunflowerSpiral.Generate(cx, cz, radius, count, buf);

        // Find minimum inter-point distance
        float minDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                float dx = buf[i * 2] - buf[j * 2];
                float dz = buf[i * 2 + 1] - buf[j * 2 + 1];
                float dist = (float)Math.Sqrt(dx * dx + dz * dz);
                if (dist < minDist)
                {
                    minDist = dist;
                }
            }
        }

        // Sunflower spirals should have reasonable spacing (not clumped)
        // For 64 points in radius 100, minimum distance should be > 5
        Assert.Greater(minDist, 5f, "Points should be evenly distributed, not clumped");
    }

    [Test]
    public void GoldenAngle_MatchesExpectedValue()
    {
        float expected = (float)(Math.PI * (3.0 - Math.Sqrt(5.0)));
        Assert.AreEqual(expected, SunflowerSpiral.GoldenAngle, 1e-6f);
        // Approximately 2.3999 radians
        Assert.AreEqual(2.3999f, SunflowerSpiral.GoldenAngle, 0.001f);
    }

    [Test]
    public void ComputeSampleEpsilon_Radius10Count16_ReturnsExpected()
    {
        // 0.886 * 10 / sqrt(16) = 0.886 * 10 / 4 = 2.215
        float result = SunflowerSpiral.ComputeSampleEpsilon(10f, 16);
        Assert.AreEqual(2.215f, result, 0.001f);
    }

    [Test]
    public void ComputeSampleEpsilon_CountZero_ReturnsZero()
    {
        float result = SunflowerSpiral.ComputeSampleEpsilon(10f, 0);
        Assert.AreEqual(0f, result);
    }

    [Test]
    public void ComputeSampleEpsilon_CountOne_ReturnsRadiusTimesCoeff()
    {
        float radius = 20f;
        // 0.886 * 20 / sqrt(1) = 17.72
        float result = SunflowerSpiral.ComputeSampleEpsilon(radius, 1);
        Assert.AreEqual(0.886f * radius, result, 0.001f);
    }

    [Test]
    public void Generate_LargeCount64_AllWithinRadius()
    {
        float cx = -50f,
            cz = 30f,
            radius = 25f;
        int count = 64;
        var buf = new float[count * 2];

        int result = SunflowerSpiral.Generate(cx, cz, radius, count, buf);
        Assert.AreEqual(count, result);

        for (int i = 0; i < count; i++)
        {
            float dx = buf[i * 2] - cx;
            float dz = buf[i * 2 + 1] - cz;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            Assert.LessOrEqual(dist, radius + 0.001f);
        }
    }

    [Test]
    public void Generate_NullBuffer_ReturnsZero()
    {
        int result = SunflowerSpiral.Generate(0f, 0f, 10f, 16, null);
        Assert.AreEqual(0, result);
    }

    [Test]
    public void Generate_BufferSmallerThanCountTimes2_GeneratesOnlyWhatFits()
    {
        // Buffer for 4 positions but request 16
        var buf = new float[8];
        int result = SunflowerSpiral.Generate(0f, 0f, 10f, 16, buf);
        Assert.AreEqual(4, result);
    }

    [Test]
    public void Generate_NegativeRadius_ReturnsZero()
    {
        var buf = new float[32];
        int result = SunflowerSpiral.Generate(0f, 0f, -5f, 8, buf);
        Assert.AreEqual(0, result);
    }

    [Test]
    public void ComputeSampleEpsilon_NegativeRadius_ReturnsZero()
    {
        float result = SunflowerSpiral.ComputeSampleEpsilon(-10f, 16);
        Assert.AreEqual(0f, result);
    }

    [Test]
    public void Generate_NegativeCount_ReturnsZero()
    {
        var buf = new float[32];
        int result = SunflowerSpiral.Generate(0f, 0f, 10f, -5, buf);
        Assert.AreEqual(0, result);
    }

    [Test]
    public void ComputeSampleEpsilon_NegativeCount_ReturnsZero()
    {
        float result = SunflowerSpiral.ComputeSampleEpsilon(10f, -3);
        Assert.AreEqual(0f, result);
    }
}
