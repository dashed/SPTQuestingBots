using System.Collections.Generic;
using System.Linq;
using EFT.Game.Spawning;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Integration;

/// <summary>
/// Discovers bot zones in the current map and converts them to advection sources
/// for the <see cref="Fields.AdvectionField"/>.
/// <para>
/// Since BSG's <c>BotZone</c> class has no public <c>GetAllZones()</c> API, this
/// class groups <c>SpawnPointParams</c> by their <c>BotZoneName</c> and uses each
/// group's centroid as the zone position. Zones with more spawn points receive
/// proportionally higher strength.
/// </para>
/// </summary>
public static class ZoneDiscovery
{
    /// <summary>
    /// Discovers zones from spawn point data by grouping spawn points by their
    /// <c>BotZoneName</c> and computing each group's centroid position.
    /// </summary>
    /// <param name="spawnPoints">
    /// All valid spawn points for the current map (from
    /// <c>LocationData.GetAllValidSpawnPointParams()</c>).
    /// </param>
    /// <returns>
    /// A list of (position, strength) tuples. Strength is normalized so the
    /// zone with the most spawn points has strength 1.0.
    /// </returns>
    public static List<(Vector3 position, float strength)> DiscoverZones(SpawnPointParams[] spawnPoints)
    {
        var zones = new List<(Vector3 position, float strength)>();

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            LoggingController.LogWarning("[ZoneMovement] No spawn points for zone discovery");
            return zones;
        }

        // Group spawn points by BotZoneName
        var groups = new Dictionary<string, List<Vector3>>();
        foreach (var sp in spawnPoints)
        {
            string zoneName = sp.BotZoneName ?? "";
            if (!groups.ContainsKey(zoneName))
            {
                groups[zoneName] = new List<Vector3>();
            }
            groups[zoneName].Add(sp.Position);
        }

        // Find the max group size for normalization
        int maxGroupSize = 0;
        foreach (var group in groups.Values)
        {
            if (group.Count > maxGroupSize)
                maxGroupSize = group.Count;
        }

        if (maxGroupSize == 0)
        {
            return zones;
        }

        // Create advection sources from zone centroids
        foreach (var kvp in groups)
        {
            Vector3 centroid = ComputeCentroid(kvp.Value);
            float strength = (float)kvp.Value.Count / maxGroupSize;

            zones.Add((centroid, strength));
        }

        LoggingController.LogInfo($"[ZoneMovement] Discovered {zones.Count} zones from {spawnPoints.Length} spawn points");

        return zones;
    }

    /// <summary>
    /// Computes the centroid (average position) of a list of positions.
    /// </summary>
    internal static Vector3 ComputeCentroid(List<Vector3> positions)
    {
        float x = 0,
            y = 0,
            z = 0;
        for (int i = 0; i < positions.Count; i++)
        {
            x += positions[i].x;
            y += positions[i].y;
            z += positions[i].z;
        }

        float inv = 1f / positions.Count;
        return new Vector3(x * inv, y * inv, z * inv);
    }
}
