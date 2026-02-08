using System;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Core;
using SPTQuestingBots.ZoneMovement.Selection;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class DestinationSelectorTests
{
    private WorldGrid CreateGridWithPois()
    {
        // 3x3 grid, 100x100 area
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(300, 0, 300), 9);

        // Add POIs to make cells navigable
        for (int col = 0; col < grid.Cols; col++)
        {
            for (int row = 0; row < grid.Rows; row++)
            {
                var cell = grid.GetCell(col, row);
                cell.AddPoi(
                    new PointOfInterest(cell.Center, PoiCategory.Synthetic)
                );
            }
        }

        return grid;
    }

    [Test]
    public void SelectsNeighborAlignedWithDirection()
    {
        var grid = CreateGridWithPois();
        var scorer = new CellScorer(poiWeight: 0f);
        var selector = new DestinationSelector(scorer);

        var currentCell = grid.GetCell(1, 1); // Center cell
        Assert.That(currentCell, Is.Not.Null);

        // Composite direction: east (+X)
        var result = selector.SelectDestination(
            grid, currentCell, 1f, 0f, currentCell.Center
        );

        // Should pick the cell to the east (col+1)
        Assert.That(result.Col, Is.GreaterThan(currentCell.Col));
    }

    [Test]
    public void SelectsNeighborAligned_NorthDirection()
    {
        var grid = CreateGridWithPois();
        var scorer = new CellScorer(poiWeight: 0f);
        var selector = new DestinationSelector(scorer);

        var currentCell = grid.GetCell(1, 1);

        // Composite direction: north (+Z)
        var result = selector.SelectDestination(
            grid, currentCell, 0f, 1f, currentCell.Center
        );

        // Should pick the cell to the north (row+1)
        Assert.That(result.Row, Is.GreaterThan(currentCell.Row));
    }

    [Test]
    public void NoNavigableNeighbors_ReturnsCurrent()
    {
        // Grid with no POIs (all cells non-navigable)
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(300, 0, 300), 9);
        var scorer = new CellScorer();
        var selector = new DestinationSelector(scorer);

        var currentCell = grid.GetCell(1, 1);
        // Make current cell navigable but not its neighbors
        currentCell.AddPoi(new PointOfInterest(currentCell.Center, PoiCategory.Synthetic));

        var result = selector.SelectDestination(
            grid, currentCell, 1f, 0f, currentCell.Center
        );

        // Should fall back to current cell
        Assert.That(result, Is.SameAs(currentCell));
    }

    [Test]
    public void CornerCell_WorksWithFewerNeighbors()
    {
        var grid = CreateGridWithPois();
        var scorer = new CellScorer(poiWeight: 0f);
        var selector = new DestinationSelector(scorer);

        var corner = grid.GetCell(0, 0);
        Assert.That(corner.Neighbors, Has.Count.EqualTo(2));

        // Should still select a valid neighbor
        var result = selector.SelectDestination(
            grid, corner, 1f, 0f, corner.Center
        );

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.SameAs(corner)); // Should pick a neighbor
    }

    [Test]
    public void NullScorer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DestinationSelector(null));
    }

    [Test]
    public void PoiRichCell_FavoredByDensityWeight()
    {
        var grid = new WorldGrid(new Vector3(0, 0, 0), new Vector3(300, 0, 300), 9);
        var scorer = new CellScorer(poiWeight: 0.9f); // Heavily favor POI density
        var selector = new DestinationSelector(scorer);

        var currentCell = grid.GetCell(1, 1);
        currentCell.AddPoi(new PointOfInterest(currentCell.Center, PoiCategory.Synthetic));

        // Add many POIs to the east neighbor only
        var eastCell = grid.GetCell(2, 1);
        if (eastCell != null)
        {
            for (int i = 0; i < 5; i++)
                eastCell.AddPoi(
                    new PointOfInterest(eastCell.Center, PoiCategory.Container)
                );
        }

        // Add minimal POI to other neighbors to make them navigable
        var westCell = grid.GetCell(0, 1);
        westCell?.AddPoi(new PointOfInterest(westCell.Center, PoiCategory.Synthetic));
        var northCell = grid.GetCell(1, 2);
        northCell?.AddPoi(new PointOfInterest(northCell.Center, PoiCategory.Synthetic));
        var southCell = grid.GetCell(1, 0);
        southCell?.AddPoi(new PointOfInterest(southCell.Center, PoiCategory.Synthetic));

        // Composite direction points north, but east cell has way more POIs
        var result = selector.SelectDestination(
            grid, currentCell, 0f, 1f, currentCell.Center
        );

        // With 90% poi weight, east cell should win despite perpendicular direction
        Assert.That(result, Is.SameAs(eastCell));
    }

    [Test]
    public void ZeroCompositeDirection_StillSelectsCell()
    {
        var grid = CreateGridWithPois();
        var scorer = new CellScorer(poiWeight: 0f);
        var selector = new DestinationSelector(scorer);

        var currentCell = grid.GetCell(1, 1);

        // Zero composite direction
        var result = selector.SelectDestination(
            grid, currentCell, 0f, 0f, currentCell.Center
        );

        // Should still return a valid cell (all equally scored)
        Assert.That(result, Is.Not.Null);
    }
}
