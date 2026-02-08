using System.Linq;
using Comfort.Common;
using EFT;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Models.Questing;
using SPTQuestingBots.ZoneMovement.Core;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Integration;

/// <summary>
/// Selects zone movement objectives using live field state (advection, convergence,
/// per-bot momentum, and noise) instead of the default nearest-to-bot selection.
/// <para>
/// This matches Phobos's <c>GotoObjectiveStrategy.AssignNewObjective()</c> pattern
/// where <c>LocationSystem.RequestNear()</c> composes field vectors to pick the best
/// neighboring cell. Here we delegate to <see cref="WorldGridManager.GetRecommendedDestination(string, Vector3)"/>
/// which uses the same field composition pipeline.
/// </para>
/// </summary>
public static class ZoneObjectiveCycler
{
    /// <summary>
    /// Selects a zone quest objective using live field state instead of nearest-to-bot.
    /// </summary>
    /// <param name="bot">The bot to select an objective for.</param>
    /// <param name="zoneQuest">The zone movement quest containing cell-based objectives.</param>
    /// <param name="gridManager">The world grid manager providing field-based destination selection.</param>
    /// <returns>
    /// A <see cref="QuestObjective"/> selected via field composition, or <c>null</c> if
    /// no valid objective could be found (caller should fall back to default selection).
    /// </returns>
    public static QuestObjective SelectZoneObjective(BotOwner bot, Quest zoneQuest, WorldGridManager gridManager)
    {
        if (bot == null || zoneQuest == null || gridManager == null || !gridManager.IsInitialized)
            return null;

        // Get field-based recommended destination using per-bot state
        Vector3? destination = gridManager.GetRecommendedDestination(bot.Profile.Id, bot.Position);
        if (!destination.HasValue)
            return null;

        // Find the grid cell for the destination
        GridCell cell = gridManager.GetCellForBot(destination.Value);
        if (cell == null)
            return null;

        // Match the objective by the naming convention used in ZoneQuestBuilder
        string objectiveName = $"Zone ({cell.Col},{cell.Row})";
        QuestObjective match = zoneQuest.AllObjectives.FirstOrDefault(o => o.ToString() == objectiveName);

        if (match != null && match.CanAssignBot(bot))
            return match;

        // Fallback: nearest remaining objective the bot can do
        return zoneQuest.RemainingObjectivesForBot(bot)?.Where(o => o.CanAssignBot(bot))?.NearestToBot(bot);
    }
}
