using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI
{
    /// <summary>
    /// Creates and configures a <see cref="UtilityTaskManager"/> with all 8 quest utility tasks.
    /// Single point of truth for task count and registration order.
    /// </summary>
    public static class QuestTaskFactory
    {
        /// <summary>Number of quest utility tasks. Used to size <c>BotEntity.TaskScores</c>.</summary>
        public const int TaskCount = 11;

        /// <summary>
        /// Create a new <see cref="UtilityTaskManager"/> pre-populated with all quest tasks.
        /// </summary>
        public static UtilityTaskManager Create()
        {
            LoggingController.LogInfo("[QuestTaskFactory] Creating UtilityTaskManager with " + TaskCount + " tasks");
            return new UtilityTaskManager(
                new UtilityTask[]
                {
                    new Tasks.GoToObjectiveTask(),
                    new Tasks.AmbushTask(),
                    new Tasks.SnipeTask(),
                    new Tasks.HoldPositionTask(),
                    new Tasks.PlantItemTask(),
                    new Tasks.UnlockDoorTask(),
                    new Tasks.ToggleSwitchTask(),
                    new Tasks.CloseDoorsTask(),
                    new Tasks.LootTask(),
                    new Tasks.VultureTask(),
                    new Tasks.LingerTask(),
                }
            );
        }
    }
}
