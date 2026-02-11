using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Scores high when <c>CurrentQuestAction == CloseNearbyDoors</c>.
/// This action handles its own movement.
/// </summary>
public sealed class CloseDoorsTask : QuestUtilityTask
{
    public const float BaseScore = 0.70f;

    public override int BotActionTypeId
    {
        get { return UtilityAI.BotActionTypeId.CloseNearbyDoors; }
    }

    public override string ActionReason
    {
        get { return "CloseNearbyDoors"; }
    }

    public CloseDoorsTask(float hysteresis = 0.10f)
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

        if (entity.CurrentQuestAction != QuestActionId.CloseNearbyDoors)
        {
            return 0f;
        }

        return BaseScore;
    }
}
