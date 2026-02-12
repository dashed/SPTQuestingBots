using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Scores high when the bot is close to a plant-item position.
/// Only active when <c>CurrentQuestAction == PlantItem</c> and <c>IsCloseToObjective</c>.
/// Plant zone verification is handled by PlantItemAction at execution time.
/// </summary>
public sealed class PlantItemTask : QuestUtilityTask
{
    public const float BaseScore = 0.65f;

    public override int BotActionTypeId
    {
        get { return UtilityAI.BotActionTypeId.PlantItem; }
    }

    public override string ActionReason
    {
        get { return "PlantItem"; }
    }

    public PlantItemTask(float hysteresis = 0.15f)
        : base(hysteresis) { }

    public override void ScoreEntity(int ordinal, BotEntity entity)
    {
        entity.TaskScores[ordinal] = Score(entity);
    }

    internal static float Score(BotEntity entity)
    {
        if (!entity.HasActiveObjective)
        {
            return 0f;
        }

        if (entity.CurrentQuestAction != QuestActionId.PlantItem)
        {
            return 0f;
        }

        if (!entity.IsCloseToObjective)
        {
            return 0f;
        }

        return BaseScore;
    }
}
