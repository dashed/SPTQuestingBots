using System;
using System.Collections.Generic;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Fields;

/// <summary>
/// Computes a dynamic convergence vector that pulls bots toward human players.
/// The field is recomputed periodically (default: every 30 seconds) and cached
/// between updates to avoid per-frame recalculation.
/// </summary>
/// <remarks>
/// <para>
/// Inspired by Phobos's convergence field. Player attraction uses <c>1/sqrt(distance)</c>
/// falloff, which means nearby players attract strongly but the effect doesn't vanish
/// at long range — bots will still drift toward distant players, just more slowly.
/// </para>
/// <para>
/// The output is always a normalized 2D direction vector on the XZ plane (Y is ignored).
/// </para>
/// </remarks>
public sealed class ConvergenceField
{
    private readonly float updateInterval;
    private float lastUpdateTime = float.NegativeInfinity;
    private float cachedX;
    private float cachedZ;

    /// <summary>
    /// Creates a new convergence field with the specified update interval.
    /// </summary>
    /// <param name="updateIntervalSec">
    /// Minimum seconds between recomputations. Between updates, the cached
    /// direction is returned. Default is 30 seconds (matching Phobos).
    /// </param>
    public ConvergenceField(float updateIntervalSec = 30f)
    {
        updateInterval = updateIntervalSec;
    }

    /// <summary>
    /// Returns the convergence direction at a given position, using the cached
    /// value if the update interval hasn't elapsed.
    /// </summary>
    /// <param name="position">Query position (world space).</param>
    /// <param name="playerPositions">Current positions of human players.</param>
    /// <param name="currentTime">
    /// Current game time (e.g. <c>Time.time</c>). Used to check if the cache is stale.
    /// </param>
    /// <param name="outX">X component of the normalized convergence direction.</param>
    /// <param name="outZ">Z component of the normalized convergence direction.</param>
    public void GetConvergence(
        Vector3 position,
        IReadOnlyList<Vector3> playerPositions,
        float currentTime,
        out float outX,
        out float outZ
    )
    {
        if (currentTime - lastUpdateTime < updateInterval)
        {
            outX = cachedX;
            outZ = cachedZ;
            return;
        }

        ComputeConvergence(position, playerPositions, out outX, out outZ);
        cachedX = outX;
        cachedZ = outZ;
        lastUpdateTime = currentTime;
    }

    /// <summary>
    /// Computes the convergence direction without caching. Useful for testing
    /// or when a fresh computation is always desired.
    /// </summary>
    /// <param name="position">Query position (world space).</param>
    /// <param name="playerPositions">Current positions of human players.</param>
    /// <param name="outX">X component of the normalized convergence direction.</param>
    /// <param name="outZ">Z component of the normalized convergence direction.</param>
    public void ComputeConvergence(
        Vector3 position,
        IReadOnlyList<Vector3> playerPositions,
        out float outX,
        out float outZ
    )
    {
        float ax = 0f;
        float az = 0f;

        if (playerPositions != null)
        {
            for (int i = 0; i < playerPositions.Count; i++)
            {
                float dx = playerPositions[i].x - position.x;
                float dz = playerPositions[i].z - position.z;
                float distSq = dx * dx + dz * dz;
                if (distSq < 0.01f)
                    continue;
                float dist = (float)Math.Sqrt(distSq);
                // sqrt falloff: closer players attract more strongly
                float w = 1f / (float)Math.Sqrt(dist);
                ax += (dx / dist) * w;
                az += (dz / dist) * w;
            }
        }

        // Normalize to unit direction
        float mag = (float)Math.Sqrt(ax * ax + az * az);
        if (mag > 0.001f)
        {
            outX = ax / mag;
            outZ = az / mag;
        }
        else
        {
            outX = 0f;
            outZ = 0f;
        }
    }

    /// <summary>
    /// Forces the next call to <see cref="GetConvergence"/> to recompute
    /// rather than returning the cached value.
    /// </summary>
    public void InvalidateCache()
    {
        lastUpdateTime = float.NegativeInfinity;
    }
}
