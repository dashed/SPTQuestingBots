using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks
{
    /// <summary>
    /// Scores very high for the first few seconds after a bot spawns, causing it to
    /// pause and scan its surroundings before rushing to objectives. Once the spawn
    /// entry duration expires, the task permanently returns 0.
    /// <para>
    /// Gating: not yet complete (one-time transition). No personality/raid-time
    /// modifier — this is a gating task, not a behavior preference.
    /// </para>
    /// </summary>
    public sealed class SpawnEntryTask : QuestUtilityTask
    {
        /// <summary>Maximum base score during spawn entry (higher than GoToObjective's 0.65).</summary>
        public const float MaxBaseScore = 0.80f;

        public override int BotActionTypeId => UtilityAI.BotActionTypeId.SpawnEntry;
        public override string ActionReason => "SpawnEntry";

        public SpawnEntryTask(float hysteresis = 0.10f)
            : base(hysteresis) { }

        public override void ScoreEntity(int ordinal, BotEntity entity)
        {
            // No personality/raid-time modifier — this is a gating task
            entity.TaskScores[ordinal] = Score(entity);
        }

        internal static float Score(BotEntity entity)
        {
            // Already completed — never score again
            if (entity.IsSpawnEntryComplete)
            {
                return 0f;
            }

            // No spawn entry duration configured (disabled or not initialized)
            if (entity.SpawnEntryDuration <= 0f)
            {
                return 0f;
            }

            float elapsed = entity.CurrentGameTime - entity.SpawnTime;

            // Negative elapsed (clock issue or future spawn time)
            if (elapsed < 0f)
            {
                return MaxBaseScore;
            }

            // Duration expired — mark complete and return 0
            if (elapsed >= entity.SpawnEntryDuration)
            {
                entity.IsSpawnEntryComplete = true;
                LoggingController.LogInfo(
                    "[SpawnEntryTask] Entity " + entity.Id + ": spawn entry complete after " + elapsed.ToString("F1") + "s"
                );
                return 0f;
            }

            LoggingController.LogDebug(
                "[SpawnEntryTask] Entity "
                    + entity.Id
                    + ": elapsed="
                    + elapsed.ToString("F1")
                    + " duration="
                    + entity.SpawnEntryDuration.ToString("F1")
                    + " score="
                    + MaxBaseScore.ToString("F2")
            );
            return MaxBaseScore;
        }
    }
}
