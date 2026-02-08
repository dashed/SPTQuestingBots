using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Core;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class GridCellTests
{
    [Test]
    public void NewCell_HasNoPois_IsNotNavigable()
    {
        var cell = new GridCell(0, 0, new Vector3(50, 0, 50));

        Assert.Multiple(() =>
        {
            Assert.That(cell.POIs, Is.Empty);
            Assert.That(cell.PoiDensity, Is.EqualTo(0f));
            Assert.That(cell.IsNavigable, Is.False);
        });
    }

    [Test]
    public void AddPoi_MakesCellNavigable()
    {
        var cell = new GridCell(1, 2, new Vector3(100, 0, 200));
        cell.AddPoi(new PointOfInterest(new Vector3(100, 0, 200), PoiCategory.Container));

        Assert.Multiple(() =>
        {
            Assert.That(cell.POIs, Has.Count.EqualTo(1));
            Assert.That(cell.IsNavigable, Is.True);
        });
    }

    [Test]
    public void PoiDensity_SumsWeights()
    {
        var cell = new GridCell(0, 0, new Vector3(0, 0, 0));
        cell.AddPoi(new PointOfInterest(new Vector3(0, 0, 0), PoiCategory.Container)); // 1.0
        cell.AddPoi(new PointOfInterest(new Vector3(1, 0, 1), PoiCategory.Quest)); // 1.2
        cell.AddPoi(new PointOfInterest(new Vector3(2, 0, 2), PoiCategory.Synthetic)); // 0.2

        Assert.That(cell.PoiDensity, Is.EqualTo(2.4f).Within(0.001f));
    }

    [Test]
    public void ColAndRow_AreSetCorrectly()
    {
        var cell = new GridCell(3, 7, new Vector3(0, 0, 0));

        Assert.Multiple(() =>
        {
            Assert.That(cell.Col, Is.EqualTo(3));
            Assert.That(cell.Row, Is.EqualTo(7));
        });
    }

    [Test]
    public void Center_IsSetCorrectly()
    {
        var center = new Vector3(150, 5, 250);
        var cell = new GridCell(0, 0, center);

        Assert.Multiple(() =>
        {
            Assert.That(cell.Center.x, Is.EqualTo(150f));
            Assert.That(cell.Center.y, Is.EqualTo(5f));
            Assert.That(cell.Center.z, Is.EqualTo(250f));
        });
    }

    [Test]
    public void Neighbors_DefaultEmpty()
    {
        var cell = new GridCell(0, 0, new Vector3(0, 0, 0));
        Assert.That(cell.Neighbors, Is.Empty);
    }
}
