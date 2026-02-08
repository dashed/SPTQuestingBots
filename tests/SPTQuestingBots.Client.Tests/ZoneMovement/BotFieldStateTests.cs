using System;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Core;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class BotFieldStateTests
{
    [Test]
    public void ComputeMomentum_DelegatesToZoneMathUtils()
    {
        var state = new BotFieldState(42);
        state.PreviousDestination = new Vector3(0, 0, 0);

        var (momX, momZ) = state.ComputeMomentum(new Vector3(10, 0, 0));

        // Should match ZoneMathUtils.ComputeMomentum(from: (0,0,0), to: (10,0,0))
        Assert.That(momX, Is.EqualTo(1f).Within(0.001f));
        Assert.That(momZ, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void GetNoiseAngle_DifferentSeeds_DifferentAngles()
    {
        var state1 = new BotFieldState(100);
        var state2 = new BotFieldState(200);

        float angle1 = state1.GetNoiseAngle(10f);
        float angle2 = state2.GetNoiseAngle(10f);

        Assert.That(angle1, Is.Not.EqualTo(angle2).Within(0.0001f));
    }

    [Test]
    public void GetNoiseAngle_SameSeedAndTime_Deterministic()
    {
        var state1 = new BotFieldState(42);
        var state2 = new BotFieldState(42);

        float angle1 = state1.GetNoiseAngle(15f);
        float angle2 = state2.GetNoiseAngle(15f);

        Assert.That(angle1, Is.EqualTo(angle2).Within(0.0001f));
    }

    [Test]
    public void GetNoiseAngle_RangeIsPiToPi()
    {
        var state = new BotFieldState(123);

        // Sample across many time buckets
        for (float t = 0f; t < 100f; t += 0.5f)
        {
            float angle = state.GetNoiseAngle(t);
            Assert.That(angle, Is.GreaterThanOrEqualTo(-(float)Math.PI));
            Assert.That(angle, Is.LessThanOrEqualTo((float)Math.PI));
        }
    }
}
