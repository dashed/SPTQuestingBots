using System;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks
{
    /// <summary>
    /// Scores high when the bot is far from its objective and needs to travel.
    /// Mirrors Phobos <c>GotoObjectiveAction</c> scoring:
    /// base 0.65 when far, decays to 0 when close.
    /// <para>
    /// Covers <c>MoveToPosition</c> and the travel phase of
    /// <c>Ambush</c>, <c>Snipe</c>, and <c>PlantItem</c>.
    /// </para>
    /// </summary>
    public sealed class GoToObjectiveTask : QuestUtilityTask
    {
        public const float BaseScore = 0.65f;

        public override int BotActionTypeId => UtilityAI.BotActionTypeId.GoToObjective;
        public override string ActionReason => "GoToObjective";

        public GoToObjectiveTask(float hysteresis = 0.25f)
            : base(hysteresis) { }

        public override void ScoreEntity(int ordinal, BotEntity entity)
        {
            float score = Score(entity);
            entity.TaskScores[ordinal] =
                score * ScoringModifiers.CombinedModifier(entity.Aggression, entity.RaidTimeNormalized, BotActionTypeId);
        }

        internal static float Score(BotEntity entity)
        {
            if (!entity.HasActiveObjective)
                return 0f;

            // UnlockDoorTask handles door-blocked paths
            if (entity.MustUnlockDoor)
                return 0f;

            int action = entity.CurrentQuestAction;

            // These actions handle their own movement — no GoToObjective needed
            if (
                action == QuestActionId.HoldAtPosition
                || action == QuestActionId.ToggleSwitch
                || action == QuestActionId.CloseNearbyDoors
                || action == QuestActionId.RequestExtract
                || action == QuestActionId.Undefined
            )
            {
                return 0f;
            }

            // For two-phase actions (Ambush/Snipe/PlantItem): score 0 when close
            // so the action-specific task takes over
            if (action == QuestActionId.Ambush || action == QuestActionId.Snipe || action == QuestActionId.PlantItem)
            {
                if (entity.IsCloseToObjective)
                    return 0f;
            }

            // Distance-based continuous scoring: close → low, far → high
            // score = BaseScore * (1 - exp(-distance / falloff))
            // At 0m → 0, at 50m → ~0.31, at 75m → ~0.41, at 200m → ~0.61
            float distance = entity.DistanceToObjective;
            float falloff = 75f;
            float score = BaseScore * (1f - (float)Math.Exp(-distance / falloff));
            return score;
        }
    }
}
