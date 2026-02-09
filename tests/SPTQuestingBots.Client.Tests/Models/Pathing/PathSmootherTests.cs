using System;
using NUnit.Framework;
using SPTQuestingBots.Models.Pathing;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.Models.Pathing;

[TestFixture]
public class PathSmootherTests
{
    private const float Epsilon = 0.01f;

    // --- ChaikinSmooth ---

    [Test]
    public void ChaikinSmooth_NullInput_ReturnsNull()
    {
        Assert.IsNull(PathSmoother.ChaikinSmooth(null, 2));
    }

    [Test]
    public void ChaikinSmooth_SingleCorner_ReturnsOriginal()
    {
        var corners = new[] { new Vector3(0, 0, 0) };
        var result = PathSmoother.ChaikinSmooth(corners, 2);
        Assert.AreEqual(1, result.Length);
    }

    [Test]
    public void ChaikinSmooth_TwoCorners_ReturnsOriginal()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        var result = PathSmoother.ChaikinSmooth(corners, 2);
        Assert.AreEqual(2, result.Length);
    }

    [Test]
    public void ChaikinSmooth_ZeroIterations_ReturnsOriginal()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
        var result = PathSmoother.ChaikinSmooth(corners, 0);
        Assert.AreEqual(3, result.Length);
        Assert.AreEqual(corners[1].x, result[1].x, Epsilon);
    }

    [Test]
    public void ChaikinSmooth_PreservesStartAndEnd()
    {
        var corners = new[] { new Vector3(1, 2, 3), new Vector3(10, 0, 0), new Vector3(20, 5, 7) };
        var result = PathSmoother.ChaikinSmooth(corners, 2);
        Assert.AreEqual(1f, result[0].x, Epsilon);
        Assert.AreEqual(2f, result[0].y, Epsilon);
        Assert.AreEqual(3f, result[0].z, Epsilon);
        Assert.AreEqual(20f, result[result.Length - 1].x, Epsilon);
        Assert.AreEqual(5f, result[result.Length - 1].y, Epsilon);
        Assert.AreEqual(7f, result[result.Length - 1].z, Epsilon);
    }

    [Test]
    public void ChaikinSmooth_StraightPath_StaysStraight()
    {
        // All collinear points should remain roughly collinear
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(10, 0, 0), new Vector3(15, 0, 0) };
        var result = PathSmoother.ChaikinSmooth(corners, 2);

        // All Z values should be 0 (no lateral deviation)
        for (int i = 0; i < result.Length; i++)
        {
            Assert.AreEqual(0f, result[i].z, Epsilon, $"Point {i} has non-zero Z");
        }
    }

    [Test]
    public void ChaikinSmooth_RightAngle_ProducesMorePoints()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
        var result = PathSmoother.ChaikinSmooth(corners, 1);

        // 1 iteration on 3 points: start + R0 + Q1 + end = 4 points
        Assert.AreEqual(4, result.Length);
    }

    [Test]
    public void ChaikinSmooth_RightAngle_CutsCorner()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
        var result = PathSmoother.ChaikinSmooth(corners, 1);

        // The sharp corner at (10,0,0) should be replaced by two points
        // that cut the corner — neither should be exactly at (10,0,0)
        bool hasExactCorner = false;
        for (int i = 1; i < result.Length - 1; i++)
        {
            if (Math.Abs(result[i].x - 10f) < Epsilon && Math.Abs(result[i].z) < Epsilon)
                hasExactCorner = true;
        }
        Assert.IsFalse(hasExactCorner, "Corner should be cut, not preserved exactly");
    }

    [Test]
    public void ChaikinSmooth_MultipleIterations_ProducesMorePoints()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
        var result1 = PathSmoother.ChaikinSmooth(corners, 1);
        var result2 = PathSmoother.ChaikinSmooth(corners, 2);
        var result3 = PathSmoother.ChaikinSmooth(corners, 3);

        Assert.Greater(result2.Length, result1.Length);
        Assert.Greater(result3.Length, result2.Length);
    }

    [Test]
    public void ChaikinSmooth_PreservesYValues()
    {
        // Path with height variation — Y should be interpolated
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 5, 0), new Vector3(20, 10, 0) };
        var result = PathSmoother.ChaikinSmooth(corners, 1);

        // All Y values should be between 0 and 10
        for (int i = 0; i < result.Length; i++)
        {
            Assert.GreaterOrEqual(result[i].y, -Epsilon, $"Point {i} Y below range");
            Assert.LessOrEqual(result[i].y, 10f + Epsilon, $"Point {i} Y above range");
        }
    }

    [Test]
    public void ChaikinSmooth_NegativeIterations_ReturnsOriginal()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
        var result = PathSmoother.ChaikinSmooth(corners, -1);
        Assert.AreEqual(3, result.Length);
    }

    // --- InsertIntermediatePoints ---

    [Test]
    public void InsertIntermediatePoints_NullInput_ReturnsNull()
    {
        Assert.IsNull(PathSmoother.InsertIntermediatePoints(null, 5f));
    }

    [Test]
    public void InsertIntermediatePoints_SingleCorner_ReturnsOriginal()
    {
        var corners = new[] { new Vector3(0, 0, 0) };
        var result = PathSmoother.InsertIntermediatePoints(corners, 5f);
        Assert.AreEqual(1, result.Length);
    }

    [Test]
    public void InsertIntermediatePoints_ShortSegments_Unchanged()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(2, 0, 0) };
        var result = PathSmoother.InsertIntermediatePoints(corners, 5f);
        Assert.AreEqual(2, result.Length);
    }

    [Test]
    public void InsertIntermediatePoints_LongSegment_AddsMidpoints()
    {
        // 10m segment, min 3m → ceil(10/3) = 4 subdivisions → 3 midpoints
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        var result = PathSmoother.InsertIntermediatePoints(corners, 3f);
        Assert.AreEqual(5, result.Length); // start + 3 midpoints + end
    }

    [Test]
    public void InsertIntermediatePoints_PreservesStartAndEnd()
    {
        var corners = new[] { new Vector3(1, 2, 3), new Vector3(20, 5, 7) };
        var result = PathSmoother.InsertIntermediatePoints(corners, 3f);
        Assert.AreEqual(1f, result[0].x, Epsilon);
        Assert.AreEqual(20f, result[result.Length - 1].x, Epsilon);
    }

    [Test]
    public void InsertIntermediatePoints_MidpointsAreEvenly_Spaced()
    {
        // 12m segment, min 4m → 3 subdivisions → 2 midpoints at 4m and 8m
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(12, 0, 0) };
        var result = PathSmoother.InsertIntermediatePoints(corners, 4f);
        Assert.AreEqual(4, result.Length); // 0, 4, 8, 12
        Assert.AreEqual(4f, result[1].x, Epsilon);
        Assert.AreEqual(8f, result[2].x, Epsilon);
    }

    [Test]
    public void InsertIntermediatePoints_ZeroMinLength_ReturnsOriginal()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        var result = PathSmoother.InsertIntermediatePoints(corners, 0f);
        Assert.AreEqual(2, result.Length);
    }

    [Test]
    public void InsertIntermediatePoints_NegativeMinLength_ReturnsOriginal()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        var result = PathSmoother.InsertIntermediatePoints(corners, -1f);
        Assert.AreEqual(2, result.Length);
    }

    [Test]
    public void InsertIntermediatePoints_MultipleMixedSegments()
    {
        // Short (2m) + long (10m) segments
        var corners = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(2, 0, 0), // 2m, no split
            new Vector3(12, 0, 0), // 10m, split
        };
        var result = PathSmoother.InsertIntermediatePoints(corners, 5f);
        // Short stays: 0, 2
        // Long splits: 2 → 12 = 10m / 5m = ceil(2) = 2 subdivisions → 1 midpoint at 7
        Assert.AreEqual(4, result.Length); // 0, 2, 7, 12
    }

    // --- Smooth (full pipeline) ---

    [Test]
    public void Smooth_AppliesBothSteps()
    {
        var corners = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(15, 0, 0), // 15m → will get intermediate points
            new Vector3(15, 0, 15), // then chaikin smoothing
        };
        var config = CustomMoverConfig.CreateDefault();
        var result = PathSmoother.Smooth(corners, config);

        // Should have more points than original (both steps add points)
        Assert.Greater(result.Length, corners.Length);
        // Start and end preserved
        Assert.AreEqual(0f, result[0].x, Epsilon);
        Assert.AreEqual(15f, result[result.Length - 1].z, Epsilon);
    }
}
