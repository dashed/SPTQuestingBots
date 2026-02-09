namespace SPTQuestingBots.BotLogic.ECS.UtilityAI
{
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
            return new UtilityTaskManager(new UtilityTask[] { new Tasks.GoToTacticalPositionTask(), new Tasks.HoldTacticalPositionTask() });
        }
    }
}
