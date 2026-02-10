using System.Collections.Generic;
using Comfort.Common;
using EFT;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.ZoneMovement.Core;
using SPTQuestingBots.ZoneMovement.Diag;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Integration;

/// <summary>
/// MonoBehaviour that draws a debug overlay showing zone movement system state.
/// <para>
/// Displays grid stats, player cell info, and convergence field data in a
/// semi-transparent panel in the top-left corner. Optionally renders a 2D minimap
/// showing grid cells, field vectors, bot positions, and zone sources.
/// </para>
/// <para>
/// The text panel is gated behind <c>QuestingBotsPluginConfig.ZoneMovementDebugOverlay</c>.
/// The minimap is gated behind <c>QuestingBotsPluginConfig.ZoneMovementDebugMinimap</c>.
/// </para>
/// </summary>
public class ZoneDebugOverlay : MonoBehaviour
{
    private const float MinimapSize = 400f;
    private const float MinimapMargin = 10f;
    private const float CellGap = 1f;
    private const float ArrowLength = 12f;
    private const float ArrowThickness = 1.5f;
    private const float DotRadius = 3f;
    private const float ZoneDotRadius = 4f;
    private const float LegendWidth = 120f;
    private const float LegendRowHeight = 16f;

    private WorldGridManager gridManager;
    private GUIStyle panelStyle;
    private GUIStyle labelStyle;
    private GUIStyle legendStyle;
    private bool stylesInitialized;

    // Cached per-cell vectors (refreshed on convergence interval)
    private float[] cachedAdvX;
    private float[] cachedAdvZ;
    private float[] cachedConvX;
    private float[] cachedConvZ;
    private float lastVectorCacheTime;

    protected void Awake()
    {
        gridManager = GetComponent<WorldGridManager>();
        LoggingController.LogInfo("[ZoneDebugOverlay] Awake, gridManager=" + (gridManager != null));
    }

    protected void OnGUI()
    {
        if (gridManager == null || !gridManager.IsInitialized)
            return;

        if (!Singleton<GameWorld>.Instantiated || Camera.main == null)
            return;

        var grid = gridManager.Grid;
        var mainPlayer = Singleton<GameWorld>.Instance.MainPlayer;
        if (mainPlayer == null || grid == null)
            return;

        InitStyles();

        if (QuestingBotsPluginConfig.ZoneMovementDebugOverlay?.Value == true)
        {
            RenderTextPanel(grid, mainPlayer);
        }

        if (QuestingBotsPluginConfig.ZoneMovementDebugMinimap?.Value == true)
        {
            RenderMinimap(grid, mainPlayer);
        }
    }

    /// <summary>
    /// Renders the text information panel in the top-left corner.
    /// </summary>
    private void RenderTextPanel(WorldGrid grid, Player mainPlayer)
    {
        // Count navigable cells and POIs
        int navigableCells = 0;
        int totalPois = 0;
        int containers = 0;
        int quests = 0;
        int exfils = 0;
        int spawns = 0;
        int synthetic = 0;

        for (int col = 0; col < grid.Cols; col++)
        {
            for (int row = 0; row < grid.Rows; row++)
            {
                var cell = grid.GetCell(col, row);
                if (cell.IsNavigable)
                    navigableCells++;

                for (int p = 0; p < cell.POIs.Count; p++)
                {
                    totalPois++;
                    switch (cell.POIs[p].Category)
                    {
                        case PoiCategory.Container:
                            containers++;
                            break;
                        case PoiCategory.Quest:
                            quests++;
                            break;
                        case PoiCategory.Exfil:
                            exfils++;
                            break;
                        case PoiCategory.SpawnPoint:
                            spawns++;
                            break;
                        case PoiCategory.Synthetic:
                            synthetic++;
                            break;
                    }
                }
            }
        }

        // Player cell info
        var playerCell = grid.GetCell(mainPlayer.Position);
        string cellInfo = "outside grid";
        if (playerCell != null)
        {
            var dominant = ZoneQuestBuilder.GetDominantCategory(playerCell);
            cellInfo = $"({playerCell.Col},{playerCell.Row}) dominant={dominant} density={playerCell.PoiDensity:F1}";
        }

        // Build overlay text
        string text =
            $"<b>[Zone Movement]</b>\n"
            + $"Grid: {grid.Cols}x{grid.Rows} ({grid.Cols * grid.Rows} cells, {navigableCells} navigable), cell: {grid.CellSize:F1}m\n"
            + $"POIs: {totalPois} ({containers}C {quests}Q {exfils}E {spawns}S {synthetic}Syn)\n"
            + $"Player cell: {cellInfo}";

        // Draw panel
        float width = 450;
        float height = 80;
        GUI.Box(new Rect(10, 10, width, height), GUIContent.none, panelStyle);
        GUI.Label(new Rect(15, 12, width - 10, height - 4), text, labelStyle);
    }

    /// <summary>
    /// Renders a 2D minimap showing grid cells, field vectors, positions, and legend.
    /// Positioned in the bottom-right corner of the screen.
    /// </summary>
    private void RenderMinimap(WorldGrid grid, Player mainPlayer)
    {
        float screenW = Screen.width;
        float screenH = Screen.height;

        // Position minimap in bottom-right corner
        var minimapRect = new Rect(screenW - MinimapSize - MinimapMargin, screenH - MinimapSize - MinimapMargin, MinimapSize, MinimapSize);

        // Background
        DebugDrawing.DrawFilledRect(minimapRect, new Color(0f, 0f, 0f, 0.85f));
        DebugDrawing.DrawRectOutline(minimapRect, new Color(0.5f, 0.5f, 0.5f, 0.9f), 2f);

        float minX = grid.MinBounds.x;
        float minZ = grid.MinBounds.z;
        float maxX = grid.MaxBounds.x;
        float maxZ = grid.MaxBounds.z;

        // Refresh cached vectors periodically
        RefreshVectorCache(grid, minX, minZ, maxX, maxZ);

        float cellW = (minimapRect.width - CellGap) / grid.Cols;
        float cellH = (minimapRect.height - CellGap) / grid.Rows;

        // Draw cells
        for (int col = 0; col < grid.Cols; col++)
        {
            for (int row = 0; row < grid.Rows; row++)
            {
                var cell = grid.GetCell(col, row);
                var dominant = ZoneQuestBuilder.GetDominantCategory(cell);
                var (r, g, b, a) = MinimapProjection.GetCellColor(cell.IsNavigable ? dominant : null, cell.IsNavigable);

                // Cell screen position (Z inverted for screen Y)
                float cx = minimapRect.x + col * cellW;
                float cy = minimapRect.y + (grid.Rows - 1 - row) * cellH;
                var cellRect = new Rect(cx, cy, cellW - CellGap, cellH - CellGap);
                DebugDrawing.DrawFilledRect(cellRect, new Color(r, g, b, a));

                // Draw field vectors at cell center
                int idx = col * grid.Rows + row;
                var cellCenter = new Vector2(cx + cellW * 0.5f, cy + cellH * 0.5f);

                // Advection arrow (white)
                if (cachedAdvX != null && idx < cachedAdvX.Length)
                {
                    float ax = cachedAdvX[idx];
                    float az = cachedAdvZ[idx];
                    // Negate Z for screen-Y inversion
                    DebugDrawing.DrawArrow(cellCenter, ax, -az, ArrowLength, ArrowThickness, new Color(1f, 1f, 1f, 0.6f));
                }

                // Convergence arrow (red)
                if (cachedConvX != null && idx < cachedConvX.Length)
                {
                    float cvx = cachedConvX[idx];
                    float cvz = cachedConvZ[idx];
                    if (cvx * cvx + cvz * cvz > 0.001f)
                    {
                        DebugDrawing.DrawArrow(cellCenter, cvx, -cvz, ArrowLength * 0.8f, ArrowThickness, new Color(1f, 0.3f, 0.3f, 0.7f));
                    }
                }
            }
        }

        // Draw zone source dots (blue)
        var zoneSources = gridManager.ZoneSources;
        if (zoneSources != null)
        {
            for (int i = 0; i < zoneSources.Count; i++)
            {
                var (pos, _) = zoneSources[i];
                var (sx, sy) = MinimapProjection.WorldToMinimap(
                    pos.x,
                    pos.z,
                    minimapRect.x,
                    minimapRect.y,
                    minimapRect.width,
                    minimapRect.height,
                    minX,
                    minZ,
                    maxX,
                    maxZ
                );
                DebugDrawing.DrawDot(new Vector2(sx, sy), ZoneDotRadius, new Color(0.2f, 0.4f, 1f, 0.9f));
            }
        }

        // Draw bot position dots (cyan)
        var botPositions = gridManager.CachedBotPositions;
        if (botPositions != null)
        {
            for (int i = 0; i < botPositions.Count; i++)
            {
                var (sx, sy) = MinimapProjection.WorldToMinimap(
                    botPositions[i].x,
                    botPositions[i].z,
                    minimapRect.x,
                    minimapRect.y,
                    minimapRect.width,
                    minimapRect.height,
                    minX,
                    minZ,
                    maxX,
                    maxZ
                );
                DebugDrawing.DrawDot(new Vector2(sx, sy), DotRadius, Color.cyan);
            }
        }

        // Draw player position dot (white, slightly larger)
        var (px, py) = MinimapProjection.WorldToMinimap(
            mainPlayer.Position.x,
            mainPlayer.Position.z,
            minimapRect.x,
            minimapRect.y,
            minimapRect.width,
            minimapRect.height,
            minX,
            minZ,
            maxX,
            maxZ
        );
        DebugDrawing.DrawDot(new Vector2(px, py), DotRadius + 1f, Color.white);

        // Draw legend
        RenderLegend(minimapRect);
    }

    /// <summary>
    /// Renders a legend panel above the minimap showing color meanings.
    /// </summary>
    private void RenderLegend(Rect minimapRect)
    {
        var entries = new (string label, Color color)[]
        {
            ("Container", new Color(0.9f, 0.75f, 0.2f, 0.7f)),
            ("Loose Loot", new Color(0.9f, 0.5f, 0.1f, 0.7f)),
            ("Quest", new Color(0.2f, 0.8f, 0.3f, 0.7f)),
            ("Exfil", new Color(0.8f, 0.2f, 0.2f, 0.7f)),
            ("Spawn", new Color(0.3f, 0.4f, 0.8f, 0.7f)),
            ("Synthetic", new Color(0.3f, 0.3f, 0.3f, 0.7f)),
            ("Advection →", new Color(1f, 1f, 1f, 0.6f)),
            ("Convergence →", new Color(1f, 0.3f, 0.3f, 0.7f)),
            ("Player ●", Color.white),
            ("Bot ●", Color.cyan),
            ("Zone ●", new Color(0.2f, 0.4f, 1f, 0.9f)),
        };

        float legendH = entries.Length * LegendRowHeight + 8f;
        var legendRect = new Rect(minimapRect.xMax - LegendWidth, minimapRect.y - legendH - 4f, LegendWidth, legendH);

        DebugDrawing.DrawFilledRect(legendRect, new Color(0f, 0f, 0f, 0.8f));

        for (int i = 0; i < entries.Length; i++)
        {
            float y = legendRect.y + 4f + i * LegendRowHeight;
            // Color swatch
            DebugDrawing.DrawFilledRect(new Rect(legendRect.x + 4f, y + 2f, 10f, 10f), entries[i].color);
            // Label
            GUI.Label(new Rect(legendRect.x + 18f, y - 1f, LegendWidth - 22f, LegendRowHeight), entries[i].label, legendStyle);
        }
    }

    /// <summary>
    /// Refreshes cached per-cell advection and convergence vectors.
    /// Only recomputes when enough time has elapsed (matching convergence update interval).
    /// </summary>
    private void RefreshVectorCache(WorldGrid grid, float minX, float minZ, float maxX, float maxZ)
    {
        float now = Time.time;
        int totalCells = grid.Cols * grid.Rows;

        if (cachedAdvX != null && now - lastVectorCacheTime < 5f)
            return;

        lastVectorCacheTime = now;

        if (cachedAdvX == null || cachedAdvX.Length != totalCells)
        {
            cachedAdvX = new float[totalCells];
            cachedAdvZ = new float[totalCells];
            cachedConvX = new float[totalCells];
            cachedConvZ = new float[totalCells];
        }

        var advField = gridManager.Advection;
        var convField = gridManager.Convergence;
        var botPositions = gridManager.CachedBotPositions as List<Vector3>;
        var playerPositions = gridManager.CachedPlayerPositions as List<Vector3>;

        for (int col = 0; col < grid.Cols; col++)
        {
            for (int row = 0; row < grid.Rows; row++)
            {
                int idx = col * grid.Rows + row;
                var cell = grid.GetCell(col, row);

                if (advField != null)
                {
                    advField.GetAdvection(cell.Center, botPositions, out cachedAdvX[idx], out cachedAdvZ[idx]);
                }

                if (convField != null)
                {
                    convField.ComputeConvergence(cell.Center, playerPositions, out cachedConvX[idx], out cachedConvZ[idx]);
                }
            }
        }
    }

    private void InitStyles()
    {
        if (stylesInitialized)
            return;

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.7f));

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 13;
        labelStyle.normal.textColor = Color.white;
        labelStyle.richText = true;
        labelStyle.wordWrap = false;

        legendStyle = new GUIStyle(GUI.skin.label);
        legendStyle.fontSize = 11;
        legendStyle.normal.textColor = Color.white;
        legendStyle.wordWrap = false;

        stylesInitialized = true;
    }

    private static Texture2D MakeTex(int width, int height, Color color)
    {
        var pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;

        var tex = new Texture2D(width, height);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
