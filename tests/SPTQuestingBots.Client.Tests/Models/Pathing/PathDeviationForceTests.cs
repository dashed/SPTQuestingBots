using System;
using NUnit.Framework;
using SPTQuestingBots.Models.Pathing;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.Models.Pathing;

[TestFixture]
public class PathDeviationForceTests
{
    private const float Epsilon = 0.01f;

    // --- ComputeDeviation ---

    [Test]
    public void ComputeDeviation_BotOnPath_ReturnsZero()
    {
        // Bot is exactly on the path segment midpoint
        var bot = new Vector3(5, 0, 0);
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 0, 0);

        var deviation = PathDeviationForce.ComputeDeviation(bot, start, end);

        Assert.AreEqual(0f, deviation.x, Epsilon);
        Assert.AreEqual(0f, deviation.y, Epsilon);
        Assert.AreEqual(0f, deviation.z, Epsilon);
    }

    [Test]
    public void ComputeDeviation_BotOffPath_PullsToward()
    {
        // Bot is 5 units north of an east-west path segment
        var bot = new Vector3(5, 0, 5);
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 0, 0);

        var deviation = PathDeviationForce.ComputeDeviation(bot, start, end);

        // Should pull south (negative Z) toward the path
        Assert.AreEqual(0f, deviation.x, Epsilon);
        Assert.AreEqual(0f, deviation.y, Epsilon);
        Assert.AreEqual(-5f, deviation.z, Epsilon);
    }

    [Test]
    public void ComputeDeviation_BotBeforeSegmentStart_ClamsToStart()
    {
        // Bot is behind the start of the segment
        var bot = new Vector3(-5, 0, 3);
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 0, 0);

        var deviation = PathDeviationForce.ComputeDeviation(bot, start, end);

        // Closest point is clamped to start (0,0,0), deviation points to (0,0,0)
        Assert.AreEqual(5f, deviation.x, Epsilon);
        Assert.AreEqual(0f, deviation.y, Epsilon);
        Assert.AreEqual(-3f, deviation.z, Epsilon);
    }

    [Test]
    public void ComputeDeviation_BotAfterSegmentEnd_ClampsToEnd()
    {
        // Bot is beyond the end of the segment
        var bot = new Vector3(15, 0, 4);
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 0, 0);

        var deviation = PathDeviationForce.ComputeDeviation(bot, start, end);

        // Closest point is clamped to end (10,0,0)
        Assert.AreEqual(-5f, deviation.x, Epsilon);
        Assert.AreEqual(0f, deviation.y, Epsilon);
        Assert.AreEqual(-4f, deviation.z, Epsilon);
    }

    [Test]
    public void ComputeDeviation_DegenerateSegment_PointsToStart()
    {
        // Start == End → degenerate segment
        var bot = new Vector3(3, 0, 4);
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(0, 0, 0);

        var deviation = PathDeviationForce.ComputeDeviation(bot, start, end);

        Assert.AreEqual(-3f, deviation.x, Epsilon);
        Assert.AreEqual(0f, deviation.y, Epsilon);
        Assert.AreEqual(-4f, deviation.z, Epsilon);
    }

    [Test]
    public void ComputeDeviation_DiagonalSegment_PerpendicularDeviation()
    {
        // Path goes diagonal (0,0,0) → (10,0,10). Bot at (0,0,10) → off path
        var bot = new Vector3(0, 0, 10);
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 0, 10);

        var deviation = PathDeviationForce.ComputeDeviation(bot, start, end);

        // Closest point on diagonal should be (5,0,5) → deviation = (5,0,-5)
        Assert.AreEqual(5f, deviation.x, Epsilon);
        Assert.AreEqual(0f, deviation.y, Epsilon);
        Assert.AreEqual(-5f, deviation.z, Epsilon);
    }

    [Test]
    public void ComputeDeviation_YAxisIgnored()
    {
        // Bot has different Y but same XZ as path
        var bot = new Vector3(5, 10, 0);
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 0, 0);

        var deviation = PathDeviationForce.ComputeDeviation(bot, start, end);

        // Should return zero deviation (XZ plane match)
        Assert.AreEqual(0f, deviation.x, Epsilon);
        Assert.AreEqual(0f, deviation.y, Epsilon);
        Assert.AreEqual(0f, deviation.z, Epsilon);
    }

    [Test]
    public void ComputeDeviation_ResultHasZeroY()
    {
        var bot = new Vector3(5, 50, 10);
        var start = new Vector3(0, 20, 0);
        var end = new Vector3(10, 30, 0);

        var deviation = PathDeviationForce.ComputeDeviation(bot, start, end);

        // Y component must always be 0
        Assert.AreEqual(0f, deviation.y, Epsilon);
    }

    // --- BlendWithDeviation ---

    [Test]
    public void BlendWithDeviation_ZeroDeviation_ReturnsMoveDirection()
    {
        var move = new Vector3(1, 0, 0);
        var deviation = new Vector3(0, 0, 0);

        var result = PathDeviationForce.BlendWithDeviation(move, deviation);

        Assert.AreEqual(1f, result.x, Epsilon);
        Assert.AreEqual(0f, result.z, Epsilon);
    }

    [Test]
    public void BlendWithDeviation_PerpendicularDeviation_BlendsDirection()
    {
        // Moving east, deviation pulls north
        var move = new Vector3(1, 0, 0);
        var deviation = new Vector3(0, 0, 1);

        var result = PathDeviationForce.BlendWithDeviation(move, deviation);

        // Result should be northeast, normalized
        float expected = 1f / (float)Math.Sqrt(2);
        Assert.AreEqual(expected, result.x, Epsilon);
        Assert.AreEqual(expected, result.z, Epsilon);
    }

    [Test]
    public void BlendWithDeviation_StrengthZero_ReturnsMoveDirection()
    {
        var move = new Vector3(1, 0, 0);
        var deviation = new Vector3(0, 0, 10);

        var result = PathDeviationForce.BlendWithDeviation(move, deviation, strength: 0f);

        Assert.AreEqual(1f, result.x, Epsilon);
        Assert.AreEqual(0f, result.z, Epsilon);
    }

    [Test]
    public void BlendWithDeviation_StrengthHalf_ScalesDeviation()
    {
        var move = new Vector3(1, 0, 0);
        var deviation = new Vector3(0, 0, 2);

        var fullBlend = PathDeviationForce.BlendWithDeviation(move, deviation, strength: 1f);
        var halfBlend = PathDeviationForce.BlendWithDeviation(move, deviation, strength: 0.5f);

        // Half strength should produce less Z component than full
        Assert.Less(Math.Abs(halfBlend.z), Math.Abs(fullBlend.z));
    }

    [Test]
    public void BlendWithDeviation_ResultIsNormalized()
    {
        var move = new Vector3(3, 0, 0);
        var deviation = new Vector3(0, 0, 4);

        var result = PathDeviationForce.BlendWithDeviation(move, deviation);

        float magnitude = (float)Math.Sqrt(result.x * result.x + result.z * result.z);
        Assert.AreEqual(1f, magnitude, Epsilon);
    }

    [Test]
    public void BlendWithDeviation_OpposingDeviation_ReducesMoveDirection()
    {
        // Moving east, deviation pulls west
        var move = new Vector3(1, 0, 0);
        var deviation = new Vector3(-0.5f, 0, 0);

        var result = PathDeviationForce.BlendWithDeviation(move, deviation);

        // Should still go east but less so? No, result is (0.5, 0, 0) normalized → (1,0,0)
        Assert.AreEqual(1f, result.x, Epsilon);
    }

    [Test]
    public void BlendWithDeviation_YComponentAlwaysZero()
    {
        var move = new Vector3(1, 5, 0);
        var deviation = new Vector3(0, 3, 1);

        var result = PathDeviationForce.BlendWithDeviation(move, deviation);

        Assert.AreEqual(0f, result.y, Epsilon);
    }
}
