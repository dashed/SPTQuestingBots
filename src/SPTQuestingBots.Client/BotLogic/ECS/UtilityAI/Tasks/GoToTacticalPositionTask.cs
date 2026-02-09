namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks
{
    /// <summary>
    /// Scores high when a follower has a tactical position but is far from it.
    /// Reuses GoToObjective movement action for actual pathfinding.
    /// </summary>
    public sealed class GoToTacticalPositionTask : QuestUtilityTask
    {
        public const float BaseScore = 0.70f;
        public const float MinDistanceSqr = 9f; // 3m squared

        public override int BotActionTypeId => UtilityAI.BotActionTypeId.GoToObjective;
        public override string ActionReason => "GoToTacticalPosition";

        public GoToTacticalPositionTask(float hysteresis = 0.20f)
            : base(hysteresis) { }

        public override void ScoreEntity(int ordinal, BotEntity entity)
        {
            entity.TaskScores[ordinal] = Score(entity);
        }

        internal static float Score(BotEntity entity)
        {
            if (!entity.HasTacticalPosition || !entity.HasBoss)
                return 0f;

            float dx = entity.CurrentPositionX - entity.TacticalPositionX;
            float dy = entity.CurrentPositionY - entity.TacticalPositionY;
            float dz = entity.CurrentPositionZ - entity.TacticalPositionZ;
            float sqrDist = dx * dx + dy * dy + dz * dz;

            if (sqrDist <= MinDistanceSqr)
                return 0f;

            return BaseScore;
        }
    }
}
