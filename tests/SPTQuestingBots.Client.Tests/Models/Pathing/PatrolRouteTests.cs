using NUnit.Framework;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.Models.Pathing
{
    [TestFixture]
    public class PatrolWaypointTests
    {
        [Test]
        public void Constructor_SetsPositionAndDefaultPause()
        {
            var wp = new PatrolWaypoint(10f, 5f, 20f);

            Assert.Multiple(() =>
            {
                Assert.That(wp.X, Is.EqualTo(10f));
                Assert.That(wp.Y, Is.EqualTo(5f));
                Assert.That(wp.Z, Is.EqualTo(20f));
                Assert.That(wp.PauseDurationMin, Is.EqualTo(2f));
                Assert.That(wp.PauseDurationMax, Is.EqualTo(5f));
            });
        }

        [Test]
        public void Constructor_CustomPauseDurations()
        {
            var wp = new PatrolWaypoint(1f, 2f, 3f, 8f, 15f);

            Assert.That(wp.PauseDurationMin, Is.EqualTo(8f));
            Assert.That(wp.PauseDurationMax, Is.EqualTo(15f));
        }

        [Test]
        public void DefaultStruct_AllZeros()
        {
            var wp = new PatrolWaypoint();

            Assert.Multiple(() =>
            {
                Assert.That(wp.X, Is.EqualTo(0f));
                Assert.That(wp.Y, Is.EqualTo(0f));
                Assert.That(wp.Z, Is.EqualTo(0f));
                Assert.That(wp.PauseDurationMin, Is.EqualTo(0f));
                Assert.That(wp.PauseDurationMax, Is.EqualTo(0f));
            });
        }
    }

    [TestFixture]
    public class PatrolRouteTypeTests
    {
        [Test]
        public void Values_AreExpected()
        {
            Assert.That((byte)PatrolRouteType.Perimeter, Is.EqualTo(0));
            Assert.That((byte)PatrolRouteType.Interior, Is.EqualTo(1));
            Assert.That((byte)PatrolRouteType.Overwatch, Is.EqualTo(2));
        }
    }

    [TestFixture]
    public class PatrolRouteTests
    {
        [Test]
        public void Constructor_SetsAllFields()
        {
            var waypoints = new[] { new PatrolWaypoint(10f, 0f, 20f), new PatrolWaypoint(30f, 0f, 40f) };

            var route = new PatrolRoute("Test Route", PatrolRouteType.Interior, waypoints, 0.2f, 0.8f, 0.1f, 0.9f, true);

            Assert.Multiple(() =>
            {
                Assert.That(route.Name, Is.EqualTo("Test Route"));
                Assert.That(route.Type, Is.EqualTo(PatrolRouteType.Interior));
                Assert.That(route.Waypoints.Length, Is.EqualTo(2));
                Assert.That(route.MinAggression, Is.EqualTo(0.2f));
                Assert.That(route.MaxAggression, Is.EqualTo(0.8f));
                Assert.That(route.MinRaidTime, Is.EqualTo(0.1f));
                Assert.That(route.MaxRaidTime, Is.EqualTo(0.9f));
                Assert.That(route.IsLoop, Is.True);
            });
        }

        [Test]
        public void Constructor_DefaultFilters()
        {
            var route = new PatrolRoute("Default", PatrolRouteType.Perimeter, System.Array.Empty<PatrolWaypoint>());

            Assert.Multiple(() =>
            {
                Assert.That(route.MinAggression, Is.EqualTo(0f));
                Assert.That(route.MaxAggression, Is.EqualTo(1f));
                Assert.That(route.MinRaidTime, Is.EqualTo(0f));
                Assert.That(route.MaxRaidTime, Is.EqualTo(1f));
            });
        }

        [Test]
        public void IsLoop_DefaultTrue_ForPerimeter()
        {
            var route = new PatrolRoute("P", PatrolRouteType.Perimeter, System.Array.Empty<PatrolWaypoint>());
            Assert.That(route.IsLoop, Is.True);
        }

        [Test]
        public void IsLoop_DefaultTrue_ForInterior()
        {
            var route = new PatrolRoute("I", PatrolRouteType.Interior, System.Array.Empty<PatrolWaypoint>());
            Assert.That(route.IsLoop, Is.True);
        }

        [Test]
        public void IsLoop_DefaultFalse_ForOverwatch()
        {
            var route = new PatrolRoute("O", PatrolRouteType.Overwatch, System.Array.Empty<PatrolWaypoint>());
            Assert.That(route.IsLoop, Is.False);
        }

        [Test]
        public void IsLoop_ExplicitOverride_TakesEffect()
        {
            var route = new PatrolRoute("O", PatrolRouteType.Overwatch, System.Array.Empty<PatrolWaypoint>(), isLoop: true);
            Assert.That(route.IsLoop, Is.True);
        }
    }
}
