using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems
{
    [TestFixture]
    public class PatrolRouteSelectorTests
    {
        // ── No routes / null ─────────────────────────────────────────

        [Test]
        public void SelectRoute_NullRoutes_ReturnsNegativeOne()
        {
            int result = PatrolRouteSelector.SelectRoute(0f, 0f, 0.5f, 0.5f, null, 42);
            Assert.That(result, Is.EqualTo(-1));
        }

        [Test]
        public void SelectRoute_EmptyRoutes_ReturnsNegativeOne()
        {
            int result = PatrolRouteSelector.SelectRoute(0f, 0f, 0.5f, 0.5f, Array.Empty<PatrolRoute>(), 42);
            Assert.That(result, Is.EqualTo(-1));
        }

        // ── Single route ─────────────────────────────────────────────

        [Test]
        public void SelectRoute_SingleRoute_ReturnsZero()
        {
            var routes = new[] { new PatrolRoute("Test", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(10f, 0f, 20f) }) };

            int result = PatrolRouteSelector.SelectRoute(0f, 0f, 0.5f, 0.5f, routes, 42);
            Assert.That(result, Is.EqualTo(0));
        }

        // ── Personality filter ───────────────────────────────────────

        [Test]
        public void SelectRoute_AggressionTooLow_Filtered()
        {
            var routes = new[]
            {
                new PatrolRoute(
                    "Aggressive Only",
                    PatrolRouteType.Perimeter,
                    new[] { new PatrolWaypoint(10f, 0f, 20f) },
                    minAggression: 0.6f,
                    maxAggression: 1.0f
                ),
            };

            int result = PatrolRouteSelector.SelectRoute(0f, 0f, 0.3f, 0.5f, routes, 42);
            Assert.That(result, Is.EqualTo(-1));
        }

        [Test]
        public void SelectRoute_AggressionTooHigh_Filtered()
        {
            var routes = new[]
            {
                new PatrolRoute(
                    "Cautious Only",
                    PatrolRouteType.Perimeter,
                    new[] { new PatrolWaypoint(10f, 0f, 20f) },
                    minAggression: 0f,
                    maxAggression: 0.4f
                ),
            };

            int result = PatrolRouteSelector.SelectRoute(0f, 0f, 0.7f, 0.5f, routes, 42);
            Assert.That(result, Is.EqualTo(-1));
        }

        [Test]
        public void SelectRoute_AggressionWithinRange_Selected()
        {
            var routes = new[]
            {
                new PatrolRoute(
                    "Mid Range",
                    PatrolRouteType.Perimeter,
                    new[] { new PatrolWaypoint(10f, 0f, 20f) },
                    minAggression: 0.3f,
                    maxAggression: 0.7f
                ),
            };

            int result = PatrolRouteSelector.SelectRoute(0f, 0f, 0.5f, 0.5f, routes, 42);
            Assert.That(result, Is.EqualTo(0));
        }

        // ── Raid time filter ─────────────────────────────────────────

        [Test]
        public void SelectRoute_RaidTimeTooEarly_Filtered()
        {
            var routes = new[]
            {
                new PatrolRoute(
                    "Late Raid Only",
                    PatrolRouteType.Perimeter,
                    new[] { new PatrolWaypoint(10f, 0f, 20f) },
                    minRaidTime: 0.5f,
                    maxRaidTime: 1.0f
                ),
            };

            int result = PatrolRouteSelector.SelectRoute(0f, 0f, 0.5f, 0.2f, routes, 42);
            Assert.That(result, Is.EqualTo(-1));
        }

        [Test]
        public void SelectRoute_RaidTimeWithinRange_Selected()
        {
            var routes = new[]
            {
                new PatrolRoute(
                    "Late Raid",
                    PatrolRouteType.Perimeter,
                    new[] { new PatrolWaypoint(10f, 0f, 20f) },
                    minRaidTime: 0.5f,
                    maxRaidTime: 1.0f
                ),
            };

            int result = PatrolRouteSelector.SelectRoute(0f, 0f, 0.5f, 0.7f, routes, 42);
            Assert.That(result, Is.EqualTo(0));
        }

        // ── Proximity preference ─────────────────────────────────────

        [Test]
        public void SelectRoute_PrefersCloserRoute()
        {
            var routes = new[]
            {
                new PatrolRoute("Far Route", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(500f, 0f, 500f) }),
                new PatrolRoute("Near Route", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(5f, 0f, 5f) }),
            };

            int result = PatrolRouteSelector.SelectRoute(0f, 0f, 0.5f, 0.5f, routes, 42);
            Assert.That(result, Is.EqualTo(1), "Should prefer the closer route");
        }

        // ── Route with no waypoints ──────────────────────────────────

        [Test]
        public void SelectRoute_RouteWithNoWaypoints_Skipped()
        {
            var routes = new[]
            {
                new PatrolRoute("Empty", PatrolRouteType.Perimeter, Array.Empty<PatrolWaypoint>()),
                new PatrolRoute("Valid", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(10f, 0f, 20f) }),
            };

            int result = PatrolRouteSelector.SelectRoute(0f, 0f, 0.5f, 0.5f, routes, 42);
            Assert.That(result, Is.EqualTo(1));
        }

        // ── Deterministic with seed ──────────────────────────────────

        [Test]
        public void SelectRoute_SameSeed_SameResult()
        {
            var routes = new[]
            {
                new PatrolRoute("A", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(10f, 0f, 10f) }),
                new PatrolRoute("B", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(10f, 0f, 10f) }),
            };

            int result1 = PatrolRouteSelector.SelectRoute(0f, 0f, 0.5f, 0.5f, routes, 123);
            int result2 = PatrolRouteSelector.SelectRoute(0f, 0f, 0.5f, 0.5f, routes, 123);
            Assert.That(result1, Is.EqualTo(result2));
        }

        // ── Multiple routes with mixed filters ───────────────────────

        [Test]
        public void SelectRoute_SkipsFilteredRoutes_SelectsFirstValid()
        {
            var routes = new[]
            {
                new PatrolRoute("Too Aggressive", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(5f, 0f, 5f) }, minAggression: 0.8f),
                new PatrolRoute("Valid", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(5f, 0f, 5f) }),
            };

            int result = PatrolRouteSelector.SelectRoute(0f, 0f, 0.3f, 0.5f, routes, 42);
            Assert.That(result, Is.EqualTo(1));
        }

        [Test]
        public void SelectRoute_AllFiltered_ReturnsNegativeOne()
        {
            var routes = new[]
            {
                new PatrolRoute(
                    "Only Aggressive",
                    PatrolRouteType.Perimeter,
                    new[] { new PatrolWaypoint(5f, 0f, 5f) },
                    minAggression: 0.8f
                ),
                new PatrolRoute("Only Late Raid", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(5f, 0f, 5f) }, minRaidTime: 0.9f),
            };

            int result = PatrolRouteSelector.SelectRoute(0f, 0f, 0.3f, 0.1f, routes, 42);
            Assert.That(result, Is.EqualTo(-1));
        }
    }
}
