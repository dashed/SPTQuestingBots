using System;
using System.Collections.Generic;
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
    }

    /// <summary>Number of registered zone sources.</summary>
    public int ZoneCount => zoneSources.Count;

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
                continue;
            float dist = (float)Math.Sqrt(distSq);
            float w = zoneSources[i].Strength / dist;
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
                    continue;
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
}
