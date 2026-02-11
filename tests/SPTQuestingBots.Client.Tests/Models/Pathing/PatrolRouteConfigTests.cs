using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.Models.Pathing;

[TestFixture]
public class PatrolRouteConfigTests
{
    // ── Default route loading ─────────────────────────────────────

    [Test]
    public void GetRoutesForMap_Bigmap_Returns3Routes()
    {
        var routes = PatrolRouteConfig.GetRoutesForMap("bigmap", null);
        Assert.That(routes.Length, Is.EqualTo(3));
    }

    [Test]
    public void GetRoutesForMap_Bigmap_FirstRouteIsDormsPerimeter()
    {
        var routes = PatrolRouteConfig.GetRoutesForMap("bigmap", null);
        Assert.That(routes[0].Name, Is.EqualTo("Dorms Perimeter"));
        Assert.That(routes[0].Type, Is.EqualTo(PatrolRouteType.Perimeter));
        Assert.That(routes[0].IsLoop, Is.True);
    }

    [Test]
    public void GetRoutesForMap_Bigmap_OverwatchRoute_NotLoop()
    {
        var routes = PatrolRouteConfig.GetRoutesForMap("bigmap", null);
        var overwatch = routes[2];
        Assert.That(overwatch.Name, Is.EqualTo("Construction Overwatch"));
        Assert.That(overwatch.Type, Is.EqualTo(PatrolRouteType.Overwatch));
        Assert.That(overwatch.IsLoop, Is.False);
    }

    [Test]
    public void GetRoutesForMap_Interchange_Returns2Routes()
    {
        var routes = PatrolRouteConfig.GetRoutesForMap("interchange", null);
        Assert.That(routes.Length, Is.EqualTo(2));
    }

    [Test]
    public void GetRoutesForMap_Shoreline_Returns2Routes()
    {
        var routes = PatrolRouteConfig.GetRoutesForMap("shoreline", null);
        Assert.That(routes.Length, Is.EqualTo(2));
    }

    [Test]
    public void GetRoutesForMap_Rezervbase_Returns2Routes()
    {
        var routes = PatrolRouteConfig.GetRoutesForMap("rezervbase", null);
        Assert.That(routes.Length, Is.EqualTo(2));
    }

    [Test]
    public void GetRoutesForMap_Woods_Returns1Route()
    {
        var routes = PatrolRouteConfig.GetRoutesForMap("woods", null);
        Assert.That(routes.Length, Is.EqualTo(1));
        Assert.That(routes[0].Name, Is.EqualTo("Sawmill Circuit"));
    }

    [Test]
    public void GetRoutesForMap_Factory_ReturnsEmpty()
    {
        var routes = PatrolRouteConfig.GetRoutesForMap("factory4_day", null);
        Assert.That(routes.Length, Is.EqualTo(0));
    }

    [Test]
    public void GetRoutesForMap_Laboratory_ReturnsEmpty()
    {
        var routes = PatrolRouteConfig.GetRoutesForMap("laboratory", null);
        Assert.That(routes.Length, Is.EqualTo(0));
    }

    [Test]
    public void GetRoutesForMap_UnknownMap_ReturnsEmpty()
    {
        var routes = PatrolRouteConfig.GetRoutesForMap("nonexistent_map", null);
        Assert.That(routes.Length, Is.EqualTo(0));
    }

    [Test]
    public void GetRoutesForMap_CaseInsensitive()
    {
        var routes = PatrolRouteConfig.GetRoutesForMap("BIGMAP", null);
        Assert.That(routes.Length, Is.EqualTo(3));
    }

    // ── Waypoint validation ───────────────────────────────────────

    [Test]
    public void AllDefaultRoutes_HaveWaypoints()
    {
        var defaults = PatrolRouteConfig.GetDefaults();
        foreach (var kvp in defaults)
        {
            foreach (var route in kvp.Value)
            {
                Assert.That(
                    route.Waypoints.Length,
                    Is.GreaterThanOrEqualTo(0),
                    $"Route '{route.Name}' on map '{kvp.Key}' has no waypoints"
                );
            }
        }
    }

    [Test]
    public void AllDefaultRoutes_HaveValidAggressionRange()
    {
        var defaults = PatrolRouteConfig.GetDefaults();
        foreach (var kvp in defaults)
        {
            foreach (var route in kvp.Value)
            {
                Assert.That(
                    route.MinAggression,
                    Is.LessThanOrEqualTo(route.MaxAggression),
                    $"Route '{route.Name}' on '{kvp.Key}': MinAggression > MaxAggression"
                );
            }
        }
    }

    // ── Config override ───────────────────────────────────────────

    [Test]
    public void GetRoutesForMap_WithOverride_UsesOverride()
    {
        var overrides = new Dictionary<string, PatrolRouteEntry[]>
        {
            ["bigmap"] = new[]
            {
                new PatrolRouteEntry
                {
                    Name = "Custom Route",
                    Type = "Interior",
                    Waypoints = new[]
                    {
                        new PatrolWaypointEntry
                        {
                            X = 1f,
                            Y = 2f,
                            Z = 3f,
                            PauseMin = 1f,
                            PauseMax = 2f,
                        },
                        new PatrolWaypointEntry
                        {
                            X = 4f,
                            Y = 5f,
                            Z = 6f,
                        },
                    },
                    IsLoop = false,
                },
            },
        };

        var routes = PatrolRouteConfig.GetRoutesForMap("bigmap", overrides);

        Assert.That(routes.Length, Is.EqualTo(1));
        Assert.That(routes[0].Name, Is.EqualTo("Custom Route"));
        Assert.That(routes[0].Type, Is.EqualTo(PatrolRouteType.Interior));
        Assert.That(routes[0].Waypoints.Length, Is.EqualTo(2));
        Assert.That(routes[0].Waypoints[0].X, Is.EqualTo(1f));
        Assert.That(routes[0].IsLoop, Is.False);
    }

    [Test]
    public void GetRoutesForMap_Override_DoesNotAffectOtherMaps()
    {
        var overrides = new Dictionary<string, PatrolRouteEntry[]>
        {
            ["bigmap"] = new[]
            {
                new PatrolRouteEntry { Name = "Custom Only", Waypoints = new[] { new PatrolWaypointEntry { X = 1f } } },
            },
        };

        // Interchange should still use defaults
        var routes = PatrolRouteConfig.GetRoutesForMap("interchange", overrides);
        Assert.That(routes.Length, Is.EqualTo(2));
    }

    // ── ConvertEntries ───────────────────────────────────────────

    [Test]
    public void ConvertEntries_Null_ReturnsEmpty()
    {
        var result = PatrolRouteConfig.ConvertEntries(null);
        Assert.That(result.Length, Is.EqualTo(0));
    }

    [Test]
    public void ConvertEntries_Empty_ReturnsEmpty()
    {
        var result = PatrolRouteConfig.ConvertEntries(Array.Empty<PatrolRouteEntry>());
        Assert.That(result.Length, Is.EqualTo(0));
    }

    [Test]
    public void ConvertEntries_TypeMapping_Perimeter()
    {
        var entries = new[]
        {
            new PatrolRouteEntry { Type = "Perimeter", Waypoints = Array.Empty<PatrolWaypointEntry>() },
        };
        var result = PatrolRouteConfig.ConvertEntries(entries);
        Assert.That(result[0].Type, Is.EqualTo(PatrolRouteType.Perimeter));
    }

    [Test]
    public void ConvertEntries_TypeMapping_Interior()
    {
        var entries = new[]
        {
            new PatrolRouteEntry { Type = "Interior", Waypoints = Array.Empty<PatrolWaypointEntry>() },
        };
        var result = PatrolRouteConfig.ConvertEntries(entries);
        Assert.That(result[0].Type, Is.EqualTo(PatrolRouteType.Interior));
    }

    [Test]
    public void ConvertEntries_TypeMapping_Overwatch()
    {
        var entries = new[]
        {
            new PatrolRouteEntry { Type = "Overwatch", Waypoints = Array.Empty<PatrolWaypointEntry>() },
        };
        var result = PatrolRouteConfig.ConvertEntries(entries);
        Assert.That(result[0].Type, Is.EqualTo(PatrolRouteType.Overwatch));
    }

    [Test]
    public void ConvertEntries_TypeMapping_UnknownDefaultsToPerimeter()
    {
        var entries = new[]
        {
            new PatrolRouteEntry { Type = "invalid", Waypoints = Array.Empty<PatrolWaypointEntry>() },
        };
        var result = PatrolRouteConfig.ConvertEntries(entries);
        Assert.That(result[0].Type, Is.EqualTo(PatrolRouteType.Perimeter));
    }
}
