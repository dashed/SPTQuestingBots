using System;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Integration;

/// <summary>
/// Detects map bounds from a collection of positions (typically spawn points).
/// The resulting axis-aligned bounding box on the XZ plane defines the area
/// covered by the <see cref="Core.WorldGrid"/>.
/// <para>
/// This class operates on raw <see cref="Vector3"/> arrays and has no Unity scene
/// or EFT dependencies, making it fully unit-testable with the Vector3 test shim.
/// </para>
/// </summary>
public static class MapBoundsDetector
{
    /// <summary>
    /// Computes the axis-aligned bounding box on the XZ plane that encloses
    /// all given positions, expanded by <paramref name="padding"/> on each edge.
    /// </summary>
    /// <param name="positions">
    /// Array of world-space positions (e.g. from <c>SpawnPointParams</c>).
    /// Must contain at least one element.
    /// </param>
    /// <param name="padding">
    /// Distance (in meters) to expand the bounds beyond the outermost positions.
    /// Prevents bots from being placed exactly at grid edges. Default: 50 m.
    /// </param>
    /// <returns>
    /// A tuple of (min, max) <see cref="Vector3"/> values defining the bounding box.
    /// The Y components are set to <c>-10000</c> (min) and <c>10000</c> (max) to
    /// encompass all vertical positions, since the grid operates on the XZ plane.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="positions"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="positions"/> is empty.
    /// </exception>
    public static (Vector3 min, Vector3 max) DetectBounds(Vector3[] positions, float padding = 50f)
    {
        if (positions == null)
        {
            throw new ArgumentNullException(nameof(positions));
        }

        if (positions.Length == 0)
        {
            throw new ArgumentException("At least one position is required.", nameof(positions));
        }

        float minX = positions[0].x;
        float maxX = positions[0].x;
        float minZ = positions[0].z;
        float maxZ = positions[0].z;

        for (int i = 1; i < positions.Length; i++)
        {
            float x = positions[i].x;
            float z = positions[i].z;

            if (x < minX)
            {
                minX = x;
            }

            if (x > maxX)
            {
                maxX = x;
            }

            if (z < minZ)
            {
                minZ = z;
            }

            if (z > maxZ)
            {
                maxZ = z;
            }
        }

        var result = (new Vector3(minX - padding, -10000f, minZ - padding), new Vector3(maxX + padding, 10000f, maxZ + padding));
        LoggingController.LogInfo(
            "[MapBoundsDetector] Detected bounds from "
                + positions.Length
                + " spawn points: min=("
                + result.Item1.x.ToString("F0")
                + ","
                + result.Item1.z.ToString("F0")
                + ") max=("
                + result.Item2.x.ToString("F0")
                + ","
                + result.Item2.z.ToString("F0")
                + ") padding="
                + padding.ToString("F0")
        );
        return result;
    }
}
