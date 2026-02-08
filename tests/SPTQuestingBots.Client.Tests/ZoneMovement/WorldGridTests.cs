using System;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Core;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class WorldGridTests
{
    [Test]
    public void Constructor_CreatesGridWithCorrectDimensions()
    {
        // 100x100 area with 100 target cells → cell size ~10
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(100, 0, 100), 100);

        Assert.Multiple(() =>
        {
            Assert.That(grid.Cols, Is.GreaterThan(0));
            Assert.That(grid.Rows, Is.GreaterThan(0));
            Assert.That(grid.Cols * grid.Rows, Is.GreaterThanOrEqualTo(80));
            Assert.That(grid.Cols * grid.Rows, Is.LessThanOrEqualTo(150));
        });
    }

    [Test]
    public void Constructor_AutoComputesCellSize()
    {
        // 1000x1000 area, 150 cells → cell size ~81.6
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(1000, 0, 1000), 150);

        Assert.That(grid.CellSize, Is.GreaterThan(50f));
        Assert.That(grid.CellSize, Is.LessThan(120f));
    }

    [Test]
    public void Constructor_InvalidBounds_Throws()
    {
        Assert.Throws<ArgumentException>(() => new WorldGrid(new Vector3(100, 0, 100), new Vector3(0, 0, 0)));
    }

    [Test]
    public void Constructor_ZeroTargetCells_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldGrid(new Vector3(0, 0, 0), new Vector3(100, 0, 100), 0));
    }

    [Test]
    public void GetCell_ByPosition_ReturnsCorrectCell()
    {
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(100, 0, 100), 100);

        var cell = grid.GetCell(new Vector3(5, 0, 5));
        Assert.That(cell, Is.Not.Null);
        Assert.That(cell.Col, Is.EqualTo(0));
        Assert.That(cell.Row, Is.EqualTo(0));
    }

    [Test]
    public void GetCell_OutOfBounds_ClampsToEdge()
    {
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(100, 0, 100), 100);

        // Far beyond max bounds
        var cell = grid.GetCell(new Vector3(999, 0, 999));
        Assert.That(cell, Is.Not.Null);
        Assert.That(cell.Col, Is.EqualTo(grid.Cols - 1));
        Assert.That(cell.Row, Is.EqualTo(grid.Rows - 1));
    }

    [Test]
    public void GetCell_NegativePosition_ClampsToZero()
    {
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(100, 0, 100), 100);

        var cell = grid.GetCell(new Vector3(-50, 0, -50));
        Assert.That(cell, Is.Not.Null);
        Assert.That(cell.Col, Is.EqualTo(0));
        Assert.That(cell.Row, Is.EqualTo(0));
    }

    [Test]
    public void GetCell_ByIndex_ReturnsCell()
    {
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(100, 0, 100), 100);

        var cell = grid.GetCell(0, 0);
        Assert.That(cell, Is.Not.Null);
    }

    [Test]
    public void GetCell_ByIndex_OutOfBounds_ReturnsNull()
    {
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(100, 0, 100), 100);

        Assert.That(grid.GetCell(-1, 0), Is.Null);
        Assert.That(grid.GetCell(0, -1), Is.Null);
        Assert.That(grid.GetCell(grid.Cols, 0), Is.Null);
        Assert.That(grid.GetCell(0, grid.Rows), Is.Null);
    }

    [Test]
    public void Neighbors_CornerCell_HasTwoNeighbors()
    {
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(100, 0, 100), 4);
        var corner = grid.GetCell(0, 0);

        Assert.That(corner.Neighbors, Has.Count.EqualTo(2));
    }

    [Test]
    public void Neighbors_InteriorCell_HasFourNeighbors()
    {
        // Need at least a 3x3 grid to have interior cells
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(300, 0, 300), 9);

        // Find a cell that should be interior (not on edge)
        if (grid.Cols >= 3 && grid.Rows >= 3)
        {
            var interior = grid.GetCell(1, 1);
            Assert.That(interior.Neighbors, Has.Count.EqualTo(4));
        }
    }

    [Test]
    public void AddPoi_PlacesPOIInCorrectCell()
    {
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(100, 0, 100), 4);
        var poi = new PointOfInterest(new Vector3(10, 0, 10), PoiCategory.Container);

        grid.AddPoi(poi);

        var cell = grid.GetCell(new Vector3(10, 0, 10));
        Assert.That(cell.POIs, Has.Count.EqualTo(1));
        Assert.That(cell.IsNavigable, Is.True);
    }

    [Test]
    public void MaxPoiDensity_ReturnsHighestCellDensity()
    {
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(100, 0, 100), 4);

        // Add POIs to one cell
        grid.AddPoi(new PointOfInterest(new Vector3(10, 0, 10), PoiCategory.Container)); // 1.0
        grid.AddPoi(new PointOfInterest(new Vector3(11, 0, 11), PoiCategory.Quest)); // 1.2

        Assert.That(grid.MaxPoiDensity, Is.EqualTo(2.2f).Within(0.001f));
    }

    [Test]
    public void MaxPoiDensity_EmptyGrid_ReturnsZero()
    {
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(100, 0, 100), 4);
        Assert.That(grid.MaxPoiDensity, Is.EqualTo(0f));
    }

    [Test]
    public void CellCenter_IsWithinCellBounds()
    {
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(100, 0, 100), 4);
        var cell = grid.GetCell(0, 0);

        // Cell center should be at (cellSize/2, y, cellSize/2)
        Assert.That(cell.Center.x, Is.GreaterThan(0f));
        Assert.That(cell.Center.z, Is.GreaterThan(0f));
        Assert.That(cell.Center.x, Is.LessThan(grid.CellSize));
        Assert.That(cell.Center.z, Is.LessThan(grid.CellSize));
    }

    [Test]
    public void SmallArea_MinimumCellSize()
    {
        // Very small area: should still create valid grid
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(2, 0, 2), 1000);

        Assert.Multiple(() =>
        {
            Assert.That(grid.CellSize, Is.GreaterThanOrEqualTo(1f));
            Assert.That(grid.Cols, Is.GreaterThanOrEqualTo(1));
            Assert.That(grid.Rows, Is.GreaterThanOrEqualTo(1));
        });
    }
}
