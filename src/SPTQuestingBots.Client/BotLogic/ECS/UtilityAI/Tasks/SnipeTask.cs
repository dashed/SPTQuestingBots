namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks
{
    /// <summary>
    /// Scores high when the bot is close to a snipe position.
    /// Only active when <c>CurrentQuestAction == Snipe</c> and <c>IsCloseToObjective</c>.
    /// </summary>
    public sealed class SnipeTask : QuestUtilityTask
    {
        public const float BaseScore = 0.65f;

        public override int BotActionTypeId => UtilityAI.BotActionTypeId.Snipe;
        public override string ActionReason => "Snipe";

        public SnipeTask(float hysteresis = 0.15f)
            : base(hysteresis) { }

        public override void ScoreEntity(int ordinal, BotEntity entity)
        {
            entity.TaskScores[ordinal] = Score(entity);
        }

        internal static float Score(BotEntity entity)
        {
            if (!entity.HasActiveObjective)
                return 0f;

            if (entity.CurrentQuestAction != QuestActionId.Snipe)
                return 0f;

            if (!entity.IsCloseToObjective)
                return 0f;

            return BaseScore;
        }
    }
}
