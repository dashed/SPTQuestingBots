using System;
using System.Collections.Generic;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Models.Pathing;

/// <summary>
/// Per-map patrol route definitions with hardcoded defaults and JSON config overrides.
/// Pure C# — no Unity or EFT dependencies.
/// </summary>
public static class PatrolRouteConfig
{
    /// <summary>
    /// Returns patrol routes for a map, using config overrides if available,
    /// otherwise falling back to hardcoded defaults.
    /// </summary>
    /// <param name="mapId">BSG location ID (case-insensitive).</param>
    /// <param name="overrides">Per-map route overrides from user config. May be null.</param>
    public static PatrolRoute[] GetRoutesForMap(string mapId, Dictionary<string, PatrolRouteEntry[]> overrides)
    {
        // Check overrides first
        if (overrides != null)
        {
            foreach (var kvp in overrides)
            {
                if (string.Equals(kvp.Key, mapId, StringComparison.OrdinalIgnoreCase) && kvp.Value != null)
                {
                    return ConvertEntries(kvp.Value);
                }
            }
        }

        // Hardcoded defaults
        var defaults = GetDefaults();
        foreach (var kvp in defaults)
        {
            if (string.Equals(kvp.Key, mapId, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        return Array.Empty<PatrolRoute>();
    }

    /// <summary>
    /// Converts JSON config entries to PatrolRoute objects.
    /// </summary>
    internal static PatrolRoute[] ConvertEntries(PatrolRouteEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            return Array.Empty<PatrolRoute>();
        }

        var routes = new PatrolRoute[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var waypoints = new PatrolWaypoint[entry.Waypoints?.Length ?? 0];
            for (int j = 0; j < waypoints.Length; j++)
            {
                var wp = entry.Waypoints[j];
                waypoints[j] = new PatrolWaypoint(wp.X, wp.Y, wp.Z, wp.PauseMin, wp.PauseMax);
            }

            PatrolRouteType type;
            switch (entry.Type?.ToLowerInvariant())
            {
                case "interior":
                    type = PatrolRouteType.Interior;
                    break;
                case "overwatch":
                    type = PatrolRouteType.Overwatch;
                    break;
                default:
                    type = PatrolRouteType.Perimeter;
                    break;
            }

            routes[i] = new PatrolRoute(
                entry.Name ?? "Custom Route " + i,
                type,
                waypoints,
                entry.MinAggression,
                entry.MaxAggression,
                entry.MinRaidTime,
                entry.MaxRaidTime,
                entry.IsLoop
            );
        }

        return routes;
    }

    /// <summary>
    /// Hardcoded default patrol routes per map. Positions are approximate world coordinates
    /// derived from BSG BotZone centroids — NavMesh handles precise pathfinding.
    /// </summary>
    internal static Dictionary<string, PatrolRoute[]> GetDefaults()
    {
        return new Dictionary<string, PatrolRoute[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["bigmap"] = new[]
            {
                new PatrolRoute(
                    "Dorms Perimeter",
                    PatrolRouteType.Perimeter,
                    new[]
                    {
                        new PatrolWaypoint(180f, 1f, 240f),
                        new PatrolWaypoint(200f, 1f, 260f),
                        new PatrolWaypoint(220f, 1f, 270f),
                        new PatrolWaypoint(230f, 1f, 250f),
                        new PatrolWaypoint(210f, 1f, 230f),
                        new PatrolWaypoint(190f, 1f, 230f),
                    }
                ),
                new PatrolRoute(
                    "Customs Road",
                    PatrolRouteType.Perimeter,
                    new[]
                    {
                        new PatrolWaypoint(50f, 1f, 100f),
                        new PatrolWaypoint(100f, 1f, 110f),
                        new PatrolWaypoint(150f, 1f, 120f),
                        new PatrolWaypoint(200f, 1f, 130f),
                        new PatrolWaypoint(250f, 1f, 125f),
                        new PatrolWaypoint(300f, 1f, 120f),
                        new PatrolWaypoint(350f, 1f, 115f),
                        new PatrolWaypoint(400f, 1f, 110f),
                    }
                ),
                new PatrolRoute(
                    "Construction Overwatch",
                    PatrolRouteType.Overwatch,
                    new[]
                    {
                        new PatrolWaypoint(280f, 5f, 180f, 5f, 10f),
                        new PatrolWaypoint(300f, 8f, 190f, 5f, 10f),
                        new PatrolWaypoint(310f, 6f, 200f, 5f, 10f),
                        new PatrolWaypoint(290f, 7f, 195f, 5f, 10f),
                    },
                    minAggression: 0f,
                    maxAggression: 0.5f
                ),
            },
            ["interchange"] = new[]
            {
                new PatrolRoute(
                    "Mall Interior",
                    PatrolRouteType.Interior,
                    new[]
                    {
                        new PatrolWaypoint(-20f, 22f, 80f),
                        new PatrolWaypoint(0f, 22f, 100f),
                        new PatrolWaypoint(30f, 22f, 110f),
                        new PatrolWaypoint(60f, 22f, 100f),
                        new PatrolWaypoint(80f, 22f, 80f),
                        new PatrolWaypoint(40f, 22f, 70f),
                    }
                ),
                new PatrolRoute(
                    "Parking Perimeter",
                    PatrolRouteType.Perimeter,
                    new[]
                    {
                        new PatrolWaypoint(-60f, 18f, 20f),
                        new PatrolWaypoint(-40f, 18f, 40f),
                        new PatrolWaypoint(-20f, 18f, 30f),
                        new PatrolWaypoint(0f, 18f, 20f),
                        new PatrolWaypoint(-30f, 18f, 10f),
                    }
                ),
            },
            ["shoreline"] = new[]
            {
                new PatrolRoute(
                    "Resort Sweep",
                    PatrolRouteType.Interior,
                    new[]
                    {
                        new PatrolWaypoint(-120f, 1f, 150f),
                        new PatrolWaypoint(-100f, 1f, 170f),
                        new PatrolWaypoint(-80f, 1f, 180f),
                        new PatrolWaypoint(-60f, 1f, 170f),
                        new PatrolWaypoint(-40f, 1f, 160f),
                        new PatrolWaypoint(-70f, 1f, 150f),
                    }
                ),
                new PatrolRoute(
                    "Shoreline Path",
                    PatrolRouteType.Perimeter,
                    new[]
                    {
                        new PatrolWaypoint(-200f, 1f, -50f),
                        new PatrolWaypoint(-150f, 1f, -60f),
                        new PatrolWaypoint(-100f, 1f, -55f),
                        new PatrolWaypoint(-50f, 1f, -45f),
                        new PatrolWaypoint(0f, 1f, -40f),
                        new PatrolWaypoint(50f, 1f, -50f),
                    }
                ),
            },
            ["rezervbase"] = new[]
            {
                new PatrolRoute(
                    "Bunker Patrol",
                    PatrolRouteType.Interior,
                    new[]
                    {
                        new PatrolWaypoint(50f, -10f, 80f),
                        new PatrolWaypoint(70f, -10f, 100f),
                        new PatrolWaypoint(90f, -10f, 110f),
                        new PatrolWaypoint(80f, -10f, 90f),
                        new PatrolWaypoint(60f, -10f, 85f),
                    }
                ),
                new PatrolRoute(
                    "Base Perimeter",
                    PatrolRouteType.Perimeter,
                    new[]
                    {
                        new PatrolWaypoint(-50f, 1f, 30f),
                        new PatrolWaypoint(-20f, 1f, 60f),
                        new PatrolWaypoint(20f, 1f, 80f),
                        new PatrolWaypoint(60f, 1f, 70f),
                        new PatrolWaypoint(80f, 1f, 40f),
                        new PatrolWaypoint(40f, 1f, 20f),
                    }
                ),
            },
            ["woods"] = new[]
            {
                new PatrolRoute(
                    "Sawmill Circuit",
                    PatrolRouteType.Perimeter,
                    new[]
                    {
                        new PatrolWaypoint(-80f, 1f, 150f),
                        new PatrolWaypoint(-50f, 1f, 170f),
                        new PatrolWaypoint(-20f, 1f, 180f),
                        new PatrolWaypoint(10f, 1f, 165f),
                        new PatrolWaypoint(-30f, 1f, 145f),
                    }
                ),
            },
            // Small/indoor maps: no patrol routes
            ["factory4_day"] = Array.Empty<PatrolRoute>(),
            ["factory4_night"] = Array.Empty<PatrolRoute>(),
            ["laboratory"] = Array.Empty<PatrolRoute>(),
            ["sandbox"] = Array.Empty<PatrolRoute>(),
            ["sandbox_high"] = Array.Empty<PatrolRoute>(),
        };
    }
}
