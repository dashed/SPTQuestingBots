using System.Collections.Generic;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Core;

/// <summary>
/// A single cell in the <see cref="WorldGrid"/>. Each cell covers a square region
/// on the XZ plane and holds zero or more <see cref="PointOfInterest"/> objects
/// discovered during scene scanning.
/// </summary>
/// <remarks>
/// <para>
/// Bots move cell-to-cell: the <see cref="Selection.DestinationSelector"/> picks the
/// best <see cref="Neighbors">neighbor</see> based on the composite field direction
/// and POI density.
/// </para>
/// <para>
/// A cell is considered <see cref="IsNavigable"/> only if it contains at least one
/// POI (which implies a NavMesh-valid position exists within the cell).
/// </para>
/// </remarks>
public sealed class GridCell
{
    private readonly List<PointOfInterest> pois = new List<PointOfInterest>();
    private GridCell[] neighbors = System.Array.Empty<GridCell>();

    /// <summary>Column index (X axis) in the parent grid.</summary>
    public int Col { get; }

    /// <summary>Row index (Z axis) in the parent grid.</summary>
    public int Row { get; }

    /// <summary>World-space center of this cell (Y is averaged from grid bounds).</summary>
    public Vector3 Center { get; }

    /// <summary>Points of interest contained within this cell.</summary>
    public IReadOnlyList<PointOfInterest> POIs => pois;

    /// <summary>
    /// Adjacent cells (4-connected: left, right, up, down).
    /// Set by the parent <see cref="WorldGrid"/> during construction.
    /// </summary>
    public IReadOnlyList<GridCell> Neighbors => neighbors;

    /// <summary>
    /// Sum of all POI weights in this cell. Used by <see cref="Selection.CellScorer"/>
    /// to give a density bonus when scoring candidate destinations.
    /// </summary>
    public float PoiDensity
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < pois.Count; i++)
                sum += pois[i].Weight;
            return sum;
        }
    }

    /// <summary>
    /// Whether this cell has at least one POI, indicating a NavMesh-valid
    /// position exists here and bots can navigate to it.
    /// </summary>
    public bool IsNavigable => pois.Count > 0;

    /// <summary>
    /// Creates a new grid cell at the specified grid coordinates and world-space center.
    /// </summary>
    /// <param name="col">Column index (X axis).</param>
    /// <param name="row">Row index (Z axis).</param>
    /// <param name="center">World-space center position.</param>
    public GridCell(int col, int row, Vector3 center)
    {
        Col = col;
        Row = row;
        Center = center;
    }

    /// <summary>Adds a point of interest to this cell.</summary>
    public void AddPoi(PointOfInterest poi)
    {
        pois.Add(poi);
        LoggingController.LogDebug(
            "[GridCell] ("
                + Col
                + ","
                + Row
                + ") registered POI category="
                + poi.Category
                + " weight="
                + poi.Weight.ToString("F1")
                + " total="
                + pois.Count
        );
    }

    /// <summary>
    /// Sets the neighbor array. Called internally by <see cref="WorldGrid"/>
    /// after all cells are created.
    /// </summary>
    internal void SetNeighbors(GridCell[] cells)
    {
        neighbors = cells;
    }
}
