using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks
{
    /// <summary>
    /// Scores high when the bot's current path requires unlocking a door.
    /// High base score (0.70) ensures it takes priority over GoToObjective.
    /// </summary>
    public sealed class UnlockDoorTask : QuestUtilityTask
    {
        public const float BaseScore = 0.70f;

        public override int BotActionTypeId => UtilityAI.BotActionTypeId.UnlockDoor;
        public override string ActionReason => "UnlockDoor";

        public UnlockDoorTask(float hysteresis = 0.20f)
            : base(hysteresis) { }

        public override void ScoreEntity(int ordinal, BotEntity entity)
        {
            entity.TaskScores[ordinal] = Score(entity);
        }

        internal static float Score(BotEntity entity)
        {
            if (!entity.HasActiveObjective)
                return 0f;

            if (!entity.MustUnlockDoor)
                return 0f;

            return BaseScore;
        }
    }
}
