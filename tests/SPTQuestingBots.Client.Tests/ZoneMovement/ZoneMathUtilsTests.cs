using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Core;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class ZoneMathUtilsTests
{
    // ── GetDominantCategory ──────────────────────────────────────────

    [Test]
    public void GetDominantCategory_EmptyCell_ReturnsSynthetic()
    {
        var cell = new GridCell(0, 0, new Vector3(0, 0, 0));

        var result = ZoneMathUtils.GetDominantCategory(cell);

        Assert.That(result, Is.EqualTo(PoiCategory.Synthetic));
    }

    [Test]
    public void GetDominantCategory_SingleContainer_ReturnsContainer()
    {
        var cell = new GridCell(0, 0, new Vector3(0, 0, 0));
        cell.AddPoi(new PointOfInterest(new Vector3(1, 0, 1), PoiCategory.Container));

        var result = ZoneMathUtils.GetDominantCategory(cell);

        Assert.That(result, Is.EqualTo(PoiCategory.Container));
    }

    [Test]
    public void GetDominantCategory_SingleExfil_ReturnsExfil()
    {
        var cell = new GridCell(0, 0, new Vector3(0, 0, 0));
        cell.AddPoi(new PointOfInterest(new Vector3(1, 0, 1), PoiCategory.Exfil));

        var result = ZoneMathUtils.GetDominantCategory(cell);

        Assert.That(result, Is.EqualTo(PoiCategory.Exfil));
    }

    [Test]
    public void GetDominantCategory_MultipleContainers_ReturnsContainer()
    {
        var cell = new GridCell(0, 0, new Vector3(0, 0, 0));
        cell.AddPoi(new PointOfInterest(new Vector3(1, 0, 1), PoiCategory.Container));
        cell.AddPoi(new PointOfInterest(new Vector3(2, 0, 2), PoiCategory.Container));
        cell.AddPoi(new PointOfInterest(new Vector3(3, 0, 3), PoiCategory.SpawnPoint));

        var result = ZoneMathUtils.GetDominantCategory(cell);

        Assert.That(result, Is.EqualTo(PoiCategory.Container));
    }

    [Test]
    public void GetDominantCategory_MixedCategories_ReturnsHighestWeight()
    {
        var cell = new GridCell(0, 0, new Vector3(0, 0, 0));
        // Quest has default weight 1.2, Container 1.0, SpawnPoint 0.3
        cell.AddPoi(new PointOfInterest(new Vector3(1, 0, 1), PoiCategory.Container));
        cell.AddPoi(new PointOfInterest(new Vector3(2, 0, 2), PoiCategory.Quest));
        cell.AddPoi(new PointOfInterest(new Vector3(3, 0, 3), PoiCategory.SpawnPoint));

        var result = ZoneMathUtils.GetDominantCategory(cell);

        Assert.That(result, Is.EqualTo(PoiCategory.Quest));
    }

    [Test]
    public void GetDominantCategory_MultipleSpawnPointsBeatSingleQuest()
    {
        var cell = new GridCell(0, 0, new Vector3(0, 0, 0));
        // 5 spawn points (5 * 0.3 = 1.5) > 1 quest (1 * 1.2 = 1.2)
        cell.AddPoi(new PointOfInterest(new Vector3(1, 0, 1), PoiCategory.Quest));
        for (int i = 0; i < 5; i++)
            cell.AddPoi(new PointOfInterest(new Vector3(i, 0, i), PoiCategory.SpawnPoint));

        var result = ZoneMathUtils.GetDominantCategory(cell);

        Assert.That(result, Is.EqualTo(PoiCategory.SpawnPoint));
    }

    [Test]
    public void GetDominantCategory_CustomWeights_RespectedCorrectly()
    {
        var cell = new GridCell(0, 0, new Vector3(0, 0, 0));
        // One exfil with huge custom weight should win
        cell.AddPoi(new PointOfInterest(new Vector3(1, 0, 1), PoiCategory.Container));
        cell.AddPoi(new PointOfInterest(new Vector3(2, 0, 2), PoiCategory.Container));
        cell.AddPoi(new PointOfInterest(new Vector3(3, 0, 3), PoiCategory.Exfil, 10.0f));

        var result = ZoneMathUtils.GetDominantCategory(cell);

        Assert.That(result, Is.EqualTo(PoiCategory.Exfil));
    }

    [Test]
    public void GetDominantCategory_LooseLoot_ReturnedWhenDominant()
    {
        var cell = new GridCell(0, 0, new Vector3(0, 0, 0));
        cell.AddPoi(new PointOfInterest(new Vector3(1, 0, 1), PoiCategory.LooseLoot, 5.0f));
        cell.AddPoi(new PointOfInterest(new Vector3(2, 0, 2), PoiCategory.Synthetic));

        var result = ZoneMathUtils.GetDominantCategory(cell);

        Assert.That(result, Is.EqualTo(PoiCategory.LooseLoot));
    }

    [Test]
    public void GetDominantCategory_OnlySynthetic_ReturnsSynthetic()
    {
        var cell = new GridCell(0, 0, new Vector3(0, 0, 0));
        cell.AddPoi(new PointOfInterest(new Vector3(1, 0, 1), PoiCategory.Synthetic));

        var result = ZoneMathUtils.GetDominantCategory(cell);

        Assert.That(result, Is.EqualTo(PoiCategory.Synthetic));
    }

    [Test]
    public void GetDominantCategory_AllCategoriesPresent_HighestWeightWins()
    {
        var cell = new GridCell(0, 0, new Vector3(0, 0, 0));
        cell.AddPoi(new PointOfInterest(new Vector3(1, 0, 1), PoiCategory.Container)); // 1.0
        cell.AddPoi(new PointOfInterest(new Vector3(2, 0, 2), PoiCategory.LooseLoot)); // 0.8
        cell.AddPoi(new PointOfInterest(new Vector3(3, 0, 3), PoiCategory.Quest)); // 1.2
        cell.AddPoi(new PointOfInterest(new Vector3(4, 0, 4), PoiCategory.Exfil)); // 0.5
        cell.AddPoi(new PointOfInterest(new Vector3(5, 0, 5), PoiCategory.SpawnPoint)); // 0.3
        cell.AddPoi(new PointOfInterest(new Vector3(6, 0, 6), PoiCategory.Synthetic)); // 0.2

        var result = ZoneMathUtils.GetDominantCategory(cell);

        Assert.That(result, Is.EqualTo(PoiCategory.Quest));
    }

    // ── ComputeCentroid ──────────────────────────────────────────────

    [Test]
    public void ComputeCentroid_SinglePoint_ReturnsSamePoint()
    {
        var positions = new List<Vector3> { new Vector3(10, 20, 30) };

        var result = ZoneMathUtils.ComputeCentroid(positions);

        Assert.That(result.x, Is.EqualTo(10f).Within(0.001f));
        Assert.That(result.y, Is.EqualTo(20f).Within(0.001f));
        Assert.That(result.z, Is.EqualTo(30f).Within(0.001f));
    }

    [Test]
    public void ComputeCentroid_TwoPoints_ReturnsMidpoint()
    {
        var positions = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(10, 20, 30) };

        var result = ZoneMathUtils.ComputeCentroid(positions);

        Assert.That(result.x, Is.EqualTo(5f).Within(0.001f));
        Assert.That(result.y, Is.EqualTo(10f).Within(0.001f));
        Assert.That(result.z, Is.EqualTo(15f).Within(0.001f));
    }

    [Test]
    public void ComputeCentroid_MultiplePoints_ReturnsAverage()
    {
        var positions = new List<Vector3> { new Vector3(1, 2, 3), new Vector3(4, 5, 6), new Vector3(7, 8, 9) };

        var result = ZoneMathUtils.ComputeCentroid(positions);

        Assert.That(result.x, Is.EqualTo(4f).Within(0.001f));
        Assert.That(result.y, Is.EqualTo(5f).Within(0.001f));
        Assert.That(result.z, Is.EqualTo(6f).Within(0.001f));
    }

    [Test]
    public void ComputeCentroid_NegativeCoordinates_CorrectResult()
    {
        var positions = new List<Vector3> { new Vector3(-10, -20, -30), new Vector3(10, 20, 30) };

        var result = ZoneMathUtils.ComputeCentroid(positions);

        Assert.That(result.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(result.y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(result.z, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ComputeCentroid_AllSamePoint_ReturnsThatPoint()
    {
        var positions = new List<Vector3> { new Vector3(5, 5, 5), new Vector3(5, 5, 5), new Vector3(5, 5, 5) };

        var result = ZoneMathUtils.ComputeCentroid(positions);

        Assert.That(result.x, Is.EqualTo(5f).Within(0.001f));
        Assert.That(result.y, Is.EqualTo(5f).Within(0.001f));
        Assert.That(result.z, Is.EqualTo(5f).Within(0.001f));
    }

    // ── ComputeMomentum ────────────────────────────────────────────

    [Test]
    public void ComputeMomentum_SamePoint_ReturnsZero()
    {
        var (momX, momZ) = ZoneMathUtils.ComputeMomentum(new Vector3(5, 0, 5), new Vector3(5, 0, 5));

        Assert.That(momX, Is.EqualTo(0f).Within(0.001f));
        Assert.That(momZ, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ComputeMomentum_EastDirection_ReturnsPositiveX()
    {
        var (momX, momZ) = ZoneMathUtils.ComputeMomentum(new Vector3(0, 0, 0), new Vector3(10, 0, 0));

        Assert.That(momX, Is.EqualTo(1f).Within(0.001f));
        Assert.That(momZ, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ComputeMomentum_NorthDirection_ReturnsPositiveZ()
    {
        var (momX, momZ) = ZoneMathUtils.ComputeMomentum(new Vector3(0, 0, 0), new Vector3(0, 0, 10));

        Assert.That(momX, Is.EqualTo(0f).Within(0.001f));
        Assert.That(momZ, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void ComputeMomentum_DiagonalDirection_Normalized()
    {
        var (momX, momZ) = ZoneMathUtils.ComputeMomentum(new Vector3(0, 0, 0), new Vector3(10, 0, 10));

        float expected = (float)(1.0 / Math.Sqrt(2.0));
        Assert.That(momX, Is.EqualTo(expected).Within(0.001f));
        Assert.That(momZ, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void ComputeMomentum_IgnoresYAxis()
    {
        var (momX, momZ) = ZoneMathUtils.ComputeMomentum(new Vector3(0, 0, 0), new Vector3(10, 999, 0));

        // Y difference should not affect XZ momentum
        Assert.That(momX, Is.EqualTo(1f).Within(0.001f));
        Assert.That(momZ, Is.EqualTo(0f).Within(0.001f));
    }
}
