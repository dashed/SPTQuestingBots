using Comfort.Common;
using EFT;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.ZoneMovement.Core;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Integration;

/// <summary>
/// MonoBehaviour that draws a debug overlay showing zone movement system state.
/// <para>
/// Displays grid stats, player cell info, and convergence field data in a
/// semi-transparent panel in the top-left corner. Gated behind the F12 config
/// entry <c>QuestingBotsPluginConfig.ZoneMovementDebugOverlay</c>.
/// </para>
/// </summary>
public class ZoneDebugOverlay : MonoBehaviour
{
    private WorldGridManager gridManager;
    private GUIStyle panelStyle;
    private GUIStyle labelStyle;
    private bool stylesInitialized;

    protected void Awake()
    {
        gridManager = GetComponent<WorldGridManager>();
    }

    protected void OnGUI()
    {
        if (QuestingBotsPluginConfig.ZoneMovementDebugOverlay?.Value != true)
            return;

        if (gridManager == null || !gridManager.IsInitialized)
            return;

        if (!Singleton<GameWorld>.Instantiated || Camera.main == null)
            return;

        InitStyles();

        var grid = gridManager.Grid;
        var mainPlayer = Singleton<GameWorld>.Instance.MainPlayer;
        if (mainPlayer == null || grid == null)
            return;

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
