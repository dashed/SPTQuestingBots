using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Core;
using SPTQuestingBots.ZoneMovement.Fields;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement.Core;

[TestFixture]
public class AdvectionZoneLoaderTests
{
    private AdvectionField field;
    private System.Random deterministicRandom;

    [SetUp]
    public void SetUp()
    {
        field = new AdvectionField();
        deterministicRandom = new System.Random(42);
    }

    // --- LoadAndInjectZones ---

    [Test]
    public void LoadAndInjectZones_NullField_ReturnsZero()
    {
        int count = AdvectionZoneLoader.LoadAndInjectZones(null, new Dictionary<string, Vector3>(), "bigmap", null, 0f);

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void LoadAndInjectZones_UnknownMap_ReturnsZero()
    {
        int count = AdvectionZoneLoader.LoadAndInjectZones(
            field,
            new Dictionary<string, Vector3>(),
            "unknownmap",
            null,
            0f,
            deterministicRandom
        );

        Assert.That(count, Is.EqualTo(0));
        Assert.That(field.BoundedZoneCount, Is.EqualTo(0));
    }

    [Test]
    public void LoadAndInjectZones_FactoryMap_ReturnsZero()
    {
        int count = AdvectionZoneLoader.LoadAndInjectZones(
            field,
            new Dictionary<string, Vector3>(),
            "factory4_day",
            null,
            0f,
            deterministicRandom
        );

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void LoadAndInjectZones_BigmapWithCentroids_InjectsBuiltinZones()
    {
        var centroids = new Dictionary<string, Vector3>
        {
            ["ZoneDormitory"] = new Vector3(100, 0, 200),
            ["ZoneGasStation"] = new Vector3(300, 0, 400),
            ["ZoneScavBase"] = new Vector3(-100, 0, -50),
            ["ZoneOldAZS"] = new Vector3(50, 0, 150),
        };

        int count = AdvectionZoneLoader.LoadAndInjectZones(field, centroids, "bigmap", null, 0f, deterministicRandom);

        Assert.That(count, Is.EqualTo(4));
        Assert.That(field.BoundedZoneCount, Is.EqualTo(4));
    }

    [Test]
    public void LoadAndInjectZones_BigmapPartialCentroids_InjectsOnlyMatched()
    {
        var centroids = new Dictionary<string, Vector3> { ["ZoneDormitory"] = new Vector3(100, 0, 200) };

        int count = AdvectionZoneLoader.LoadAndInjectZones(field, centroids, "bigmap", null, 0f, deterministicRandom);

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void LoadAndInjectZones_Laboratory_InjectsCustomZone()
    {
        int count = AdvectionZoneLoader.LoadAndInjectZones(
            field,
            new Dictionary<string, Vector3>(),
            "laboratory",
            null,
            0f,
            deterministicRandom
        );

        Assert.That(count, Is.EqualTo(1));
        Assert.That(field.BoundedZoneCount, Is.EqualTo(1));
    }

    [Test]
    public void LoadAndInjectZones_Shoreline_InjectsCustomZones()
    {
        int count = AdvectionZoneLoader.LoadAndInjectZones(
            field,
            new Dictionary<string, Vector3>(),
            "shoreline",
            null,
            0f,
            deterministicRandom
        );

        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void LoadAndInjectZones_NullCentroids_StillInjectsCustomZones()
    {
        int count = AdvectionZoneLoader.LoadAndInjectZones(field, null, "laboratory", null, 0f, deterministicRandom);

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void LoadAndInjectZones_WithOverrides_UsesOverride()
    {
        var overrides = new Dictionary<string, AdvectionMapZones>(StringComparer.OrdinalIgnoreCase)
        {
            ["bigmap"] = new AdvectionMapZones(
                new Dictionary<string, BuiltinZoneEntry>(StringComparer.OrdinalIgnoreCase),
                new List<CustomZoneEntry> { new CustomZoneEntry(0f, 0f, 1.0f, 1.0f, 100f) }
            ),
        };

        var centroids = new Dictionary<string, Vector3> { ["ZoneDormitory"] = new Vector3(100, 0, 200) };

        int count = AdvectionZoneLoader.LoadAndInjectZones(field, centroids, "bigmap", overrides, 0f, deterministicRandom);

        Assert.That(count, Is.EqualTo(1));
    }

    // --- SampleForce ---

    [Test]
    public void SampleForce_EqualMinMax_ReturnsMin()
    {
        var entry = new AdvectionZoneEntry { ForceMin = 1.5f, ForceMax = 1.5f };
        var rng = new System.Random(1);

        float result = AdvectionZoneLoader.SampleForce(entry, rng);

        Assert.That(result, Is.EqualTo(1.5f));
    }

    [Test]
    public void SampleForce_Range_WithinBounds()
    {
        var entry = new AdvectionZoneEntry { ForceMin = -1f, ForceMax = 2f };

        for (int i = 0; i < 100; i++)
        {
            float result = AdvectionZoneLoader.SampleForce(entry, new System.Random(i));
            Assert.That(result, Is.InRange(-1f, 2f));
        }
    }

    [Test]
    public void SampleForce_MinGreaterThanMax_ReturnsMin()
    {
        var entry = new AdvectionZoneEntry { ForceMin = 3f, ForceMax = 1f };
        var rng = new System.Random(1);

        float result = AdvectionZoneLoader.SampleForce(entry, rng);

        Assert.That(result, Is.EqualTo(3f));
    }

    // --- ComputeTimeMultiplier ---

    [Test]
    public void ComputeTimeMultiplier_AtStart_ReturnsEarlyMultiplier()
    {
        var entry = new AdvectionZoneEntry { EarlyMultiplier = 1.5f, LateMultiplier = 0.5f };

        float result = AdvectionZoneLoader.ComputeTimeMultiplier(entry, 0f);

        Assert.That(result, Is.EqualTo(1.5f).Within(0.001f));
    }

    [Test]
    public void ComputeTimeMultiplier_AtEnd_ReturnsLateMultiplier()
    {
        var entry = new AdvectionZoneEntry { EarlyMultiplier = 1.5f, LateMultiplier = 0.5f };

        float result = AdvectionZoneLoader.ComputeTimeMultiplier(entry, 1f);

        Assert.That(result, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void ComputeTimeMultiplier_AtMidpoint_ReturnsMidValue()
    {
        var entry = new AdvectionZoneEntry { EarlyMultiplier = 1.5f, LateMultiplier = 0.5f };

        float result = AdvectionZoneLoader.ComputeTimeMultiplier(entry, 0.5f);

        Assert.That(result, Is.EqualTo(1.0f).Within(0.001f));
    }

    [Test]
    public void ComputeTimeMultiplier_DefaultMultipliers_AlwaysOne()
    {
        var entry = new AdvectionZoneEntry();

        Assert.That(AdvectionZoneLoader.ComputeTimeMultiplier(entry, 0f), Is.EqualTo(1.0f));
        Assert.That(AdvectionZoneLoader.ComputeTimeMultiplier(entry, 0.5f), Is.EqualTo(1.0f));
        Assert.That(AdvectionZoneLoader.ComputeTimeMultiplier(entry, 1f), Is.EqualTo(1.0f));
    }

    [Test]
    public void ComputeTimeMultiplier_NegativeTime_ClampsToZero()
    {
        var entry = new AdvectionZoneEntry { EarlyMultiplier = 2.0f, LateMultiplier = 0.5f };

        float result = AdvectionZoneLoader.ComputeTimeMultiplier(entry, -0.5f);

        Assert.That(result, Is.EqualTo(2.0f).Within(0.001f));
    }

    [Test]
    public void ComputeTimeMultiplier_BeyondOne_ClampsToOne()
    {
        var entry = new AdvectionZoneEntry { EarlyMultiplier = 2.0f, LateMultiplier = 0.5f };

        float result = AdvectionZoneLoader.ComputeTimeMultiplier(entry, 1.5f);

        Assert.That(result, Is.EqualTo(0.5f).Within(0.001f));
    }
}
