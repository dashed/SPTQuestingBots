using System;
using System.Collections.Generic;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Core;

/// <summary>
/// Pure-logic math utilities for the zone movement system.
/// Extracted from integration classes to enable unit testing without Unity/EFT dependencies.
/// </summary>
public static class ZoneMathUtils
{
    /// <summary>
    /// Computes the normalized XZ-plane momentum direction from <paramref name="from"/> to
    /// <paramref name="to"/>. The Y axis is ignored since bots move on the horizontal plane.
    /// </summary>
    /// <param name="from">The origin position (e.g. previous destination).</param>
    /// <param name="to">The target position (e.g. current bot position).</param>
    /// <returns>
    /// A normalized (momX, momZ) tuple, or (0, 0) if the positions are coincident.
    /// </returns>
    public static (float momX, float momZ) ComputeMomentum(Vector3 from, Vector3 to)
    {
        float dx = to.x - from.x;
        float dz = to.z - from.z;
        float mag = (float)Math.Sqrt(dx * dx + dz * dz);
        if (mag < 0.001f)
            return (0f, 0f);
        return (dx / mag, dz / mag);
    }

    /// <summary>
    /// Determines the dominant POI category in a cell by finding the category
    /// with the highest total weight.
    /// </summary>
    /// <param name="cell">The grid cell to analyze.</param>
    /// <returns>
    /// The POI category with the highest total weight, or <see cref="PoiCategory.Synthetic"/>
    /// if the cell has no POIs.
    /// </returns>
    public static PoiCategory GetDominantCategory(GridCell cell)
    {
        if (cell.POIs.Count == 0)
            return PoiCategory.Synthetic;

        // Accumulate weights per category
        float containerWeight = 0f;
        float looseLootWeight = 0f;
        float questWeight = 0f;
        float exfilWeight = 0f;
        float spawnPointWeight = 0f;
        float syntheticWeight = 0f;

        for (int i = 0; i < cell.POIs.Count; i++)
        {
            var poi = cell.POIs[i];
            switch (poi.Category)
            {
                case PoiCategory.Container:
                    containerWeight += poi.Weight;
                    break;
                case PoiCategory.LooseLoot:
                    looseLootWeight += poi.Weight;
                    break;
                case PoiCategory.Quest:
                    questWeight += poi.Weight;
                    break;
                case PoiCategory.Exfil:
                    exfilWeight += poi.Weight;
                    break;
                case PoiCategory.SpawnPoint:
                    spawnPointWeight += poi.Weight;
                    break;
                case PoiCategory.Synthetic:
                    syntheticWeight += poi.Weight;
                    break;
            }
        }

        // Find the category with highest total weight
        PoiCategory best = PoiCategory.Synthetic;
        float bestWeight = syntheticWeight;

        if (containerWeight > bestWeight)
        {
            best = PoiCategory.Container;
            bestWeight = containerWeight;
        }
        if (looseLootWeight > bestWeight)
        {
            best = PoiCategory.LooseLoot;
            bestWeight = looseLootWeight;
        }
        if (questWeight > bestWeight)
        {
            best = PoiCategory.Quest;
            bestWeight = questWeight;
        }
        if (exfilWeight > bestWeight)
        {
            best = PoiCategory.Exfil;
            bestWeight = exfilWeight;
        }
        if (spawnPointWeight > bestWeight)
        {
            best = PoiCategory.SpawnPoint;
            bestWeight = spawnPointWeight;
        }

        return best;
    }

    /// <summary>
    /// Computes the centroid (average position) of a list of positions.
    /// </summary>
    /// <param name="positions">Non-empty list of world-space positions.</param>
    /// <returns>The average position across all input points.</returns>
    public static Vector3 ComputeCentroid(List<Vector3> positions)
    {
        if (positions == null || positions.Count == 0)
        {
            LoggingController.LogError("[ZoneMathUtils] ComputeCentroid called with null or empty positions list");
            return default;
        }

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
