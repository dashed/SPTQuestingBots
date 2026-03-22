using NUnit.Framework;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Client.Tests.Helpers;

[TestFixture]
public class BotZoneEntranceHelperTests
{
    [Test]
    public void TryFindNearestEntrance_NullArray_ReturnsFalse()
    {
        bool found = EntrancePointScorer.TryFindNearestEntrance(0, 0, 0, null, out _);
        Assert.That(found, Is.False);
    }

    [Test]
    public void TryFindNearestEntrance_EmptyArray_ReturnsFalse()
    {
        bool found = EntrancePointScorer.TryFindNearestEntrance(0, 0, 0, System.Array.Empty<EntrancePointData>(), out _);
        Assert.That(found, Is.False);
    }

    [Test]
    public void TryFindNearestEntrance_SingleEntrance_ReturnsIt()
    {
        var entrances = new[]
        {
            new EntrancePointData
            {
                OutsideX = 10,
                OutsideY = 0,
                OutsideZ = 10,
                InsideX = 12,
                InsideY = 0,
                InsideZ = 12,
                CenterX = 11,
                CenterY = 0,
                CenterZ = 11,
                ConnectedAreaId = 5,
                Id = 42,
            },
        };

        bool found = EntrancePointScorer.TryFindNearestEntrance(0, 0, 0, entrances, out var nearest);

        Assert.That(found, Is.True);
        Assert.That(nearest.Id, Is.EqualTo(42));
        Assert.That(nearest.ConnectedAreaId, Is.EqualTo(5));
    }

    [Test]
    public void TryFindNearestEntrance_ReturnsClosest()
    {
        var entrances = new[]
        {
            new EntrancePointData
            {
                CenterX = 100,
                CenterY = 0,
                CenterZ = 100,
                Id = 1,
            },
            new EntrancePointData
            {
                CenterX = 10,
                CenterY = 0,
                CenterZ = 10,
                Id = 2,
            },
            new EntrancePointData
            {
                CenterX = 50,
                CenterY = 0,
                CenterZ = 50,
                Id = 3,
            },
        };

        bool found = EntrancePointScorer.TryFindNearestEntrance(0, 0, 0, entrances, out var nearest);

        Assert.That(found, Is.True);
        Assert.That(nearest.Id, Is.EqualTo(2));
    }

    [Test]
    public void TryFindNearestEntrance_UsesAllDimensions()
    {
        var entrances = new[]
        {
            new EntrancePointData
            {
                CenterX = 0,
                CenterY = 100,
                CenterZ = 0,
                Id = 1,
            },
            new EntrancePointData
            {
                CenterX = 10,
                CenterY = 0,
                CenterZ = 10,
                Id = 2,
            },
        };

        bool found = EntrancePointScorer.TryFindNearestEntrance(0, 0, 0, entrances, out var nearest);

        Assert.That(found, Is.True);
        // Entry 2 is closer in total 3D distance
        Assert.That(nearest.Id, Is.EqualTo(2));
    }

    [Test]
    public void EntrancePointData_FieldsStoredCorrectly()
    {
        var data = new EntrancePointData
        {
            OutsideX = 1,
            OutsideY = 2,
            OutsideZ = 3,
            InsideX = 4,
            InsideY = 5,
            InsideZ = 6,
            CenterX = 7,
            CenterY = 8,
            CenterZ = 9,
            ConnectedAreaId = 10,
            Id = 11,
        };

        Assert.That(data.OutsideX, Is.EqualTo(1f));
        Assert.That(data.OutsideY, Is.EqualTo(2f));
        Assert.That(data.OutsideZ, Is.EqualTo(3f));
        Assert.That(data.InsideX, Is.EqualTo(4f));
        Assert.That(data.InsideY, Is.EqualTo(5f));
        Assert.That(data.InsideZ, Is.EqualTo(6f));
        Assert.That(data.CenterX, Is.EqualTo(7f));
        Assert.That(data.CenterY, Is.EqualTo(8f));
        Assert.That(data.CenterZ, Is.EqualTo(9f));
        Assert.That(data.ConnectedAreaId, Is.EqualTo(10));
        Assert.That(data.Id, Is.EqualTo(11));
    }
}
