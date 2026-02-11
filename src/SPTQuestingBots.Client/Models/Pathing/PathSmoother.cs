using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Models.Pathing;

/// <summary>
/// Pure-logic path smoothing via Chaikin corner-cutting subdivision.
/// Reduces jerky corner-to-corner movement by replacing sharp corners with
/// gentle curves while preserving the start and end points.
///
/// All operations work on Vector3 arrays with no Unity dependencies beyond the
/// Vector3 struct (compatible with test shim).
/// </summary>
public static class PathSmoother
{
    /// <summary>
    /// Applies Chaikin corner-cutting to smooth a path.
    /// Each iteration replaces interior corners with two new points at 25% and 75%
    /// along each segment, producing a progressively smoother curve.
    ///
    /// Start and end points are always preserved exactly.
    /// </summary>
    /// <param name="corners">Original path corners.</param>
    /// <param name="iterations">Number of smoothing iterations (0 = no change).</param>
    /// <returns>Smoothed path corners, or the original if smoothing is not applicable.</returns>
    public static Vector3[] ChaikinSmooth(Vector3[] corners, int iterations)
    {
        if (corners == null || corners.Length < 3 || iterations <= 0)
        {
            return corners;
        }

        var current = corners;

        for (int iter = 0; iter < iterations; iter++)
        {
            // Each iteration: for N points, produce 2*(N-1) interior points + 2 endpoints
            int n = current.Length;
            var next = new List<Vector3>((n - 1) * 2 + 2);

            // Preserve start point
            next.Add(current[0]);

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 a = current[i];
                Vector3 b = current[i + 1];

                // Q = 0.75*A + 0.25*B (25% from A toward B)
                // R = 0.25*A + 0.75*B (75% from A toward B)
                Vector3 q = new Vector3(0.75f * a.x + 0.25f * b.x, 0.75f * a.y + 0.25f * b.y, 0.75f * a.z + 0.25f * b.z);
                Vector3 r = new Vector3(0.25f * a.x + 0.75f * b.x, 0.25f * a.y + 0.75f * b.y, 0.25f * a.z + 0.75f * b.z);

                // Skip Q for the first segment (start is already preserved)
                if (i > 0)
                {
                    next.Add(q);
                }

                // Skip R for the last segment (end will be preserved)
                if (i < n - 2)
                {
                    next.Add(r);
                }
            }

            // Preserve end point
            next.Add(current[n - 1]);

            current = next.ToArray();
        }

        return current;
    }

    /// <summary>
    /// Inserts intermediate points on segments longer than <paramref name="minSegmentLength"/>.
    /// This prevents BSG's mover from making excessively long straight-line moves
    /// that feel robotic.
    /// </summary>
    /// <param name="corners">Path corners.</param>
    /// <param name="minSegmentLength">Minimum segment length before a midpoint is inserted.</param>
    /// <returns>Path with intermediate points added on long segments.</returns>
    public static Vector3[] InsertIntermediatePoints(Vector3[] corners, float minSegmentLength)
    {
        if (corners == null || corners.Length < 2 || minSegmentLength <= 0f)
        {
            return corners;
        }

        var result = new List<Vector3>(corners.Length * 2) { corners[0] };

        for (int i = 0; i < corners.Length - 1; i++)
        {
            Vector3 a = corners[i];
            Vector3 b = corners[i + 1];

            float dx = b.x - a.x;
            float dy = b.y - a.y;
            float dz = b.z - a.z;
            float segLen = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (segLen > minSegmentLength)
            {
                // Insert midpoints to bring each sub-segment near minSegmentLength
                int subdivisions = (int)Math.Ceiling(segLen / minSegmentLength);
                for (int s = 1; s < subdivisions; s++)
                {
                    float t = (float)s / subdivisions;
                    result.Add(new Vector3(a.x + t * dx, a.y + t * dy, a.z + t * dz));
                }
            }

            result.Add(b);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Full smoothing pipeline: insert intermediate points on long segments,
    /// then apply Chaikin corner-cutting.
    /// </summary>
    /// <param name="corners">Original path corners.</param>
    /// <param name="config">Movement configuration with smoothing parameters.</param>
    /// <returns>Smoothed path corners.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3[] Smooth(Vector3[] corners, CustomMoverConfig config)
    {
        var withIntermediates = InsertIntermediatePoints(corners, config.MinSegmentLength);
        var result = ChaikinSmooth(withIntermediates, config.SmoothingIterations);
        LoggingController.LogDebug("[PathSmoother] Smoothed path: " + (corners?.Length ?? 0) + " corners -> " + (result?.Length ?? 0));
        return result;
    }
}
