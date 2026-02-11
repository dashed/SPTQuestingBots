using System;
using System.Collections.Generic;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Fields;

/// <summary>
/// Computes a static advection vector that pushes bots toward interesting geographic
/// zones while repelling them from crowds of other bots.
/// </summary>
/// <remarks>
/// <para>
/// Inspired by Phobos's advection field. Zone sources (e.g. BSG BotZone patrol points)
/// attract bots with strength weighted by <c>1/distance</c>. Other bot positions generate
/// repulsion with inverse-square falloff (<c>strength/distance²</c>) to prevent clustering.
/// </para>
/// <para>
/// The output is always a normalized 2D direction vector on the XZ plane (Y is ignored).
/// </para>
/// </remarks>
public sealed class AdvectionField
{
    private readonly List<ZoneSource> zoneSources = new List<ZoneSource>();
    private readonly List<BoundedZoneSource> boundedZoneSources = new List<BoundedZoneSource>();
    private float crowdRepulsionStrength;

    /// <summary>
    /// Creates a new advection field with the specified crowd repulsion strength.
    /// </summary>
    /// <param name="crowdRepulsionStrength">
    /// Multiplier for crowd repulsion force. Higher values push bots apart more aggressively.
    /// </param>
    public AdvectionField(float crowdRepulsionStrength = 2.0f)
    {
        this.crowdRepulsionStrength = crowdRepulsionStrength;
    }

    /// <summary>
    /// Registers a geographic zone as an attraction source.
    /// </summary>
    /// <param name="position">World-space position of the zone (only X and Z are used).</param>
    /// <param name="strength">Attraction strength. Larger zones or more important areas should use higher values.</param>
    public void AddZone(Vector3 position, float strength)
    {
        zoneSources.Add(new ZoneSource(position, strength));
        LoggingController.LogDebug(
            "[AdvectionField] Added zone at ("
                + position.x.ToString("F0")
                + ","
                + position.z.ToString("F0")
                + ") strength="
                + strength.ToString("F2")
                + " total="
                + zoneSources.Count
        );
    }

    /// <summary>Number of registered simple zone sources.</summary>
    public int ZoneCount
    {
        get { return zoneSources.Count; }
    }

    /// <summary>Number of registered bounded zone sources.</summary>
    public int BoundedZoneCount
    {
        get { return boundedZoneSources.Count; }
    }

    /// <summary>
    /// Registers a bounded geographic zone with radius-limited falloff.
    /// Force = strength * pow(clamp01(1 - dist/radius), decay).
    /// </summary>
    /// <param name="position">World-space position of the zone (only X and Z are used).</param>
    /// <param name="strength">Peak attraction/repulsion strength at the zone center.</param>
    /// <param name="radius">Maximum effective radius in meters. No effect beyond this distance.</param>
    /// <param name="decay">Falloff exponent. 1.0 = linear, &lt;1 = soft, &gt;1 = sharp edge.</param>
    public void AddBoundedZone(Vector3 position, float strength, float radius, float decay)
    {
        boundedZoneSources.Add(new BoundedZoneSource(position, strength, radius, decay));
        LoggingController.LogDebug(
            "[AdvectionField] Added bounded zone at ("
                + position.x.ToString("F0")
                + ","
                + position.z.ToString("F0")
                + ") strength="
                + strength.ToString("F2")
                + " radius="
                + radius.ToString("F0")
                + " decay="
                + decay.ToString("F2")
                + " total="
                + boundedZoneSources.Count
        );
    }

    /// <summary>
    /// Computes the normalized advection direction at a given position.
    /// </summary>
    /// <param name="position">Query position (world space).</param>
    /// <param name="botPositions">
    /// Positions of other bots for crowd repulsion. Pass <c>null</c> or empty to skip repulsion.
    /// </param>
    /// <param name="outX">X component of the normalized advection direction.</param>
    /// <param name="outZ">Z component of the normalized advection direction.</param>
    public void GetAdvection(Vector3 position, IReadOnlyList<Vector3> botPositions, out float outX, out float outZ)
    {
        float ax = 0f;
        float az = 0f;

        // Zone attraction: direction toward each zone, weighted by strength / distance
        for (int i = 0; i < zoneSources.Count; i++)
        {
            float dx = zoneSources[i].Position.x - position.x;
            float dz = zoneSources[i].Position.z - position.z;
            float distSq = dx * dx + dz * dz;
            if (distSq < 0.01f)
            {
                continue;
            }

            float dist = (float)Math.Sqrt(distSq);
            float w = zoneSources[i].Strength / dist;
            ax += (dx / dist) * w;
            az += (dz / dist) * w;
        }

        // Bounded zone attraction: pow(clamp01(1 - dist/radius), decay) * strength
        for (int i = 0; i < boundedZoneSources.Count; i++)
        {
            float dx = boundedZoneSources[i].Position.x - position.x;
            float dz = boundedZoneSources[i].Position.z - position.z;
            float distSq = dx * dx + dz * dz;
            if (distSq < 0.01f)
            {
                continue;
            }

            float dist = (float)Math.Sqrt(distSq);
            float radius = boundedZoneSources[i].Radius;
            if (dist >= radius)
            {
                continue;
            }

            float normalized = 1f - dist / radius;
            float falloff = (float)Math.Pow(normalized, boundedZoneSources[i].Decay);
            float w = boundedZoneSources[i].Strength * falloff;
            ax += (dx / dist) * w;
            az += (dz / dist) * w;
        }

        // Crowd repulsion: direction away from each bot, inverse-square falloff
        if (botPositions != null)
        {
            for (int i = 0; i < botPositions.Count; i++)
            {
                float dx = position.x - botPositions[i].x;
                float dz = position.z - botPositions[i].z;
                float distSq = dx * dx + dz * dz;
                if (distSq < 0.01f)
                {
                    continue;
                }

                float dist = (float)Math.Sqrt(distSq);
                float w = crowdRepulsionStrength / distSq;
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
        LoggingController.LogDebug(
            "[AdvectionField] Computed at ("
                + position.x.ToString("F0")
                + ","
                + position.z.ToString("F0")
                + "): dir=("
                + outX.ToString("F2")
                + ","
                + outZ.ToString("F2")
                + ") zones="
                + zoneSources.Count
                + " bounded="
                + boundedZoneSources.Count
                + " bots="
                + (botPositions?.Count ?? 0)
        );
    }

    /// <summary>
    /// Internal data for a zone attraction source.
    /// </summary>
    private readonly struct ZoneSource
    {
        public readonly Vector3 Position;
        public readonly float Strength;

        public ZoneSource(Vector3 position, float strength)
        {
            Position = position;
            Strength = strength;
        }
    }

    /// <summary>
    /// Internal data for a bounded zone with radius-limited falloff.
    /// </summary>
    private readonly struct BoundedZoneSource
    {
        public readonly Vector3 Position;
        public readonly float Strength;
        public readonly float Radius;
        public readonly float Decay;

        public BoundedZoneSource(Vector3 position, float strength, float radius, float decay)
        {
            Position = position;
            Strength = strength;
            Radius = radius;
            Decay = decay;
        }
    }
}
