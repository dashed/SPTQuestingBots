using System;
using System.Collections.Generic;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Core;

/// <summary>
/// A 2D grid partitioning the map on the XZ plane into <see cref="GridCell"/> instances.
/// The grid auto-computes its cell size from the map dimensions and a target cell count,
/// eliminating the need for per-map configuration.
/// </summary>
/// <remarks>
/// <para>
/// Inspired by Phobos's <c>LocationSystem</c>, but with automatic bounds detection
/// rather than hardcoded per-map geometry. Map bounds are derived from spawn points
/// (via <c>MapBoundsDetector</c> in the integration layer).
/// </para>
/// <para>
/// After construction, call <see cref="AddPoi"/> to populate cells with points of
/// interest. The grid links cells to their 4-connected neighbors during construction.
/// </para>
/// </remarks>
public sealed class WorldGrid
{
    private readonly GridCell[,] cells;

    /// <summary>Number of columns (X axis).</summary>
    public int Cols { get; }

    /// <summary>Number of rows (Z axis).</summary>
    public int Rows { get; }

    /// <summary>Side length of each square cell in world units.</summary>
    public float CellSize { get; }

    /// <summary>Minimum corner of the grid bounds (world space).</summary>
    public Vector3 MinBounds { get; }

    /// <summary>Maximum corner of the grid bounds (world space).</summary>
    public Vector3 MaxBounds { get; }

    /// <summary>
    /// Creates a new world grid that auto-sizes its cells based on the map area
    /// and a target cell count.
    /// </summary>
    /// <param name="minBounds">Minimum corner (world space). Only X and Z are used.</param>
    /// <param name="maxBounds">Maximum corner (world space). Only X and Z are used.</param>
    /// <param name="targetCellCount">
    /// Desired number of cells. The actual count may differ slightly due to
    /// rounding. Default is 150, which provides good coverage for most maps.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="targetCellCount"/> is less than 1.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if max bounds are not greater than min bounds on X and Z axes.
    /// </exception>
    public WorldGrid(Vector3 minBounds, Vector3 maxBounds, int targetCellCount = 150)
    {
        if (targetCellCount < 1)
            throw new ArgumentOutOfRangeException(nameof(targetCellCount), "Must be at least 1");

        MinBounds = minBounds;
        MaxBounds = maxBounds;

        float width = maxBounds.x - minBounds.x;
        float depth = maxBounds.z - minBounds.z;

        if (width <= 0 || depth <= 0)
            throw new ArgumentException("MaxBounds must be greater than MinBounds on X and Z axes");

        // Auto-compute cell size: sqrt(area / target) gives roughly square cells
        float area = width * depth;
        CellSize = (float)Math.Sqrt(area / targetCellCount);
        if (CellSize < 1f)
            CellSize = 1f;

        Cols = Math.Max(1, (int)Math.Ceiling(width / CellSize));
        Rows = Math.Max(1, (int)Math.Ceiling(depth / CellSize));

        // Create all cells with their world-space centers
        cells = new GridCell[Cols, Rows];
        for (int col = 0; col < Cols; col++)
        {
            for (int row = 0; row < Rows; row++)
            {
                float cx = minBounds.x + (col + 0.5f) * CellSize;
                float cz = minBounds.z + (row + 0.5f) * CellSize;
                float cy = (minBounds.y + maxBounds.y) * 0.5f;
                cells[col, row] = new GridCell(col, row, new Vector3(cx, cy, cz));
            }
        }

        LinkNeighbors();
        int cellCount = Cols * Rows;
        LoggingController.LogInfo(
            "[WorldGrid] Created grid: " + Cols + "x" + Rows + " = " + cellCount + " cells, cellSize=" + CellSize.ToString("F1") + "m"
        );
    }

    /// <summary>
    /// Returns the cell at the given grid coordinates, or <c>null</c> if out of bounds.
    /// </summary>
    public GridCell GetCell(int col, int row)
    {
        if (col < 0 || col >= Cols || row < 0 || row >= Rows)
            return null;
        return cells[col, row];
    }

    /// <summary>
    /// Returns the cell containing the given world-space position.
    /// Positions outside the grid are clamped to the nearest edge cell.
    /// </summary>
    public GridCell GetCell(Vector3 position)
    {
        int col = (int)((position.x - MinBounds.x) / CellSize);
        int row = (int)((position.z - MinBounds.z) / CellSize);

        // Clamp to grid bounds
        col = Math.Max(0, Math.Min(col, Cols - 1));
        row = Math.Max(0, Math.Min(row, Rows - 1));

        return cells[col, row];
    }

    /// <summary>
    /// Adds a POI to the grid cell containing its position.
    /// </summary>
    public void AddPoi(PointOfInterest poi)
    {
        GridCell cell = GetCell(poi.Position);
        cell.AddPoi(poi);
    }

    /// <summary>
    /// Returns the maximum POI density across all cells. Used by
    /// <see cref="Selection.CellScorer"/> to normalize density scores.
    /// </summary>
    public float MaxPoiDensity
    {
        get
        {
            float max = 0f;
            for (int col = 0; col < Cols; col++)
            {
                for (int row = 0; row < Rows; row++)
                {
                    float d = cells[col, row].PoiDensity;
                    if (d > max)
                        max = d;
                }
            }
            return max;
        }
    }

    /// <summary>
    /// Links each cell to its 4-connected neighbors (left, right, up, down).
    /// Called once during construction.
    /// </summary>
    private void LinkNeighbors()
    {
        for (int col = 0; col < Cols; col++)
        {
            for (int row = 0; row < Rows; row++)
            {
                var list = new List<GridCell>(4);
                if (col > 0)
                    list.Add(cells[col - 1, row]);
                if (col < Cols - 1)
                    list.Add(cells[col + 1, row]);
                if (row > 0)
                    list.Add(cells[col, row - 1]);
                if (row < Rows - 1)
                    list.Add(cells[col, row + 1]);
                cells[col, row].SetNeighbors(list.ToArray());
            }
        }
    }
}
