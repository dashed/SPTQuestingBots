using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.ZoneMovement.Core;
using SPTQuestingBots.ZoneMovement.Fields;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class ConvergenceMapConfigTests
{
    // --- ConvergenceMapConfig defaults ---

    [Test]
    public void GetDefaults_ContainsAllExpectedMaps()
    {
        var defaults = ConvergenceMapConfig.GetDefaults();

        Assert.Multiple(() =>
        {
            Assert.That(defaults, Contains.Key("factory4_day"));
            Assert.That(defaults, Contains.Key("factory4_night"));
            Assert.That(defaults, Contains.Key("bigmap"));
            Assert.That(defaults, Contains.Key("woods"));
            Assert.That(defaults, Contains.Key("shoreline"));
            Assert.That(defaults, Contains.Key("lighthouse"));
            Assert.That(defaults, Contains.Key("rezervbase"));
            Assert.That(defaults, Contains.Key("interchange"));
            Assert.That(defaults, Contains.Key("laboratory"));
            Assert.That(defaults, Contains.Key("tarkovstreets"));
            Assert.That(defaults, Contains.Key("sandbox"));
            Assert.That(defaults, Contains.Key("sandbox_high"));
        });
    }

    [Test]
    public void FactoryMaps_AreDisabled()
    {
        var defaults = ConvergenceMapConfig.GetDefaults();

        Assert.Multiple(() =>
        {
            Assert.That(defaults["factory4_day"].Enabled, Is.False);
            Assert.That(defaults["factory4_night"].Enabled, Is.False);
        });
    }

    [Test]
    public void Customs_HasExpectedValues()
    {
        var defaults = ConvergenceMapConfig.GetDefaults();
        var customs = defaults["bigmap"];

        Assert.Multiple(() =>
        {
            Assert.That(customs.Radius, Is.EqualTo(250f));
            Assert.That(customs.Force, Is.EqualTo(1.0f));
            Assert.That(customs.Enabled, Is.True);
        });
    }

    [Test]
    public void Laboratory_HasSmallRadiusAndHighForce()
    {
        var defaults = ConvergenceMapConfig.GetDefaults();
        var lab = defaults["laboratory"];

        Assert.Multiple(() =>
        {
            Assert.That(lab.Radius, Is.EqualTo(150f));
            Assert.That(lab.Force, Is.EqualTo(1.2f));
            Assert.That(lab.Enabled, Is.True);
        });
    }

    [Test]
    public void Woods_HasLargeRadius()
    {
        var defaults = ConvergenceMapConfig.GetDefaults();
        var woods = defaults["woods"];

        Assert.That(woods.Radius, Is.EqualTo(400f));
        Assert.That(woods.Force, Is.EqualTo(0.8f));
    }

    [Test]
    public void GetForMap_KnownMap_ReturnsDefault()
    {
        var config = ConvergenceMapConfig.GetForMap("bigmap", null);

        Assert.That(config.Radius, Is.EqualTo(250f));
        Assert.That(config.Force, Is.EqualTo(1.0f));
    }

    [Test]
    public void GetForMap_CaseInsensitive()
    {
        var config = ConvergenceMapConfig.GetForMap("BigMap", null);

        Assert.That(config.Radius, Is.EqualTo(250f));
    }

    [Test]
    public void GetForMap_UnknownMap_ReturnsFallbackDefault()
    {
        var config = ConvergenceMapConfig.GetForMap("unknownmap123", null);

        Assert.That(config.Radius, Is.EqualTo(ConvergenceMapConfig.Default.Radius));
        Assert.That(config.Force, Is.EqualTo(ConvergenceMapConfig.Default.Force));
        Assert.That(config.Enabled, Is.EqualTo(ConvergenceMapConfig.Default.Enabled));
    }

    [Test]
    public void GetForMap_WithOverrides_UsesOverride()
    {
        var overrides = new Dictionary<string, ConvergenceMapConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["bigmap"] = new ConvergenceMapConfig(999f, 2.5f, true),
        };

        var config = ConvergenceMapConfig.GetForMap("bigmap", overrides);

        Assert.Multiple(() =>
        {
            Assert.That(config.Radius, Is.EqualTo(999f));
            Assert.That(config.Force, Is.EqualTo(2.5f));
        });
    }

    [Test]
    public void GetForMap_OverrideDoesNotAffectOtherMaps()
    {
        var overrides = new Dictionary<string, ConvergenceMapConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["bigmap"] = new ConvergenceMapConfig(999f, 2.5f, true),
        };

        var config = ConvergenceMapConfig.GetForMap("woods", overrides);

        Assert.That(config.Radius, Is.EqualTo(400f));
    }

    // --- ConvergenceTimeWeight ---

    [Test]
    public void TimeWeight_AtStart_ReturnsEarlyWeight()
    {
        float result = ConvergenceTimeWeight.ComputeMultiplier(0f);

        Assert.That(result, Is.EqualTo(1.3f).Within(0.01f));
    }

    [Test]
    public void TimeWeight_AtMidPoint_ReturnsOne()
    {
        float result = ConvergenceTimeWeight.ComputeMultiplier(0.5f);

        Assert.That(result, Is.EqualTo(1.0f).Within(0.01f));
    }

    [Test]
    public void TimeWeight_AtEnd_ReturnsLateWeight()
    {
        float result = ConvergenceTimeWeight.ComputeMultiplier(1.0f);

        Assert.That(result, Is.EqualTo(0.7f).Within(0.01f));
    }

    [Test]
    public void TimeWeight_AtEarlyBoundary_ReturnsOne()
    {
        float result = ConvergenceTimeWeight.ComputeMultiplier(0.2f);

        Assert.That(result, Is.EqualTo(1.0f).Within(0.01f));
    }

    [Test]
    public void TimeWeight_AtLateBoundary_ReturnsOne()
    {
        float result = ConvergenceTimeWeight.ComputeMultiplier(0.7f);

        Assert.That(result, Is.EqualTo(1.0f).Within(0.01f));
    }

    [Test]
    public void TimeWeight_SmoothTransition_EarlyPhase()
    {
        // At t=0.1 (midpoint of early phase), should be between 1.3 and 1.0
        float result = ConvergenceTimeWeight.ComputeMultiplier(0.1f);

        Assert.That(result, Is.InRange(1.0f, 1.3f));
        Assert.That(result, Is.EqualTo(1.15f).Within(0.01f));
    }

    [Test]
    public void TimeWeight_SmoothTransition_LatePhase()
    {
        // At t=0.85 (midpoint of late phase), should be between 1.0 and 0.7
        float result = ConvergenceTimeWeight.ComputeMultiplier(0.85f);

        Assert.That(result, Is.InRange(0.7f, 1.0f));
        Assert.That(result, Is.EqualTo(0.85f).Within(0.01f));
    }

    [Test]
    public void TimeWeight_NegativeInput_ClampsToZero()
    {
        float result = ConvergenceTimeWeight.ComputeMultiplier(-0.5f);

        Assert.That(result, Is.EqualTo(1.3f).Within(0.01f));
    }

    [Test]
    public void TimeWeight_BeyondOne_ClampsToOne()
    {
        float result = ConvergenceTimeWeight.ComputeMultiplier(1.5f);

        Assert.That(result, Is.EqualTo(0.7f).Within(0.01f));
    }

    // --- ConvergenceField with radius and force ---

    [Test]
    public void ConvergenceField_RadiusLimitsAttraction()
    {
        // Player at 500m, but radius is 100m — should produce zero
        var field = new ConvergenceField(radius: 100f, force: 1.0f);
        var players = new List<Vector3> { new Vector3(500, 0, 0) };

        field.ComputeConvergence(new Vector3(0, 0, 0), players, out float x, out float z);

        Assert.Multiple(() =>
        {
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(z, Is.EqualTo(0f));
        });
    }

    [Test]
    public void ConvergenceField_PlayerWithinRadius_Attracts()
    {
        var field = new ConvergenceField(radius: 200f, force: 1.0f);
        var players = new List<Vector3> { new Vector3(100, 0, 0) };

        field.ComputeConvergence(new Vector3(0, 0, 0), players, out float x, out float z);

        Assert.That(x, Is.GreaterThan(0.9f));
    }

    [Test]
    public void ConvergenceField_ForceMultiplier_ScalesOutput()
    {
        // Two fields: one with force=1.0 and one with force=2.0
        // Both should produce unit direction (normalized), but the field with higher force
        // should produce the same direction (it's normalized). The force affects the
        // magnitude BEFORE normalization, which matters when multiple players exist.
        var field1 = new ConvergenceField(force: 1.0f);
        var field2 = new ConvergenceField(force: 2.0f);
        var players = new List<Vector3> { new Vector3(100, 0, 0) };

        field1.ComputeConvergence(new Vector3(0, 0, 0), players, out float x1, out float _);
        field2.ComputeConvergence(new Vector3(0, 0, 0), players, out float x2, out float _2);

        // Single player: both normalized, same direction
        Assert.That(x1, Is.EqualTo(x2).Within(0.01f));
    }

    [Test]
    public void ConvergenceField_ZeroForce_ReturnsZero()
    {
        var field = new ConvergenceField(force: 0f);
        var players = new List<Vector3> { new Vector3(100, 0, 0) };

        field.ComputeConvergence(new Vector3(0, 0, 0), players, out float x, out float z);

        Assert.Multiple(() =>
        {
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(z, Is.EqualTo(0f));
        });
    }

    // --- ConvergenceField with combat pull points ---

    [Test]
    public void ConvergenceField_CombatPull_PullsTowardEvent()
    {
        var field = new ConvergenceField();
        var combat = new CombatPullPoint[]
        {
            new CombatPullPoint
            {
                X = 100,
                Z = 0,
                Strength = 1.0f,
            },
        };

        field.ComputeConvergence(new Vector3(0, 0, 0), null, combat, 1, out float x, out float z);

        Assert.That(x, Is.GreaterThan(0.9f));
        Assert.That(Math.Abs(z), Is.LessThan(0.1f));
    }

    [Test]
    public void ConvergenceField_CombatPull_PartialDecay()
    {
        var field = new ConvergenceField();
        // Half-decayed strength (still significant)
        var combat = new CombatPullPoint[]
        {
            new CombatPullPoint
            {
                X = 100,
                Z = 0,
                Strength = 0.5f,
            },
        };

        field.ComputeConvergence(new Vector3(0, 0, 0), null, combat, 1, out float x, out float z);

        // Still pulls in the right direction (normalized)
        Assert.That(x, Is.GreaterThan(0.9f));
    }

    [Test]
    public void ConvergenceField_CombatAndPlayer_CombineDirections()
    {
        var field = new ConvergenceField();
        var players = new List<Vector3> { new Vector3(100, 0, 0) }; // East
        var combat = new CombatPullPoint[]
        {
            new CombatPullPoint
            {
                X = 0,
                Z = 100,
                Strength = 1.0f,
            }, // North
        };

        field.ComputeConvergence(new Vector3(0, 0, 0), players, combat, 1, out float x, out float z);

        // Should point northeast (both positive)
        Assert.Multiple(() =>
        {
            Assert.That(x, Is.GreaterThan(0f));
            Assert.That(z, Is.GreaterThan(0f));
        });
    }

    [Test]
    public void ConvergenceField_CombatPull_RadiusLimits()
    {
        var field = new ConvergenceField(radius: 50f);
        var combat = new CombatPullPoint[]
        {
            new CombatPullPoint
            {
                X = 100,
                Z = 0,
                Strength = 1.0f,
            }, // Beyond radius
        };

        field.ComputeConvergence(new Vector3(0, 0, 0), null, combat, 1, out float x, out float z);

        Assert.Multiple(() =>
        {
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(z, Is.EqualTo(0f));
        });
    }

    [Test]
    public void ConvergenceField_NullCombatPull_WorksLikeNoCombat()
    {
        var field = new ConvergenceField();
        var players = new List<Vector3> { new Vector3(100, 0, 0) };

        field.ComputeConvergence(new Vector3(0, 0, 0), players, null, 0, out float x1, out float z1);
        field.ComputeConvergence(new Vector3(0, 0, 0), players, out float x2, out float z2);

        Assert.Multiple(() =>
        {
            Assert.That(x1, Is.EqualTo(x2).Within(0.001f));
            Assert.That(z1, Is.EqualTo(z2).Within(0.001f));
        });
    }

    // --- FieldComposer with time multiplier ---

    [Test]
    public void FieldComposer_TimeMultiplier_ScalesConvergence()
    {
        // Only convergence, no other fields
        var composer = new FieldComposer(convergenceWeight: 1.0f, advectionWeight: 0f, momentumWeight: 0f, noiseWeight: 0f);

        // With high time multiplier — should still point east (normalized)
        composer.GetCompositeDirection(0, 0, 1, 0, 0, 0, 0, 1.3f, out float x1, out float z1);
        Assert.That(x1, Is.GreaterThan(0.9f));

        // With zero time multiplier — convergence zeroed out
        composer.GetCompositeDirection(0, 0, 1, 0, 0, 0, 0, 0f, out float x2, out float z2);
        Assert.That(x2, Is.EqualTo(0f));
        Assert.That(z2, Is.EqualTo(0f));
    }

    [Test]
    public void FieldComposer_TimeMultiplier_AffectsBalance()
    {
        // Equal convergence and advection, pointing in different directions
        var composer = new FieldComposer(convergenceWeight: 1.0f, advectionWeight: 1.0f, momentumWeight: 0f, noiseWeight: 0f);

        // Convergence east (1,0), Advection north (0,1)
        // With time multiplier 2.0: convergence is now 2x stronger
        composer.GetCompositeDirection(0, 1, 1, 0, 0, 0, 0, 2.0f, out float x, out float z);

        // Should lean more toward east (convergence) than north (advection)
        Assert.That(x, Is.GreaterThan(z));
    }

    // --- CombatEventRegistry.GatherCombatPull ---

    [SetUp]
    public void SetUp()
    {
        CombatEventRegistry.Initialize(128);
    }

    [Test]
    public void GatherCombatPull_NoEvents_ReturnsZero()
    {
        var buffer = new CombatPullPoint[128];
        int count = CombatEventRegistry.GatherCombatPull(buffer, 100f, 30f, 1.0f);

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void GatherCombatPull_RecentEvent_ReturnsPullPoint()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 200f, 10f, 100f, CombatEventType.Gunshot, false);

        var buffer = new CombatPullPoint[128];
        int count = CombatEventRegistry.GatherCombatPull(buffer, 15f, 30f, 1.0f);

        Assert.That(count, Is.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(buffer[0].X, Is.EqualTo(100f));
            Assert.That(buffer[0].Z, Is.EqualTo(200f));
            Assert.That(buffer[0].Strength, Is.GreaterThan(0f));
        });
    }

    [Test]
    public void GatherCombatPull_LinearDecay()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 200f, 0f, 100f, CombatEventType.Gunshot, false);

        var buffer = new CombatPullPoint[128];

        // At time 0: age=0, decay=1.0
        int count1 = CombatEventRegistry.GatherCombatPull(buffer, 0f, 30f, 1.0f);
        float strength0 = buffer[0].Strength;

        // At time 15: age=15, decay=0.5
        CombatEventRegistry.Initialize(128); // Reset
        CombatEventRegistry.RecordEvent(100f, 0f, 200f, 0f, 100f, CombatEventType.Gunshot, false);
        int count2 = CombatEventRegistry.GatherCombatPull(buffer, 15f, 30f, 1.0f);
        float strength15 = buffer[0].Strength;

        Assert.That(strength0, Is.EqualTo(1.0f).Within(0.01f)); // power/100 * decay(1.0) * force(1.0)
        Assert.That(strength15, Is.EqualTo(0.5f).Within(0.01f)); // power/100 * decay(0.5) * force(1.0)
    }

    [Test]
    public void GatherCombatPull_ExpiredEvent_Excluded()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 200f, 0f, 100f, CombatEventType.Gunshot, false);

        var buffer = new CombatPullPoint[128];
        // At time 60 with maxAge 30: event age is 60 > 30 — should be excluded
        int count = CombatEventRegistry.GatherCombatPull(buffer, 60f, 30f, 1.0f);

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void GatherCombatPull_ExplosionHasHigherPower()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 200f, 0f, 150f, CombatEventType.Explosion, false);

        var buffer = new CombatPullPoint[128];
        int count = CombatEventRegistry.GatherCombatPull(buffer, 0f, 30f, 1.0f);

        Assert.That(count, Is.EqualTo(1));
        Assert.That(buffer[0].Strength, Is.EqualTo(1.5f).Within(0.01f)); // 150/100 * 1.0 * 1.0
    }

    [Test]
    public void GatherCombatPull_ForceMultiplier_Applies()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 200f, 0f, 100f, CombatEventType.Gunshot, false);

        var buffer = new CombatPullPoint[128];
        int count = CombatEventRegistry.GatherCombatPull(buffer, 0f, 30f, 0.5f);

        Assert.That(buffer[0].Strength, Is.EqualTo(0.5f).Within(0.01f)); // 100/100 * 1.0 * 0.5
    }

    [Test]
    public void GatherCombatPull_InactiveEvent_Excluded()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 200f, 0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.CleanupExpired(100f, 30f); // Mark as expired

        var buffer = new CombatPullPoint[128];
        int count = CombatEventRegistry.GatherCombatPull(buffer, 100f, 200f, 1.0f);

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void GatherCombatPull_NullBuffer_ReturnsZero()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 200f, 0f, 100f, CombatEventType.Gunshot, false);

        int count = CombatEventRegistry.GatherCombatPull(null, 0f, 30f, 1.0f);

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void GatherCombatPull_MultipleEvents_AllReturned()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 200f, 0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(300f, 0f, 400f, 5f, 100f, CombatEventType.Gunshot, false);

        var buffer = new CombatPullPoint[128];
        int count = CombatEventRegistry.GatherCombatPull(buffer, 10f, 30f, 1.0f);

        Assert.That(count, Is.EqualTo(2));
    }
}
