using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Fields;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class ConvergenceFieldTests
{
    [Test]
    public void NoPlayers_ReturnsZero()
    {
        var field = new ConvergenceField();

        field.ComputeConvergence(new Vector3(0, 0, 0), null, out float x, out float z);

        Assert.Multiple(() =>
        {
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(z, Is.EqualTo(0f));
        });
    }

    [Test]
    public void SinglePlayer_PointsTowardPlayer()
    {
        var field = new ConvergenceField();
        var players = new List<Vector3> { new Vector3(100, 0, 0) };

        field.ComputeConvergence(new Vector3(0, 0, 0), players, out float x, out float z);

        // Should point in +X direction
        Assert.That(x, Is.GreaterThan(0.9f));
        Assert.That(Math.Abs(z), Is.LessThan(0.1f));
    }

    [Test]
    public void SinglePlayer_NorthDirection()
    {
        var field = new ConvergenceField();
        var players = new List<Vector3> { new Vector3(0, 0, 200) };

        field.ComputeConvergence(new Vector3(0, 0, 0), players, out float x, out float z);

        Assert.That(z, Is.GreaterThan(0.9f));
        Assert.That(Math.Abs(x), Is.LessThan(0.1f));
    }

    [Test]
    public void CachingReturnsSameValue_WithinInterval()
    {
        var field = new ConvergenceField(updateIntervalSec: 30f);
        var players = new List<Vector3> { new Vector3(100, 0, 0) };

        // First call: computes and caches
        field.GetConvergence(new Vector3(0, 0, 0), players, 0f, out float x1, out float z1);

        // Second call with different position but within interval: returns cached
        var differentPlayers = new List<Vector3> { new Vector3(-100, 0, 0) };
        field.GetConvergence(new Vector3(0, 0, 0), differentPlayers, 10f, out float x2, out float z2);

        Assert.Multiple(() =>
        {
            Assert.That(x2, Is.EqualTo(x1));
            Assert.That(z2, Is.EqualTo(z1));
        });
    }

    [Test]
    public void CacheExpires_AfterInterval()
    {
        var field = new ConvergenceField(updateIntervalSec: 5f);
        var players1 = new List<Vector3> { new Vector3(100, 0, 0) };

        field.GetConvergence(new Vector3(0, 0, 0), players1, 0f, out float x1, out float _);

        // After interval: should recompute with new player position
        var players2 = new List<Vector3> { new Vector3(-100, 0, 0) };
        field.GetConvergence(new Vector3(0, 0, 0), players2, 10f, out float x2, out float _2);

        // First pointed +X, second should point -X
        Assert.That(x1, Is.GreaterThan(0f));
        Assert.That(x2, Is.LessThan(0f));
    }

    [Test]
    public void InvalidateCache_ForcesRecomputation()
    {
        var field = new ConvergenceField(updateIntervalSec: 1000f);
        var players1 = new List<Vector3> { new Vector3(100, 0, 0) };

        field.GetConvergence(new Vector3(0, 0, 0), players1, 0f, out float _, out float _2);

        field.InvalidateCache();

        var players2 = new List<Vector3> { new Vector3(0, 0, 100) };
        field.GetConvergence(new Vector3(0, 0, 0), players2, 0.1f, out float x, out float z);

        // After invalidation, should point toward new player (+Z direction)
        Assert.That(z, Is.GreaterThan(0.9f));
    }

    [Test]
    public void OutputIsNormalized()
    {
        var field = new ConvergenceField();
        var players = new List<Vector3>
        {
            new Vector3(100, 0, 50),
            new Vector3(-30, 0, 200),
        };

        field.ComputeConvergence(new Vector3(0, 0, 0), players, out float x, out float z);

        float mag = (float)Math.Sqrt(x * x + z * z);
        Assert.That(mag, Is.EqualTo(1f).Within(0.01f));
    }

    [Test]
    public void EmptyPlayerList_ReturnsZero()
    {
        var field = new ConvergenceField();
        var players = new List<Vector3>();

        field.ComputeConvergence(new Vector3(0, 0, 0), players, out float x, out float z);

        Assert.Multiple(() =>
        {
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(z, Is.EqualTo(0f));
        });
    }
}
