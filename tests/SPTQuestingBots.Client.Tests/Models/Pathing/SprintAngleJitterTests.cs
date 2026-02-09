using NUnit.Framework;
using SPTQuestingBots.Models.Pathing;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.Models.Pathing;

[TestFixture]
public class SprintAngleJitterTests
{
    // --- ComputeAngleJitter ---

    [Test]
    public void ComputeAngleJitter_NullCorners_ReturnsZero()
    {
        Assert.AreEqual(0f, SprintAngleJitter.ComputeAngleJitter(null, 0, 100f));
    }

    [Test]
    public void ComputeAngleJitter_SingleCorner_ReturnsZero()
    {
        var corners = new[] { new Vector3(0, 0, 0) };
        Assert.AreEqual(0f, SprintAngleJitter.ComputeAngleJitter(corners, 0, 100f));
    }

    [Test]
    public void ComputeAngleJitter_TwoCorners_ReturnsZero()
    {
        // Need at least 3 corners (2 segments) to measure angle change
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        Assert.AreEqual(0f, SprintAngleJitter.ComputeAngleJitter(corners, 0, 100f));
    }

    [Test]
    public void ComputeAngleJitter_StraightPath_ReturnsZero()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(10, 0, 0), new Vector3(15, 0, 0) };
        float jitter = SprintAngleJitter.ComputeAngleJitter(corners, 0, 100f);
        Assert.AreEqual(0f, jitter, 0.01f);
    }

    [Test]
    public void ComputeAngleJitter_RightAngleTurn_Returns90()
    {
        // Path goes east then turns north → 90° turn
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
        float jitter = SprintAngleJitter.ComputeAngleJitter(corners, 0, 100f);
        Assert.AreEqual(90f, jitter, 0.1f);
    }

    [Test]
    public void ComputeAngleJitter_UTurn_Returns180()
    {
        // Path goes east then turns back west → 180° turn
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(0, 0, 0) };
        float jitter = SprintAngleJitter.ComputeAngleJitter(corners, 0, 100f);
        Assert.AreEqual(180f, jitter, 0.1f);
    }

    [Test]
    public void ComputeAngleJitter_45DegreeTurn_Returns45()
    {
        // Path goes east then turns northeast → 45° turn
        var corners = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(17.07f, 0, 7.07f), // roughly 45 degrees
        };
        float jitter = SprintAngleJitter.ComputeAngleJitter(corners, 0, 100f);
        Assert.AreEqual(45f, jitter, 1f);
    }

    [Test]
    public void ComputeAngleJitter_MultipleTurns_ReturnsMax()
    {
        // Two turns: first gentle (~27°), then sharp (90°) → max should be 90°
        var corners = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0), // east
            new Vector3(15, 0, 2.5f), // gentle turn (~27°)
            new Vector3(15, 0, 12.5f), // 90° turn (was going NE-ish, now going N)
        };
        float jitter = SprintAngleJitter.ComputeAngleJitter(corners, 0, 100f);
        // Should capture the 90° turn, which is greater than the ~27° turn
        Assert.Greater(jitter, 60f);
    }

    [Test]
    public void ComputeAngleJitter_StartIndexMid_SkipsEarlierCorners()
    {
        // Put a sharp turn early and a straight section later
        var corners = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(10, 0, 10), // 90° turn at index 1
            new Vector3(20, 0, 10),
            new Vector3(30, 0, 10), // straight at index 3
        };
        // Starting at index 2 should skip the 90° turn
        float jitter = SprintAngleJitter.ComputeAngleJitter(corners, 2, 100f);
        // Segments: (10,0,10)→(20,0,10) east, (20,0,10)→(30,0,10) east → 0° turn
        Assert.AreEqual(0f, jitter, 0.1f);
    }

    [Test]
    public void ComputeAngleJitter_NegativeStartIndex_ReturnsZero()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(10, 0, 5) };
        Assert.AreEqual(0f, SprintAngleJitter.ComputeAngleJitter(corners, -1, 100f));
    }

    [Test]
    public void ComputeAngleJitter_StartIndexBeyondArray_ReturnsZero()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(10, 0, 5) };
        Assert.AreEqual(0f, SprintAngleJitter.ComputeAngleJitter(corners, 10, 100f));
    }

    [Test]
    public void ComputeAngleJitter_LookaheadLimits_StopsEarly()
    {
        // Sharp turn far ahead, but lookahead distance is short
        var corners = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(100, 0, 0), // 100m segment
            new Vector3(100, 0, 100), // 90° turn at 100m
        };
        // Lookahead of 10m should not reach the turn
        float jitter = SprintAngleJitter.ComputeAngleJitter(corners, 0, 10f);
        Assert.AreEqual(0f, jitter, 0.01f);
    }

    [Test]
    public void ComputeAngleJitter_LookaheadReachesTurn()
    {
        // Short segments within lookahead
        var corners = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(5, 0, 0),
            new Vector3(5, 0, 5), // 90° turn at 5m
        };
        float jitter = SprintAngleJitter.ComputeAngleJitter(corners, 0, 10f);
        Assert.AreEqual(90f, jitter, 0.1f);
    }

    [Test]
    public void ComputeAngleJitter_IgnoresYAxis()
    {
        // Path with height changes but straight in XZ
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 10, 0), new Vector3(10, 0, 0) };
        float jitter = SprintAngleJitter.ComputeAngleJitter(corners, 0, 100f);
        // In XZ plane this is a straight line → 0°
        Assert.AreEqual(0f, jitter, 0.1f);
    }

    [Test]
    public void ComputeAngleJitter_DegenerateSegment_Skipped()
    {
        // Two identical consecutive points → degenerate segment
        var corners = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(5, 0, 0),
            new Vector3(5, 0, 0), // degenerate
            new Vector3(10, 0, 0),
        };
        float jitter = SprintAngleJitter.ComputeAngleJitter(corners, 0, 100f);
        // The degenerate segment should be skipped, remaining is straight → 0°
        Assert.AreEqual(0f, jitter, 0.1f);
    }

    // --- CanSprint ---

    [Test]
    public void CanSprint_ZeroJitter_AlwaysTrue()
    {
        Assert.IsTrue(SprintAngleJitter.CanSprint(0f, SprintUrgency.Low));
        Assert.IsTrue(SprintAngleJitter.CanSprint(0f, SprintUrgency.Medium));
        Assert.IsTrue(SprintAngleJitter.CanSprint(0f, SprintUrgency.High));
    }

    [Test]
    public void CanSprint_BelowLowThreshold_AllowsAll()
    {
        Assert.IsTrue(SprintAngleJitter.CanSprint(15f, SprintUrgency.Low));
        Assert.IsTrue(SprintAngleJitter.CanSprint(15f, SprintUrgency.Medium));
        Assert.IsTrue(SprintAngleJitter.CanSprint(15f, SprintUrgency.High));
    }

    [Test]
    public void CanSprint_AboveLowBelowMedium_DependsOnUrgency()
    {
        float jitter = 25f; // above 20 (low), below 30 (medium)
        Assert.IsFalse(SprintAngleJitter.CanSprint(jitter, SprintUrgency.Low));
        Assert.IsTrue(SprintAngleJitter.CanSprint(jitter, SprintUrgency.Medium));
        Assert.IsTrue(SprintAngleJitter.CanSprint(jitter, SprintUrgency.High));
    }

    [Test]
    public void CanSprint_AboveMediumBelowHigh_OnlyHighAllows()
    {
        float jitter = 35f; // above 30 (medium), below 45 (high)
        Assert.IsFalse(SprintAngleJitter.CanSprint(jitter, SprintUrgency.Low));
        Assert.IsFalse(SprintAngleJitter.CanSprint(jitter, SprintUrgency.Medium));
        Assert.IsTrue(SprintAngleJitter.CanSprint(jitter, SprintUrgency.High));
    }

    [Test]
    public void CanSprint_AboveAllThresholds_NeverSprints()
    {
        float jitter = 90f; // above all
        Assert.IsFalse(SprintAngleJitter.CanSprint(jitter, SprintUrgency.Low));
        Assert.IsFalse(SprintAngleJitter.CanSprint(jitter, SprintUrgency.Medium));
        Assert.IsFalse(SprintAngleJitter.CanSprint(jitter, SprintUrgency.High));
    }

    [Test]
    public void CanSprint_ExactlyAtThreshold_Allows()
    {
        // At threshold boundary → should allow (<=)
        Assert.IsTrue(SprintAngleJitter.CanSprint(20f, SprintUrgency.Low));
        Assert.IsTrue(SprintAngleJitter.CanSprint(30f, SprintUrgency.Medium));
        Assert.IsTrue(SprintAngleJitter.CanSprint(45f, SprintUrgency.High));
    }

    [Test]
    public void CanSprint_CustomThresholds_Respected()
    {
        float jitter = 50f;
        // Custom thresholds: High=60, Medium=40, Low=10
        Assert.IsTrue(SprintAngleJitter.CanSprint(jitter, SprintUrgency.High, 60f, 40f, 10f));
        Assert.IsFalse(SprintAngleJitter.CanSprint(jitter, SprintUrgency.Medium, 60f, 40f, 10f));
        Assert.IsFalse(SprintAngleJitter.CanSprint(jitter, SprintUrgency.Low, 60f, 40f, 10f));
    }
}
