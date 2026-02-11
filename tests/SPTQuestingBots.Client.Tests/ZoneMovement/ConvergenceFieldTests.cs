using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Core;
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
        var players = new List<Vector3> { new Vector3(100, 0, 50), new Vector3(-30, 0, 200) };

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

    // --- 1/dist falloff verification ---

    [Test]
    public void PositionFalloff_Uses1OverDist_NearSourceDominatesFar()
    {
        // With 1/dist falloff, a source at dist=10 has 20x the weight of a source at dist=200.
        // With 1/sqrt(dist) it would only be ~4.5x. This test distinguishes the two.
        //
        // Near source at (+10, 0, 0): w = 1/10 = 0.1, contribution = (0.1, 0)
        // Far source at (0, 0, +200): w = 1/200 = 0.005, contribution = (0, 0.005)
        // Net = (0.1, 0.005), normalized ≈ (0.9988, 0.050)
        //
        // With 1/sqrt: Near w=0.316, Far w=0.071 → net=(0.316,0.071) → (0.976, 0.218)
        // Assert z < 0.10 passes only with 1/dist.
        var field = new ConvergenceField();
        var sources = new List<Vector3>
        {
            new Vector3(10, 0, 0), // Near: +X
            new Vector3(0, 0, 200), // Far: +Z
        };

        field.ComputeConvergence(new Vector3(0, 0, 0), sources, out float x, out float z);

        Assert.Multiple(() =>
        {
            Assert.That(x, Is.GreaterThan(0.98f), "Near source should dominate with 1/dist falloff");
            Assert.That(z, Is.LessThan(0.10f), "Far source should be negligible with 1/dist (would be ~0.22 with 1/sqrt)");
        });
    }

    [Test]
    public void PositionFalloff_DoubleDistance_QuartersWeight()
    {
        // With 1/dist, doubling the distance halves the weight.
        // Place sources at dist=50 and dist=100 in opposing directions (+X and -X).
        // Source A at +50: w = 1/50 = 0.02 → pulls +X
        // Source B at -100: w = 1/100 = 0.01 → pulls -X
        // Net = 0.02 - 0.01 = 0.01 → points +X (toward closer source)
        var field = new ConvergenceField();
        var sources = new List<Vector3>
        {
            new Vector3(50, 0, 0), // Closer, +X
            new Vector3(-100, 0, 0), // Farther, -X
        };

        field.ComputeConvergence(new Vector3(0, 0, 0), sources, out float x, out float z);

        // Net should point toward the closer source
        Assert.That(x, Is.GreaterThan(0.9f), "Should point toward closer source with 1/dist falloff");
    }

    [Test]
    public void PositionFalloff_VeryClose_SkipsZeroDistance()
    {
        // Source exactly at query position (dist < 0.1) should be skipped
        var field = new ConvergenceField();
        var sources = new List<Vector3>
        {
            new Vector3(0.001f, 0, 0), // Nearly coincident
            new Vector3(100, 0, 0), // Normal distance
        };

        field.ComputeConvergence(new Vector3(0, 0, 0), sources, out float x, out float z);

        // Should still produce valid output from the second source
        Assert.That(x, Is.GreaterThan(0.9f));
    }

    // --- Combat event 1/sqrt(dist) falloff preserved ---

    [Test]
    public void CombatFalloff_PreservesSqrtDecay_FarEventHasMoreInfluence()
    {
        // Combat events use 1/sqrt(dist) — shallower decay than position's 1/dist.
        // A combat event at dist=200 should have relatively MORE influence than
        // a position source at the same distance.
        //
        // Position at (+10, 0, 0): w_pos = 1/10 = 0.1
        // Combat at (0, 0, +200): w_combat = 1/sqrt(200) = 0.0707
        //
        // If combat also used 1/dist: w_combat = 1/200 = 0.005
        // With 1/sqrt: z component is ~0.0707/sqrt(0.1²+0.0707²) ≈ 0.577
        // With 1/dist: z component would be ~0.005/sqrt(0.1²+0.005²) ≈ 0.050
        //
        // Assert z > 0.40 to verify combat keeps the shallower 1/sqrt falloff.
        var field = new ConvergenceField();
        var positions = new List<Vector3> { new Vector3(10, 0, 0) };
        var combat = new CombatPullPoint[]
        {
            new CombatPullPoint
            {
                X = 0,
                Z = 200,
                Strength = 1.0f,
            },
        };

        field.ComputeConvergence(new Vector3(0, 0, 0), positions, combat, 1, out float x, out float z);

        Assert.Multiple(() =>
        {
            Assert.That(x, Is.GreaterThan(0f), "Should have positive X from position source");
            Assert.That(z, Is.GreaterThan(0.40f), "Combat event at dist=200 should still have strong influence with 1/sqrt falloff");
        });
    }

    [Test]
    public void CombatFalloff_NearCombatStrongerThanFar()
    {
        // Even with 1/sqrt, closer events should still outweigh farther ones
        var field = new ConvergenceField();
        var combat = new CombatPullPoint[]
        {
            new CombatPullPoint
            {
                X = 20,
                Z = 0,
                Strength = 1.0f,
            }, // Near, +X
            new CombatPullPoint
            {
                X = -200,
                Z = 0,
                Strength = 1.0f,
            }, // Far, -X
        };

        field.ComputeConvergence(new Vector3(0, 0, 0), null, combat, 2, out float x, out float z);

        Assert.That(x, Is.GreaterThan(0f), "Closer combat event should dominate direction");
    }

    // --- Asymmetric falloff: position vs combat at same distance ---

    [Test]
    public void FalloffAsymmetry_CombatReachesFartherThanPosition()
    {
        // At dist=200, position falloff (1/dist) gives weight 0.005
        // Combat falloff (1/sqrt) gives weight 0.0707 (14x stronger).
        // At dist=10, position gives 0.1, combat gives 0.316 (3.16x stronger).
        // The ratio gap GROWS with distance — combat reaches farther.
        //
        // Test: position-only vs combat-only from far source, compare Z influence
        // when combined with a near +X source.
        var field = new ConvergenceField();

        // Position-only: near +X at 10m, far +Z at 200m
        var posOnly = new List<Vector3> { new Vector3(10, 0, 0), new Vector3(0, 0, 200) };
        field.ComputeConvergence(new Vector3(0, 0, 0), posOnly, out float _, out float zPosOnly);

        // Combat from far +Z at 200m, with near position +X at 10m
        var nearPos = new List<Vector3> { new Vector3(10, 0, 0) };
        var farCombat = new CombatPullPoint[]
        {
            new CombatPullPoint
            {
                X = 0,
                Z = 200,
                Strength = 1.0f,
            },
        };
        field.ComputeConvergence(new Vector3(0, 0, 0), nearPos, farCombat, 1, out float _, out float zWithCombat);

        // Combat's Z influence from 200m away should be much stronger than position's
        Assert.That(zWithCombat, Is.GreaterThan(zPosOnly * 3f), "Combat 1/sqrt reaches farther than position 1/dist at same distance");
    }
}
