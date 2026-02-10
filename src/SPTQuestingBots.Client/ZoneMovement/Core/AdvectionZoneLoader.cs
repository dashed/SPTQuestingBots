using System;
using System.Collections.Generic;
using SPTQuestingBots.ZoneMovement.Fields;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Core;

/// <summary>
/// Resolves advection zone configs and injects bounded zones into the advection field.
/// Accepts pre-resolved builtin zone centroids (from <see cref="Integration.ZoneDiscovery"/>)
/// so that it can be tested without EFT/SpawnPointParams dependencies.
/// </summary>
public static class AdvectionZoneLoader
{
    /// <summary>
    /// Loads zone configs for a map and injects bounded zones into the advection field.
    /// </summary>
    /// <param name="field">The advection field to add bounded zones to.</param>
    /// <param name="builtinZoneCentroids">
    /// Pre-resolved BSG BotZone centroids keyed by zone name (from <see cref="Integration.ZoneDiscovery"/>).
    /// </param>
    /// <param name="mapId">BSG location ID (case-insensitive).</param>
    /// <param name="overrides">Per-map zone overrides from user config. May be null.</param>
    /// <param name="raidTimeNormalized">Raid progress from 0.0 (start) to 1.0 (end).</param>
    /// <param name="random">Random instance for force sampling. If null, a new instance is created.</param>
    /// <returns>Number of bounded zones injected.</returns>
    public static int LoadAndInjectZones(
        AdvectionField field,
        Dictionary<string, Vector3> builtinZoneCentroids,
        string mapId,
        Dictionary<string, AdvectionMapZones> overrides,
        float raidTimeNormalized,
        System.Random random = null
    )
    {
        if (field == null)
            return 0;

        var zones = AdvectionZoneConfig.GetForMap(mapId, overrides);
        if (zones.BuiltinZones.Count == 0 && zones.CustomZones.Count == 0)
            return 0;

        if (random == null)
            random = new System.Random();

        int injected = 0;

        // Resolve builtin zones: match config entries to discovered zone centroids
        if (builtinZoneCentroids != null)
        {
            foreach (var kvp in zones.BuiltinZones)
            {
                string zoneName = kvp.Key;
                var entry = kvp.Value;

                if (!builtinZoneCentroids.TryGetValue(zoneName, out var centroid))
                    continue;

                float force = SampleForce(entry, random);
                float timeMultiplier = ComputeTimeMultiplier(entry, raidTimeNormalized);
                float finalForce = force * timeMultiplier;

                field.AddBoundedZone(centroid, finalForce, entry.Radius, entry.Decay);
                injected++;
            }
        }

        // Inject custom zones at fixed positions
        foreach (var entry in zones.CustomZones)
        {
            var position = new Vector3(entry.X, 0f, entry.Z);
            float force = SampleForce(entry, random);
            float timeMultiplier = ComputeTimeMultiplier(entry, raidTimeNormalized);
            float finalForce = force * timeMultiplier;

            field.AddBoundedZone(position, finalForce, entry.Radius, entry.Decay);
            injected++;
        }

        return injected;
    }

    /// <summary>
    /// Samples a force value uniformly between ForceMin and ForceMax.
    /// </summary>
    internal static float SampleForce(AdvectionZoneEntry entry, System.Random random)
    {
        if (entry.ForceMin >= entry.ForceMax)
            return entry.ForceMin;

        float t = (float)random.NextDouble();
        return entry.ForceMin + (entry.ForceMax - entry.ForceMin) * t;
    }

    /// <summary>
    /// Computes a time-based multiplier by linearly interpolating between
    /// EarlyMultiplier (at raidTimeNormalized=0) and LateMultiplier (at raidTimeNormalized=1).
    /// </summary>
    internal static float ComputeTimeMultiplier(AdvectionZoneEntry entry, float raidTimeNormalized)
    {
        float t = Math.Max(0f, Math.Min(1f, raidTimeNormalized));
        return entry.EarlyMultiplier + (entry.LateMultiplier - entry.EarlyMultiplier) * t;
    }
}
