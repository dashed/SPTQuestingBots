namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks
{
    /// <summary>
    /// Scores high when a follower is close to its tactical position and should hold.
    /// Reuses HoldPosition action for actual holding behavior.
    /// </summary>
    public sealed class HoldTacticalPositionTask : QuestUtilityTask
    {
        public const float BaseScore = 0.65f;
        public const float MinDistanceSqr = 9f; // 3m squared

        public override int BotActionTypeId => UtilityAI.BotActionTypeId.HoldPosition;
        public override string ActionReason => "HoldTacticalPosition";

        public HoldTacticalPositionTask(float hysteresis = 0.10f)
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

            if (sqrDist > MinDistanceSqr)
                return 0f;

            return BaseScore;
        }
    }
}
