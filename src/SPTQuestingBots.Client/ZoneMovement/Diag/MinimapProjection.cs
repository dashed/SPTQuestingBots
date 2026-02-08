using SPTQuestingBots.ZoneMovement.Core;

namespace SPTQuestingBots.ZoneMovement.Diag;

/// <summary>
/// Pure-logic helpers for 2D minimap rendering. Maps world XZ coordinates to
/// screen-space pixel coordinates and provides cell color mappings by POI category.
/// <para>
/// All methods use primitive types (floats, tuples) to avoid Unity dependencies,
/// enabling unit testing without game assemblies.
/// </para>
/// </summary>
public static class MinimapProjection
{
    /// <summary>
    /// Maps a world XZ position to screen-space pixel coordinates within a display rectangle.
    /// Screen Y is inverted (top = 0) so world Z maps to screen Y with a flip.
    /// </summary>
    /// <param name="worldX">World-space X coordinate.</param>
    /// <param name="worldZ">World-space Z coordinate.</param>
    /// <param name="rectX">Display rect left edge (pixels).</param>
    /// <param name="rectY">Display rect top edge (pixels).</param>
    /// <param name="rectW">Display rect width (pixels).</param>
    /// <param name="rectH">Display rect height (pixels).</param>
    /// <param name="minX">Grid minimum X bound (world space).</param>
    /// <param name="minZ">Grid minimum Z bound (world space).</param>
    /// <param name="maxX">Grid maximum X bound (world space).</param>
    /// <param name="maxZ">Grid maximum Z bound (world space).</param>
    /// <returns>Screen-space (x, y) pixel coordinates.</returns>
    public static (float x, float y) WorldToMinimap(
        float worldX,
        float worldZ,
        float rectX,
        float rectY,
        float rectW,
        float rectH,
        float minX,
        float minZ,
        float maxX,
        float maxZ
    )
    {
        float rangeX = maxX - minX;
        float rangeZ = maxZ - minZ;

        // Avoid division by zero for degenerate grids
        float tx = rangeX > 0.001f ? (worldX - minX) / rangeX : 0.5f;
        float tz = rangeZ > 0.001f ? (worldZ - minZ) / rangeZ : 0.5f;

        float screenX = rectX + tx * rectW;
        // Invert Z → Y (screen Y increases downward, world Z increases "forward")
        float screenY = rectY + (1f - tz) * rectH;

        return (screenX, screenY);
    }

    /// <summary>
    /// Returns the RGBA color for a grid cell based on its dominant POI category
    /// and navigability state. Matches the color scheme from the design document.
    /// </summary>
    /// <param name="dominant">The dominant POI category, or null if no POIs.</param>
    /// <param name="isNavigable">Whether the cell has any navigable POIs.</param>
    /// <returns>RGBA color as (r, g, b, a) tuple with values in [0, 1].</returns>
    public static (float r, float g, float b, float a) GetCellColor(PoiCategory? dominant, bool isNavigable)
    {
        if (!isNavigable)
            return (0.1f, 0.1f, 0.1f, 0.8f); // Black — non-navigable

        if (!dominant.HasValue)
            return (0.2f, 0.2f, 0.2f, 0.5f); // Dark gray — empty navigable

        return dominant.Value switch
        {
            PoiCategory.Container => (0.9f, 0.75f, 0.2f, 0.7f), // Gold
            PoiCategory.LooseLoot => (0.9f, 0.5f, 0.1f, 0.7f), // Orange
            PoiCategory.Quest => (0.2f, 0.8f, 0.3f, 0.7f), // Green
            PoiCategory.Exfil => (0.8f, 0.2f, 0.2f, 0.7f), // Red
            PoiCategory.SpawnPoint => (0.3f, 0.4f, 0.8f, 0.7f), // Blue
            PoiCategory.Synthetic => (0.3f, 0.3f, 0.3f, 0.7f), // Gray
            _ => (0.3f, 0.3f, 0.3f, 0.7f), // Gray fallback
        };
    }
}
