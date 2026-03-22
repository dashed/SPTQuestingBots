using NUnit.Framework;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.Models;

[TestFixture]
public class PatrolWaypointRichDataTests
{
    [Test]
    public void Constructor_DefaultsRichFields_ToZeroFalse()
    {
        var wp = new PatrolWaypoint(10, 20, 30);

        Assert.That(wp.ShallSit, Is.False);
        Assert.That(wp.PointType, Is.EqualTo(PatrolPointTypeId.CheckPoint));
        Assert.That(wp.HasLookDirection, Is.False);
        Assert.That(wp.LookDirX, Is.EqualTo(0f));
        Assert.That(wp.LookDirY, Is.EqualTo(0f));
        Assert.That(wp.LookDirZ, Is.EqualTo(0f));
        Assert.That(wp.SubPointCount, Is.EqualTo(0));
    }

    [Test]
    public void Constructor_PreservesPositionAndPause()
    {
        var wp = new PatrolWaypoint(1, 2, 3, 5, 10);

        Assert.That(wp.X, Is.EqualTo(1f));
        Assert.That(wp.Y, Is.EqualTo(2f));
        Assert.That(wp.Z, Is.EqualTo(3f));
        Assert.That(wp.PauseDurationMin, Is.EqualTo(5f));
        Assert.That(wp.PauseDurationMax, Is.EqualTo(10f));
    }

    [Test]
    public void ShallSit_CanBeSetTrue()
    {
        var wp = new PatrolWaypoint(0, 0, 0) { ShallSit = true };
        Assert.That(wp.ShallSit, Is.True);
    }

    [Test]
    public void PointType_StayPoint()
    {
        var wp = new PatrolWaypoint(0, 0, 0) { PointType = PatrolPointTypeId.StayPoint };
        Assert.That(wp.PointType, Is.EqualTo(PatrolPointTypeId.StayPoint));
    }

    [Test]
    public void PointType_CheckPoint()
    {
        var wp = new PatrolWaypoint(0, 0, 0) { PointType = PatrolPointTypeId.CheckPoint };
        Assert.That(wp.PointType, Is.EqualTo(PatrolPointTypeId.CheckPoint));
    }

    [Test]
    public void LookDirection_CanBeSet()
    {
        var wp = new PatrolWaypoint(0, 0, 0)
        {
            HasLookDirection = true,
            LookDirX = 0.707f,
            LookDirY = 0f,
            LookDirZ = 0.707f,
        };

        Assert.That(wp.HasLookDirection, Is.True);
        Assert.That(wp.LookDirX, Is.EqualTo(0.707f).Within(0.001f));
        Assert.That(wp.LookDirY, Is.EqualTo(0f));
        Assert.That(wp.LookDirZ, Is.EqualTo(0.707f).Within(0.001f));
    }

    [Test]
    public void SubPointCount_CanBeSet()
    {
        var wp = new PatrolWaypoint(0, 0, 0) { SubPointCount = 3 };
        Assert.That(wp.SubPointCount, Is.EqualTo(3));
    }

    [Test]
    public void SubPointCount_MaxValue()
    {
        var wp = new PatrolWaypoint(0, 0, 0) { SubPointCount = 255 };
        Assert.That(wp.SubPointCount, Is.EqualTo(255));
    }

    [Test]
    public void PatrolPointTypeId_Constants()
    {
        Assert.That(PatrolPointTypeId.CheckPoint, Is.EqualTo(0));
        Assert.That(PatrolPointTypeId.StayPoint, Is.EqualTo(1));
    }

    [Test]
    public void StayPoint_WithSit_FullSetup()
    {
        var wp = new PatrolWaypoint(100, 5, 200, 8, 20)
        {
            ShallSit = true,
            PointType = PatrolPointTypeId.StayPoint,
            HasLookDirection = true,
            LookDirX = 1f,
            LookDirY = 0f,
            LookDirZ = 0f,
            SubPointCount = 2,
        };

        Assert.That(wp.X, Is.EqualTo(100f));
        Assert.That(wp.Y, Is.EqualTo(5f));
        Assert.That(wp.Z, Is.EqualTo(200f));
        Assert.That(wp.PauseDurationMin, Is.EqualTo(8f));
        Assert.That(wp.PauseDurationMax, Is.EqualTo(20f));
        Assert.That(wp.ShallSit, Is.True);
        Assert.That(wp.PointType, Is.EqualTo(PatrolPointTypeId.StayPoint));
        Assert.That(wp.HasLookDirection, Is.True);
        Assert.That(wp.LookDirX, Is.EqualTo(1f));
        Assert.That(wp.SubPointCount, Is.EqualTo(2));
    }

    [Test]
    public void PatrolRoute_WithRichWaypoints()
    {
        var waypoints = new[]
        {
            new PatrolWaypoint(0, 0, 0) { PointType = PatrolPointTypeId.CheckPoint },
            new PatrolWaypoint(50, 0, 50)
            {
                PointType = PatrolPointTypeId.StayPoint,
                ShallSit = true,
                PauseDurationMin = 10,
                PauseDurationMax = 30,
            },
            new PatrolWaypoint(100, 0, 0)
            {
                PointType = PatrolPointTypeId.CheckPoint,
                HasLookDirection = true,
                LookDirX = 0,
                LookDirZ = -1,
            },
        };

        var route = new PatrolRoute("TestRoute", PatrolRouteType.Interior, waypoints);

        Assert.That(route.Waypoints.Length, Is.EqualTo(3));
        Assert.That(route.Waypoints[0].PointType, Is.EqualTo(PatrolPointTypeId.CheckPoint));
        Assert.That(route.Waypoints[1].PointType, Is.EqualTo(PatrolPointTypeId.StayPoint));
        Assert.That(route.Waypoints[1].ShallSit, Is.True);
        Assert.That(route.Waypoints[2].HasLookDirection, Is.True);
    }
}
