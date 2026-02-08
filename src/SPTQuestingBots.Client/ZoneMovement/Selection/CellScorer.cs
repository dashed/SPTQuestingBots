using System;
using SPTQuestingBots.ZoneMovement.Core;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Selection;

/// <summary>
/// Scores candidate <see cref="GridCell"/> instances for destination selection.
/// The score combines directional alignment with the composite field direction
/// and a POI density bonus.
/// </summary>
/// <remarks>
/// <para>
/// <b>Angle factor</b>: Cells in the direction of the composite field vector score
/// higher (1.0 for perfect alignment, 0.0 for opposite direction). This uses the
/// dot product between the direction to the candidate cell and the composite direction.
/// </para>
/// <para>
/// <b>POI factor</b>: Cells with more/heavier points of interest score higher,
/// normalized against the grid's maximum density. This encourages bots to visit
/// content-rich areas (loot, quest triggers).
/// </para>
/// <para>
/// The final score is a weighted blend: <c>angleFactor * (1 - poiWeight) + poiFactor * poiWeight</c>.
/// </para>
/// </remarks>
public sealed class CellScorer
{
    private readonly float poiWeight;

    /// <summary>
    /// Creates a new cell scorer.
    /// </summary>
    /// <param name="poiWeight">
    /// Blend weight for POI density (0.0 = angle only, 1.0 = density only).
    /// Clamped to [0, 1]. Default is 0.3.
    /// </param>
    public CellScorer(float poiWeight = 0.3f)
    {
        this.poiWeight = Math.Max(0f, Math.Min(poiWeight, 1f));
    }

    /// <summary>
    /// Computes a score for a candidate cell based on directional alignment and POI density.
    /// </summary>
    /// <param name="candidate">The cell being evaluated.</param>
    /// <param name="compositeDirX">X component of the composite field direction (normalized).</param>
    /// <param name="compositeDirZ">Z component of the composite field direction (normalized).</param>
    /// <param name="fromPosition">The bot's current world-space position.</param>
    /// <param name="maxPoiDensity">
    /// Maximum POI density across the entire grid, used to normalize the density score.
    /// Pass 0 to skip density scoring.
    /// </param>
    /// <returns>
    /// A score in [0, 1] where higher values indicate better destinations.
    /// </returns>
    public float Score(
        GridCell candidate,
        float compositeDirX,
        float compositeDirZ,
        Vector3 fromPosition,
        float maxPoiDensity
    )
    {
        // Direction from current position to candidate cell center (XZ plane)
        float toDirX = candidate.Center.x - fromPosition.x;
        float toDirZ = candidate.Center.z - fromPosition.z;
        float toDirMag = (float)Math.Sqrt(toDirX * toDirX + toDirZ * toDirZ);

        float angleFactor;
        if (toDirMag < 0.001f || (Math.Abs(compositeDirX) < 0.001f && Math.Abs(compositeDirZ) < 0.001f))
        {
            // No meaningful direction — treat all cells equally
            angleFactor = 0.5f;
        }
        else
        {
            // Normalize direction to candidate
            float nx = toDirX / toDirMag;
            float nz = toDirZ / toDirMag;

            // Dot product: cosine of angle between composite direction and direction to candidate
            float dot = nx * compositeDirX + nz * compositeDirZ;
            dot = Math.Max(-1f, Math.Min(dot, 1f));

            // Map dot product: 1 (same direction) → 1.0, -1 (opposite) → 0.0
            angleFactor = (dot + 1f) * 0.5f;
        }

        // POI density factor: normalized against grid maximum
        float poiFactor = 0f;
        if (maxPoiDensity > 0.001f)
        {
            poiFactor = Math.Min(candidate.PoiDensity / maxPoiDensity, 1f);
        }

        return angleFactor * (1f - poiWeight) + poiFactor * poiWeight;
    }
}
