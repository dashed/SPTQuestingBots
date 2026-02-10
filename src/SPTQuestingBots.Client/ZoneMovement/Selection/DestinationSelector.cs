using System;
using System.Collections.Generic;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.ZoneMovement.Core;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Selection;

/// <summary>
/// Selects the best neighboring <see cref="GridCell"/> as the bot's next movement
/// destination based on the composite field direction and cell scoring.
/// </summary>
/// <remarks>
/// <para>
/// The selector examines the 4-connected neighbors of the bot's current cell,
/// filters out non-navigable cells, scores each with <see cref="CellScorer"/>,
/// and returns the highest-scoring cell.
/// </para>
/// <para>
/// If no navigable neighbor is found, the current cell is returned (bot holds position).
/// This prevents bots from getting stuck when surrounded by impassable terrain.
/// </para>
/// </remarks>
public sealed class DestinationSelector
{
    private readonly CellScorer scorer;

    /// <summary>
    /// Creates a new destination selector with the given scorer.
    /// </summary>
    /// <param name="scorer">
    /// The <see cref="CellScorer"/> used to evaluate candidate cells.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="scorer"/> is null.
    /// </exception>
    public DestinationSelector(CellScorer scorer)
    {
        this.scorer = scorer ?? throw new ArgumentNullException(nameof(scorer));
    }

    /// <summary>
    /// Selects the best neighboring cell for the bot to move to.
    /// </summary>
    /// <param name="grid">The world grid (used to get max POI density for scoring).</param>
    /// <param name="currentCell">The cell the bot is currently in.</param>
    /// <param name="compositeDirX">X component of the composite field direction.</param>
    /// <param name="compositeDirZ">Z component of the composite field direction.</param>
    /// <param name="botPosition">The bot's current world-space position.</param>
    /// <returns>
    /// The best neighboring cell, or <paramref name="currentCell"/> if no navigable
    /// neighbor exists.
    /// </returns>
    public GridCell SelectDestination(WorldGrid grid, GridCell currentCell, float compositeDirX, float compositeDirZ, Vector3 botPosition)
    {
        IReadOnlyList<GridCell> neighbors = currentCell.Neighbors;
        if (neighbors.Count == 0)
            return currentCell;

        float maxDensity = grid.MaxPoiDensity;

        GridCell best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < neighbors.Count; i++)
        {
            GridCell candidate = neighbors[i];
            if (!candidate.IsNavigable)
                continue;

            float score = scorer.Score(candidate, compositeDirX, compositeDirZ, botPosition, maxDensity);

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        // Fall back to current cell if no navigable neighbor found
        if (best == null)
        {
            LoggingController.LogDebug(
                "[DestinationSelector] No navigable neighbor from (" + currentCell.Col + "," + currentCell.Row + "), holding position"
            );
            return currentCell;
        }

        LoggingController.LogDebug(
            "[DestinationSelector] Selected ("
                + best.Col
                + ","
                + best.Row
                + ") from ("
                + currentCell.Col
                + ","
                + currentCell.Row
                + ") score="
                + bestScore.ToString("F2")
        );
        return best;
    }
}
