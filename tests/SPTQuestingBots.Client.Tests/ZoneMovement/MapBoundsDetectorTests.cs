using System;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Integration;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class MapBoundsDetectorTests
{
    [Test]
    public void DetectBounds_SinglePosition_ExpandsByPadding()
    {
        var positions = new[] { new Vector3(100, 5, 200) };

        var (min, max) = MapBoundsDetector.DetectBounds(positions, padding: 50f);

        Assert.That(min.x, Is.EqualTo(50f).Within(0.01f));
        Assert.That(max.x, Is.EqualTo(150f).Within(0.01f));
        Assert.That(min.z, Is.EqualTo(150f).Within(0.01f));
        Assert.That(max.z, Is.EqualTo(250f).Within(0.01f));
    }

    [Test]
    public void DetectBounds_MultiplePositions_UsesMinMax()
    {
        var positions = new[] { new Vector3(-100, 0, -200), new Vector3(300, 10, 400), new Vector3(50, 5, 100) };

        var (min, max) = MapBoundsDetector.DetectBounds(positions, padding: 10f);

        Assert.That(min.x, Is.EqualTo(-110f).Within(0.01f)); // -100 - 10
        Assert.That(max.x, Is.EqualTo(310f).Within(0.01f)); // 300 + 10
        Assert.That(min.z, Is.EqualTo(-210f).Within(0.01f)); // -200 - 10
        Assert.That(max.z, Is.EqualTo(410f).Within(0.01f)); // 400 + 10
    }

    [Test]
    public void DetectBounds_YBounds_AreExtreme()
    {
        var positions = new[] { new Vector3(0, 50, 0) };

        var (min, max) = MapBoundsDetector.DetectBounds(positions);

        Assert.That(min.y, Is.EqualTo(-10000f));
        Assert.That(max.y, Is.EqualTo(10000f));
    }

    [Test]
    public void DetectBounds_DefaultPadding_Is50()
    {
        var positions = new[] { new Vector3(0, 0, 0) };

        var (min, max) = MapBoundsDetector.DetectBounds(positions);

        Assert.That(min.x, Is.EqualTo(-50f).Within(0.01f));
        Assert.That(max.x, Is.EqualTo(50f).Within(0.01f));
    }

    [Test]
    public void DetectBounds_ZeroPadding_ExactBounds()
    {
        var positions = new[] { new Vector3(10, 0, 20), new Vector3(30, 0, 40) };

        var (min, max) = MapBoundsDetector.DetectBounds(positions, padding: 0f);

        Assert.That(min.x, Is.EqualTo(10f).Within(0.01f));
        Assert.That(max.x, Is.EqualTo(30f).Within(0.01f));
        Assert.That(min.z, Is.EqualTo(20f).Within(0.01f));
        Assert.That(max.z, Is.EqualTo(40f).Within(0.01f));
    }

    [Test]
    public void DetectBounds_NullPositions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MapBoundsDetector.DetectBounds(null));
    }

    [Test]
    public void DetectBounds_EmptyPositions_Throws()
    {
        Assert.Throws<ArgumentException>(() => MapBoundsDetector.DetectBounds(Array.Empty<Vector3>()));
    }

    [Test]
    public void DetectBounds_NegativeCoordinates_WorksCorrectly()
    {
        var positions = new[] { new Vector3(-500, 0, -300), new Vector3(-100, 0, -50) };

        var (min, max) = MapBoundsDetector.DetectBounds(positions, padding: 25f);

        Assert.That(min.x, Is.EqualTo(-525f).Within(0.01f));
        Assert.That(max.x, Is.EqualTo(-75f).Within(0.01f));
        Assert.That(min.z, Is.EqualTo(-325f).Within(0.01f));
        Assert.That(max.z, Is.EqualTo(-25f).Within(0.01f));
    }

    [Test]
    public void DetectBounds_IgnoresYForXZBounds()
    {
        // Y values should not affect XZ bounds
        var positions = new[] { new Vector3(0, -999, 0), new Vector3(100, 999, 100) };

        var (min, max) = MapBoundsDetector.DetectBounds(positions, padding: 0f);

        Assert.That(min.x, Is.EqualTo(0f).Within(0.01f));
        Assert.That(max.x, Is.EqualTo(100f).Within(0.01f));
        Assert.That(min.z, Is.EqualTo(0f).Within(0.01f));
        Assert.That(max.z, Is.EqualTo(100f).Within(0.01f));
        // Y is always extreme range
        Assert.That(min.y, Is.EqualTo(-10000f));
        Assert.That(max.y, Is.EqualTo(10000f));
    }
}
