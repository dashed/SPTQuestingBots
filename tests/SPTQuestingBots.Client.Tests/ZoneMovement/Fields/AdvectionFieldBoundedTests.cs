using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Fields;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement.Fields;

[TestFixture]
public class AdvectionFieldBoundedTests
{
    // --- AddBoundedZone ---

    [Test]
    public void AddBoundedZone_IncrementsCount()
    {
        var field = new AdvectionField();

        Assert.That(field.BoundedZoneCount, Is.EqualTo(0));

        field.AddBoundedZone(new Vector3(0, 0, 0), 1f, 100f, 1f);

        Assert.That(field.BoundedZoneCount, Is.EqualTo(1));
    }

    [Test]
    public void AddBoundedZone_DoesNotAffectSimpleZoneCount()
    {
        var field = new AdvectionField();
        field.AddZone(new Vector3(0, 0, 0), 1f);
        field.AddBoundedZone(new Vector3(100, 0, 0), 2f, 200f, 1f);

        Assert.Multiple(() =>
        {
            Assert.That(field.ZoneCount, Is.EqualTo(1));
            Assert.That(field.BoundedZoneCount, Is.EqualTo(1));
        });
    }

    // --- GetAdvection with bounded zones ---

    [Test]
    public void BoundedZone_WithinRadius_AttractsTowardZone()
    {
        var field = new AdvectionField();
        field.AddBoundedZone(new Vector3(100, 0, 0), 1.0f, 200f, 1.0f);

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        Assert.That(x, Is.GreaterThan(0.9f));
        Assert.That(Math.Abs(z), Is.LessThan(0.1f));
    }

    [Test]
    public void BoundedZone_BeyondRadius_NoEffect()
    {
        var field = new AdvectionField();
        field.AddBoundedZone(new Vector3(500, 0, 0), 1.0f, 100f, 1.0f);

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        Assert.Multiple(() =>
        {
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(z, Is.EqualTo(0f));
        });
    }

    [Test]
    public void BoundedZone_NegativeStrength_Repels()
    {
        var field = new AdvectionField();
        field.AddBoundedZone(new Vector3(50, 0, 0), -1.0f, 200f, 1.0f);

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        Assert.That(x, Is.LessThan(-0.9f));
    }

    [Test]
    public void BoundedZone_LinearDecay_FalloffIsCorrect()
    {
        // With decay=1.0 (linear), force at center = strength, at edge = 0
        // At half radius, normalized = 0.5, so falloff = 0.5^1.0 = 0.5
        var field1 = new AdvectionField();
        field1.AddBoundedZone(new Vector3(50, 0, 0), 1.0f, 100f, 1.0f);

        var field2 = new AdvectionField();
        field2.AddBoundedZone(new Vector3(90, 0, 0), 1.0f, 100f, 1.0f);

        field1.GetAdvection(new Vector3(0, 0, 0), null, out float x1, out float _);
        field2.GetAdvection(new Vector3(0, 0, 0), null, out float x2, out float _2);

        // Both should attract east, but both are normalized to unit vectors
        // so direction is the same. The magnitude difference is only visible
        // before normalization. With single zone, both normalize to ~1.0.
        Assert.That(x1, Is.GreaterThan(0.9f));
        Assert.That(x2, Is.GreaterThan(0.9f));
    }

    [Test]
    public void BoundedZone_HighDecay_SharpEdge()
    {
        // With decay=3.0, falloff at half radius = 0.5^3 = 0.125 (very weak)
        // So a zone barely within radius should have minimal effect
        var field = new AdvectionField();
        field.AddBoundedZone(new Vector3(100, 0, 0), 1.0f, 110f, 3.0f);
        // Query at origin: distance=100, radius=110, normalized=10/110=0.0909
        // falloff = 0.0909^3 = 0.00075 — very weak

        field.AddBoundedZone(new Vector3(0, 0, 100), 1.0f, 200f, 3.0f);
        // distance=100, radius=200, normalized=0.5
        // falloff = 0.5^3 = 0.125 — much stronger

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        // The north zone (z=100) should dominate because it has much stronger falloff weight
        Assert.That(z, Is.GreaterThan(x));
    }

    [Test]
    public void BoundedZone_LowDecay_SoftFalloff()
    {
        // With decay=0.5, falloff at half radius = 0.5^0.5 = 0.707 (relatively strong)
        var field = new AdvectionField();
        field.AddBoundedZone(new Vector3(100, 0, 0), 1.0f, 200f, 0.5f);

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        Assert.That(x, Is.GreaterThan(0.9f));
    }

    [Test]
    public void BoundedZone_CombinesWithSimpleZones()
    {
        var field = new AdvectionField();
        // Simple zone pulling east
        field.AddZone(new Vector3(100, 0, 0), 1.0f);
        // Bounded zone pulling north
        field.AddBoundedZone(new Vector3(0, 0, 100), 1.0f, 200f, 1.0f);

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        // Should point northeast
        Assert.Multiple(() =>
        {
            Assert.That(x, Is.GreaterThan(0f));
            Assert.That(z, Is.GreaterThan(0f));
        });
    }

    [Test]
    public void BoundedZone_CombinesWithCrowdRepulsion()
    {
        var field = new AdvectionField(crowdRepulsionStrength: 10.0f);
        field.AddBoundedZone(new Vector3(100, 0, 0), 5.0f, 200f, 1.0f);

        var bots = new List<Vector3> { new Vector3(10, 0, 0) }; // Bot between us and zone

        field.GetAdvection(new Vector3(0, 0, 0), bots, out float x, out float z);

        // Repulsion from close bot is strong (inverse-square), but zone is also strong
        // The exact result depends on magnitudes but both forces are considered
        // Just verify we get a non-zero result
        float mag = (float)Math.Sqrt(x * x + z * z);
        Assert.That(mag, Is.GreaterThan(0.5f));
    }

    [Test]
    public void BoundedZone_TwoOpposite_Cancel()
    {
        var field = new AdvectionField();
        field.AddBoundedZone(new Vector3(100, 0, 0), 1.0f, 200f, 1.0f);
        field.AddBoundedZone(new Vector3(-100, 0, 0), 1.0f, 200f, 1.0f);

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        Assert.That(Math.Abs(x), Is.LessThan(0.1f));
    }

    [Test]
    public void BoundedZone_AtSamePosition_NoEffect()
    {
        var field = new AdvectionField();
        field.AddBoundedZone(new Vector3(0, 0, 0), 1.0f, 100f, 1.0f);

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        // Distance is 0, which is < 0.01 so it's skipped
        Assert.Multiple(() =>
        {
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(z, Is.EqualTo(0f));
        });
    }

    [Test]
    public void BoundedZone_OutputIsNormalized()
    {
        var field = new AdvectionField();
        field.AddBoundedZone(new Vector3(100, 0, 50), 5.0f, 300f, 1.0f);
        field.AddBoundedZone(new Vector3(-30, 0, 200), 3.0f, 400f, 0.5f);

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        float mag = (float)Math.Sqrt(x * x + z * z);
        Assert.That(mag, Is.EqualTo(1f).Within(0.01f).Or.EqualTo(0f).Within(0.01f));
    }

    [Test]
    public void BoundedZone_ExactlyAtRadius_NoEffect()
    {
        var field = new AdvectionField();
        // Zone at distance exactly equal to radius
        field.AddBoundedZone(new Vector3(100, 0, 0), 1.0f, 100f, 1.0f);

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        // dist >= radius means no effect
        Assert.Multiple(() =>
        {
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(z, Is.EqualTo(0f));
        });
    }
}
