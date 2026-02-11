using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI;

/// <summary>
/// Creates a UtilityTaskManager for squad follower bots.
/// Includes GoToTacticalPosition and HoldTacticalPosition tasks.
/// </summary>
public static class SquadTaskFactory
{
    /// <summary>Number of squad follower tasks. Used to size TaskScores.</summary>
    public const int TaskCount = 2;

    public static UtilityTaskManager Create()
    {
        LoggingController.LogDebug("[SquadTaskFactory] Creating squad task manager with " + TaskCount + " tasks");
        return new UtilityTaskManager(new UtilityTask[] { new Tasks.GoToTacticalPositionTask(), new Tasks.HoldTacticalPositionTask() });
    }
}
