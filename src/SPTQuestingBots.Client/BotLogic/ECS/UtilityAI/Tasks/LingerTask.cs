using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks
{
    /// <summary>
    /// Scores high briefly after a bot completes an objective, causing it to
    /// pause and look around before moving on. Score decays linearly from
    /// BaseScore to zero over LingerDuration seconds.
    /// <para>
    /// Gating: not in combat, ObjectiveCompletedTime is set, elapsed time
    /// within linger duration.
    /// </para>
    /// </summary>
    public sealed class LingerTask : QuestUtilityTask
    {
        /// <summary>Default base score for linger behavior.</summary>
        public const float DefaultBaseScore = 0.45f;

        public override int BotActionTypeId => UtilityAI.BotActionTypeId.Linger;
        public override string ActionReason => "Linger";

        /// <summary>Base score at the start of lingering (configurable).</summary>
        public float BaseScore { get; set; } = DefaultBaseScore;

        public LingerTask(float hysteresis = 0.10f)
            : base(hysteresis) { }

        public override void ScoreEntity(int ordinal, BotEntity entity)
        {
            entity.TaskScores[ordinal] = Score(entity, BaseScore);
        }

        internal static float Score(BotEntity entity, float baseScore)
        {
            // No recent objective completion
            if (entity.ObjectiveCompletedTime <= 0f)
            {
                return 0f;
            }

            // In combat — don't idle
            if (entity.IsInCombat)
            {
                LoggingController.LogDebug("[LingerTask] Entity " + entity.Id + ": in combat, score=0");
                return 0f;
            }

            // Linger duration not set
            if (entity.LingerDuration <= 0f)
            {
                return 0f;
            }

            // Already lingering — maintain score with decay
            float elapsed = entity.CurrentGameTime - entity.ObjectiveCompletedTime;

            // Duration expired
            if (elapsed >= entity.LingerDuration)
            {
                return 0f;
            }

            // Negative elapsed (clock issue or future timestamp)
            if (elapsed < 0f)
            {
                return 0f;
            }

            // Linear decay: baseScore * (1 - elapsed/duration)
            float score = baseScore * (1f - elapsed / entity.LingerDuration);

            if (score < 0f)
                return 0f;
            if (score > baseScore)
                return baseScore;

            LoggingController.LogDebug(
                "[LingerTask] Entity "
                    + entity.Id
                    + ": elapsed="
                    + elapsed.ToString("F1")
                    + " duration="
                    + entity.LingerDuration.ToString("F1")
                    + " score="
                    + score.ToString("F2")
            );
            return score;
        }
    }
}
