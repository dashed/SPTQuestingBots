using System;
using System.Collections.Generic;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Models.Questing;
using SPTQuestingBots.ZoneMovement.Core;
using SPTQuestingBots.ZoneMovement.Selection;

namespace SPTQuestingBots.ZoneMovement.Integration;

/// <summary>
/// Creates <see cref="Quest"/> objects from the zone movement grid, enabling bots
/// to use the existing quest assignment pipeline for zone-based movement.
/// <para>
/// Each navigable <see cref="GridCell"/> becomes a <see cref="QuestObjective"/> with
/// a <see cref="QuestObjectiveStep"/> whose action type is selected based on the
/// cell's dominant POI category (e.g. Ambush near containers, Snipe near exfils).
/// </para>
/// <para>
/// The resulting quest is registered with low desirability so it serves as a fallback
/// when no higher-priority quests are available for a bot.
/// </para>
/// </summary>
public static class ZoneQuestBuilder
{
    /// <summary>
    /// Maps <see cref="ZoneActionSelector"/> action indices to <see cref="QuestAction"/> values.
    /// </summary>
    private static readonly QuestAction[] ActionIndexToQuestAction =
    {
        QuestAction.MoveToPosition, // 0
        QuestAction.HoldAtPosition, // 1
        QuestAction.Ambush, // 2
        QuestAction.Snipe, // 3
        QuestAction.PlantItem, // 4
    };

    /// <summary>
    /// Creates a zone movement quest from the grid data.
    /// </summary>
    /// <param name="gridManager">
    /// The initialized <see cref="WorldGridManager"/> containing the world grid.
    /// </param>
    /// <param name="config">Zone movement configuration.</param>
    /// <returns>
    /// A <see cref="Quest"/> with one objective per navigable grid cell, or <c>null</c>
    /// if the grid is not initialized or has no navigable cells.
    /// </returns>
    public static Quest CreateZoneQuests(WorldGridManager gridManager, ZoneMovementConfig config)
    {
        if (gridManager == null || !gridManager.IsInitialized || gridManager.Grid == null)
        {
            LoggingController.LogWarning("[ZoneMovement] Cannot create zone quests: grid not initialized");
            return null;
        }

        var grid = gridManager.Grid;
        var rng = new Random();
        var quest = new Quest(config.QuestName);

        // Configure as low-desirability repeatable fallback
        quest.Desirability = config.QuestDesirability;
        quest.IsRepeatable = true;
        quest.MaxBots = 99; // No bot limit for zone movement

        int objectiveCount = 0;

        for (int col = 0; col < grid.Cols; col++)
        {
            for (int row = 0; row < grid.Rows; row++)
            {
                var cell = grid.GetCell(col, row);
                if (!cell.IsNavigable)
                    continue;

                // Find dominant POI category in this cell
                PoiCategory dominantCategory = GetDominantCategory(cell);

                // Select action based on POI category
                int actionIndex = ZoneActionSelector.SelectActionIndex(dominantCategory, rng);
                var (holdMin, holdMax) = ZoneActionSelector.GetHoldDuration(actionIndex);

                QuestAction questAction = ActionIndexToQuestAction[actionIndex];
                var minElapsedTime = new MinMaxConfig(holdMin, holdMax);

                // Create step → objective → add to quest
                var step = new QuestObjectiveStep(cell.Center, questAction, minElapsedTime);
                var objective = new QuestObjective(step);
                objective.SetName($"Zone ({col},{row})");

                quest.AddObjective(objective);
                objectiveCount++;
            }
        }

        if (objectiveCount == 0)
        {
            LoggingController.LogWarning("[ZoneMovement] No navigable cells found for zone quests");
            return null;
        }

        LoggingController.LogInfo(
            $"[ZoneMovement] Created zone quest \"{config.QuestName}\" with {objectiveCount} objectives (desirability: {config.QuestDesirability})"
        );

        return quest;
    }

    /// <summary>
    /// Determines the dominant POI category in a cell by finding the category
    /// with the highest total weight. Delegates to <see cref="ZoneMathUtils.GetDominantCategory"/>.
    /// </summary>
    internal static PoiCategory GetDominantCategory(GridCell cell) => ZoneMathUtils.GetDominantCategory(cell);
}
