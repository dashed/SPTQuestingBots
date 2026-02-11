using System;
using System.Collections.Generic;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.ZoneMovement.Core;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Fields;

/// <summary>
/// Computes a dynamic convergence vector that pulls bots toward activity hotspots.
/// The field is recomputed periodically (default: every 30 seconds) and cached
/// between updates to avoid per-frame recalculation.
/// </summary>
/// <remarks>
/// <para>
/// Position-based attraction (bot clustering) uses <c>1/distance</c> falloff for local
/// grouping without cross-map tracking. Combat event attraction uses <c>1/sqrt(distance)</c>
/// for longer-range response to gunfire and explosions.
/// </para>
/// <para>
/// The output is always a normalized 2D direction vector on the XZ plane (Y is ignored).
/// </para>
/// <para>
/// Per-map tuning: radius limits how far the field reaches, force scales the pull strength.
/// Combat events from <see cref="CombatPullPoint"/> array add temporary convergence toward
/// active firefights with linear time decay.
/// </para>
/// </remarks>
public sealed class ConvergenceField
{
    private readonly float updateInterval;
    private readonly float radius;
    private readonly float radiusSq;
    private readonly float force;
    private float lastUpdateTime = float.NegativeInfinity;
    private float cachedX;
    private float cachedZ;

    /// <summary>
    /// Creates a new convergence field with the specified parameters.
    /// </summary>
    /// <param name="updateIntervalSec">
    /// Minimum seconds between recomputations. Between updates, the cached
    /// direction is returned. Default is 30 seconds (matching Phobos).
    /// </param>
    /// <param name="radius">
    /// Maximum distance (meters) at which attraction sources affect bots. Sources beyond
    /// this distance are ignored. Default is <c>float.MaxValue</c> (no limit).
    /// </param>
    /// <param name="force">
    /// Multiplier applied to all attraction forces. Default is 1.0.
    /// </param>
    public ConvergenceField(float updateIntervalSec = 30f, float radius = float.MaxValue, float force = 1.0f)
    {
        updateInterval = updateIntervalSec;
        this.radius = radius;
        this.radiusSq = radius < float.MaxValue / 2f ? radius * radius : float.MaxValue;
        this.force = force;
    }

    /// <summary>
    /// Returns the convergence direction at a given position, using the cached
    /// value if the update interval hasn't elapsed.
    /// </summary>
    /// <param name="position">Query position (world space).</param>
    /// <param name="attractionPositions">Positions to attract toward (e.g. bot positions for clustering).</param>
    /// <param name="currentTime">
    /// Current game time (e.g. <c>Time.time</c>). Used to check if the cache is stale.
    /// </param>
    /// <param name="outX">X component of the normalized convergence direction.</param>
    /// <param name="outZ">Z component of the normalized convergence direction.</param>
    public void GetConvergence(
        Vector3 position,
        IReadOnlyList<Vector3> attractionPositions,
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

        ComputeConvergence(position, attractionPositions, out outX, out outZ);
        cachedX = outX;
        cachedZ = outZ;
        lastUpdateTime = currentTime;
        LoggingController.LogDebug(
            "[ConvergenceField] Recomputed at t="
                + currentTime.ToString("F1")
                + " sources="
                + (attractionPositions?.Count ?? 0)
                + " dir=("
                + outX.ToString("F2")
                + ","
                + outZ.ToString("F2")
                + ")"
        );
    }

    /// <summary>
    /// Returns the convergence direction at a given position, including combat event pull.
    /// Uses the cached value if the update interval hasn't elapsed.
    /// </summary>
    /// <param name="position">Query position (world space).</param>
    /// <param name="attractionPositions">Positions to attract toward (e.g. bot positions for clustering).</param>
    /// <param name="combatPull">Array of pre-computed combat pull points.</param>
    /// <param name="combatPullCount">Number of valid entries in <paramref name="combatPull"/>.</param>
    /// <param name="currentTime">
    /// Current game time (e.g. <c>Time.time</c>). Used to check if the cache is stale.
    /// </param>
    /// <param name="outX">X component of the normalized convergence direction.</param>
    /// <param name="outZ">Z component of the normalized convergence direction.</param>
    public void GetConvergence(
        Vector3 position,
        IReadOnlyList<Vector3> attractionPositions,
        CombatPullPoint[] combatPull,
        int combatPullCount,
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

        ComputeConvergence(position, attractionPositions, combatPull, combatPullCount, out outX, out outZ);
        cachedX = outX;
        cachedZ = outZ;
        lastUpdateTime = currentTime;
        LoggingController.LogDebug(
            "[ConvergenceField] Recomputed at t="
                + currentTime.ToString("F1")
                + " sources="
                + (attractionPositions?.Count ?? 0)
                + " combat="
                + combatPullCount
                + " dir=("
                + outX.ToString("F2")
                + ","
                + outZ.ToString("F2")
                + ")"
        );
    }

    /// <summary>
    /// Computes the convergence direction without caching. Useful for testing
    /// or when a fresh computation is always desired.
    /// </summary>
    /// <param name="position">Query position (world space).</param>
    /// <param name="attractionPositions">Positions to attract toward (e.g. bot positions).</param>
    /// <param name="outX">X component of the normalized convergence direction.</param>
    /// <param name="outZ">Z component of the normalized convergence direction.</param>
    public void ComputeConvergence(Vector3 position, IReadOnlyList<Vector3> attractionPositions, out float outX, out float outZ)
    {
        ComputeConvergence(position, attractionPositions, null, 0, out outX, out outZ);
    }

    /// <summary>
    /// Computes the convergence direction with combat event pull, without caching.
    /// </summary>
    /// <param name="position">Query position (world space).</param>
    /// <param name="attractionPositions">Positions to attract toward (e.g. bot positions).</param>
    /// <param name="combatPull">Array of pre-computed combat pull points (may be null).</param>
    /// <param name="combatPullCount">Number of valid entries in <paramref name="combatPull"/>.</param>
    /// <param name="outX">X component of the normalized convergence direction.</param>
    /// <param name="outZ">Z component of the normalized convergence direction.</param>
    public void ComputeConvergence(
        Vector3 position,
        IReadOnlyList<Vector3> attractionPositions,
        CombatPullPoint[] combatPull,
        int combatPullCount,
        out float outX,
        out float outZ
    )
    {
        float ax = 0f;
        float az = 0f;

        // Position-based attraction: 1/dist falloff for local clustering
        // (steeper than 1/sqrt — prevents cross-map tracking, only nearby grouping)
        if (attractionPositions != null)
        {
            for (int i = 0; i < attractionPositions.Count; i++)
            {
                float dx = attractionPositions[i].x - position.x;
                float dz = attractionPositions[i].z - position.z;
                float distSq = dx * dx + dz * dz;
                if (distSq < 0.01f)
                {
                    continue;
                }

                if (distSq > radiusSq)
                {
                    continue;
                }

                float dist = (float)Math.Sqrt(distSq);
                // 1/dist falloff: local clustering, fast decay with distance
                float w = force / dist;
                ax += (dx / dist) * w;
                az += (dz / dist) * w;
            }
        }

        // Combat event pull: 1/sqrt(dist) falloff for longer-range response to gunfire
        if (combatPull != null)
        {
            for (int i = 0; i < combatPullCount; i++)
            {
                float dx = combatPull[i].X - position.x;
                float dz = combatPull[i].Z - position.z;
                float distSq = dx * dx + dz * dz;
                if (distSq < 0.01f)
                {
                    continue;
                }

                if (distSq > radiusSq)
                {
                    continue;
                }

                float dist = (float)Math.Sqrt(distSq);
                // 1/sqrt(dist) falloff: gunfire attracts from further away than clustering
                float w = (force * combatPull[i].Strength) / (float)Math.Sqrt(dist);
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
    /// Forces the next call to <see cref="GetConvergence(Vector3, IReadOnlyList{Vector3}, float, out float, out float)"/>
    /// to recompute rather than returning the cached value.
    /// </summary>
    public void InvalidateCache()
    {
        lastUpdateTime = float.NegativeInfinity;
        LoggingController.LogDebug("[ConvergenceField] Cache invalidated");
    }
}
