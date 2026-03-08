using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI;

/// <summary>
/// Creates a UtilityTaskManager for squad follower bots.
/// Includes tactical tasks (GoToTacticalPosition, HoldTacticalPosition) plus
/// opportunistic tasks (Loot, Investigate, Linger, Patrol) so followers
/// occasionally do interesting things instead of endlessly alternating
/// "go to leader" / "hold near leader."
/// </summary>
public static class SquadTaskFactory
{
    /// <summary>Number of squad follower tasks. Used to size TaskScores.</summary>
    public const int TaskCount = 6;

    public static UtilityTaskManager Create()
    {
        LoggingController.LogDebug("[SquadTaskFactory] Creating squad task manager with " + TaskCount + " tasks");
        return new UtilityTaskManager(
            new UtilityTask[]
            {
                new Tasks.GoToTacticalPositionTask(),
                new Tasks.HoldTacticalPositionTask(),
                new Tasks.LootTask(),
                new Tasks.InvestigateTask(),
                new Tasks.LingerTask(),
                new Tasks.PatrolTask(),
            }
        );
    }
}
