using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Fields;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class AdvectionFieldTests
{
    [Test]
    public void NoZones_NoBots_ReturnsZero()
    {
        var field = new AdvectionField();

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        Assert.Multiple(() =>
        {
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(z, Is.EqualTo(0f));
        });
    }

    [Test]
    public void SingleZone_PointsTowardZone()
    {
        var field = new AdvectionField();
        // Zone to the east (positive X)
        field.AddZone(new Vector3(100, 0, 0), 1.0f);

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        // Should point in +X direction
        Assert.That(x, Is.GreaterThan(0.9f));
        Assert.That(Math.Abs(z), Is.LessThan(0.1f));
    }

    [Test]
    public void SingleZone_NorthDirection()
    {
        var field = new AdvectionField();
        // Zone to the north (positive Z)
        field.AddZone(new Vector3(0, 0, 100), 1.0f);

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        Assert.That(z, Is.GreaterThan(0.9f));
        Assert.That(Math.Abs(x), Is.LessThan(0.1f));
    }

    [Test]
    public void TwoZones_OppositeDirections_CancelsOut()
    {
        var field = new AdvectionField();
        // Equal zones in opposite directions
        field.AddZone(new Vector3(100, 0, 0), 1.0f);
        field.AddZone(new Vector3(-100, 0, 0), 1.0f);

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        // Should roughly cancel out
        Assert.That(Math.Abs(x), Is.LessThan(0.1f));
    }

    [Test]
    public void StrongerZone_DominatesDirection()
    {
        var field = new AdvectionField();
        field.AddZone(new Vector3(100, 0, 0), 10.0f); // Strong east
        field.AddZone(new Vector3(-100, 0, 0), 1.0f); // Weak west

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        // Should point east (strong zone dominates)
        Assert.That(x, Is.GreaterThan(0.5f));
    }

    [Test]
    public void CrowdRepulsion_PushesAwayFromBots()
    {
        var field = new AdvectionField(crowdRepulsionStrength: 10.0f);
        // No zones, just bot repulsion
        var botPositions = new List<Vector3> { new Vector3(10, 0, 0) }; // Bot to the east

        field.GetAdvection(new Vector3(0, 0, 0), botPositions, out float x, out float z);

        // Should push west (away from bot)
        Assert.That(x, Is.LessThan(-0.5f));
    }

    [Test]
    public void CrowdRepulsion_InverseSquareFalloff()
    {
        // Use a zone to the north as a baseline, then add bots at varying distances
        // to verify that closer bots produce stronger repulsion
        var field = new AdvectionField(crowdRepulsionStrength: 5.0f);
        field.AddZone(new Vector3(0, 0, 100), 1.0f); // Zone north

        // Near bot to the north: strong repulsion should reduce northward pull
        var nearBot = new List<Vector3> { new Vector3(0, 0, 5) };
        field.GetAdvection(new Vector3(0, 0, 0), nearBot, out float _, out float nearZ);

        // Far bot to the north: weak repulsion, zone attraction dominates
        var farBot = new List<Vector3> { new Vector3(0, 0, 30) };
        field.GetAdvection(new Vector3(0, 0, 0), farBot, out float _2, out float farZ);

        // Far bot allows stronger northward pull than near bot
        Assert.That(farZ, Is.GreaterThan(nearZ));
    }

    [Test]
    public void ZoneCount_TracksAddedZones()
    {
        var field = new AdvectionField();
        Assert.That(field.ZoneCount, Is.EqualTo(0));

        field.AddZone(new Vector3(0, 0, 0), 1f);
        field.AddZone(new Vector3(100, 0, 100), 2f);

        Assert.That(field.ZoneCount, Is.EqualTo(2));
    }

    [Test]
    public void OutputIsNormalized()
    {
        var field = new AdvectionField();
        field.AddZone(new Vector3(100, 0, 50), 5.0f);
        field.AddZone(new Vector3(-30, 0, 200), 3.0f);

        field.GetAdvection(new Vector3(0, 0, 0), null, out float x, out float z);

        float mag = (float)Math.Sqrt(x * x + z * z);
        // Should be either 0 (cancelled) or ~1 (normalized)
        Assert.That(mag, Is.EqualTo(1f).Within(0.01f).Or.EqualTo(0f).Within(0.01f));
    }
}
