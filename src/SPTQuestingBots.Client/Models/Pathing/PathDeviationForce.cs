using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SPTQuestingBots.Models.Pathing;

/// <summary>
/// Pure-logic path-deviation spring force calculator.
/// Computes a correction vector that pulls a bot back toward its path segment
/// when it drifts off due to physics, terrain, or collisions.
///
/// All calculations are in the XZ plane to avoid terrain height distortion.
///
/// Inspired by Phobos's path-deviation spring force in MovementSystem.
/// </summary>
public static class PathDeviationForce
{
    /// <summary>
    /// Computes the deviation vector from the bot's current position toward the
    /// nearest point on the path segment [segStart, segEnd], projected onto the XZ plane.
    ///
    /// Returns a Vector3 with Y=0. The magnitude indicates how far off-path the bot is.
    /// A zero vector means the bot is exactly on the path line.
    /// </summary>
    /// <param name="botPosition">Current bot position (3D).</param>
    /// <param name="segStart">Start of the current path segment (3D).</param>
    /// <param name="segEnd">End of the current path segment (3D).</param>
    /// <returns>Deviation vector in XZ plane (Y=0), pointing from bot toward path.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ComputeDeviation(Vector3 botPosition, Vector3 segStart, Vector3 segEnd)
    {
        // Project everything to XZ (2D)
        float bx = botPosition.x;
        float bz = botPosition.z;
        float sx = segStart.x;
        float sz = segStart.z;
        float ex = segEnd.x;
        float ez = segEnd.z;

        float dx = ex - sx;
        float dz = ez - sz;
        float segLenSqr = dx * dx + dz * dz;

        // Degenerate segment (start == end): deviation is vector from bot to start
        if (segLenSqr < 1e-12f)
        {
            return new Vector3(sx - bx, 0f, sz - bz);
        }

        // Project bot onto the line defined by segStart → segEnd via dot product.
        // t = dot(bot - segStart, segEnd - segStart) / |segEnd - segStart|^2
        // t is clamped to [0,1] to stay within the segment.
        float t = ((bx - sx) * dx + (bz - sz) * dz) / segLenSqr;
        t = Math.Max(0f, Math.Min(1f, t));

        // Closest point on segment
        float cx = sx + t * dx;
        float cz = sz + t * dz;

        // Deviation: from bot toward closest point
        return new Vector3(cx - bx, 0f, cz - bz);
    }

    /// <summary>
    /// Blends a move direction with a deviation force and returns a normalized result.
    /// The deviation force is scaled by the given strength before blending.
    ///
    /// Returns the original direction if the blended result is degenerate.
    /// </summary>
    /// <param name="moveDirection">Primary movement direction (XZ plane).</param>
    /// <param name="deviation">Deviation vector from ComputeDeviation.</param>
    /// <param name="strength">How strongly to apply the deviation (0=ignore, 1=full).</param>
    /// <returns>Normalized blended direction in XZ plane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 BlendWithDeviation(Vector3 moveDirection, Vector3 deviation, float strength = 1f)
    {
        float bx = moveDirection.x + deviation.x * strength;
        float bz = moveDirection.z + deviation.z * strength;
        float len = (float)Math.Sqrt(bx * bx + bz * bz);

        if (len < 1e-6f)
            return moveDirection;

        return new Vector3(bx / len, 0f, bz / len);
    }
}
