using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Integration;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class MapBoundsDetectorVoxelTests
{
    [Test]
    public void DetectBoundsFromVoxels_ReturnsVoxelMinMax_WithNoPadding()
    {
        var voxelMin = new Vector3(-200, -10, -300);
        var voxelMax = new Vector3(400, 50, 500);

        var (min, max) = MapBoundsDetector.DetectBoundsFromVoxels(voxelMin, voxelMax, padding: 0f);

        Assert.That(min.x, Is.EqualTo(-200f).Within(0.01f));
        Assert.That(min.z, Is.EqualTo(-300f).Within(0.01f));
        Assert.That(max.x, Is.EqualTo(400f).Within(0.01f));
        Assert.That(max.z, Is.EqualTo(500f).Within(0.01f));
    }

    [Test]
    public void DetectBoundsFromVoxels_ExpandsByPadding()
    {
        var voxelMin = new Vector3(-100, 0, -100);
        var voxelMax = new Vector3(100, 20, 100);

        var (min, max) = MapBoundsDetector.DetectBoundsFromVoxels(voxelMin, voxelMax, padding: 25f);

        Assert.That(min.x, Is.EqualTo(-125f).Within(0.01f));
        Assert.That(min.z, Is.EqualTo(-125f).Within(0.01f));
        Assert.That(max.x, Is.EqualTo(125f).Within(0.01f));
        Assert.That(max.z, Is.EqualTo(125f).Within(0.01f));
    }

    [Test]
    public void DetectBoundsFromVoxels_YBounds_AreExtreme()
    {
        var voxelMin = new Vector3(0, -5, 0);
        var voxelMax = new Vector3(100, 30, 100);

        var (min, max) = MapBoundsDetector.DetectBoundsFromVoxels(voxelMin, voxelMax);

        Assert.That(min.y, Is.EqualTo(-10000f));
        Assert.That(max.y, Is.EqualTo(10000f));
    }

    [Test]
    public void DetectBoundsFromVoxels_DefaultPadding_IsZero()
    {
        var voxelMin = new Vector3(-50, 0, -50);
        var voxelMax = new Vector3(50, 10, 50);

        var (min, max) = MapBoundsDetector.DetectBoundsFromVoxels(voxelMin, voxelMax);

        Assert.That(min.x, Is.EqualTo(-50f).Within(0.01f));
        Assert.That(max.x, Is.EqualTo(50f).Within(0.01f));
    }

    [Test]
    public void DetectBoundsFromVoxels_NegativeCoordinates_WorksCorrectly()
    {
        var voxelMin = new Vector3(-500, -20, -400);
        var voxelMax = new Vector3(-100, 10, -50);

        var (min, max) = MapBoundsDetector.DetectBoundsFromVoxels(voxelMin, voxelMax, padding: 10f);

        Assert.That(min.x, Is.EqualTo(-510f).Within(0.01f));
        Assert.That(max.x, Is.EqualTo(-90f).Within(0.01f));
        Assert.That(min.z, Is.EqualTo(-410f).Within(0.01f));
        Assert.That(max.z, Is.EqualTo(-40f).Within(0.01f));
    }

    [Test]
    public void DetectBoundsFromVoxels_VoxelBounds_WiderThanSpawnPoints()
    {
        // Typical scenario: voxel bounds cover more area than spawn points
        var voxelMin = new Vector3(-500, -20, -600);
        var voxelMax = new Vector3(500, 50, 600);

        // Spawn points would only cover a subset
        var spawnPositions = new[] { new Vector3(-100, 0, -200), new Vector3(200, 5, 300) };

        var (vMin, vMax) = MapBoundsDetector.DetectBoundsFromVoxels(voxelMin, voxelMax);
        var (sMin, sMax) = MapBoundsDetector.DetectBounds(spawnPositions, padding: 50f);

        // Voxel bounds should be wider
        Assert.That(vMin.x, Is.LessThan(sMin.x));
        Assert.That(vMin.z, Is.LessThan(sMin.z));
        Assert.That(vMax.x, Is.GreaterThan(sMax.x));
        Assert.That(vMax.z, Is.GreaterThan(sMax.z));
    }

    [Test]
    public void DetectBoundsFromVoxels_LargeMap_HandlesCorrectly()
    {
        // Simulate a large map like Streets
        var voxelMin = new Vector3(-1200, -30, -1500);
        var voxelMax = new Vector3(1200, 80, 1500);

        var (min, max) = MapBoundsDetector.DetectBoundsFromVoxels(voxelMin, voxelMax);

        Assert.That(max.x - min.x, Is.EqualTo(2400f).Within(0.01f));
        Assert.That(max.z - min.z, Is.EqualTo(3000f).Within(0.01f));
    }
}
