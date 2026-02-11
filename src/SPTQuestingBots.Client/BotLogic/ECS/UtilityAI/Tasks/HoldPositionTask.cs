using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Scores high when <c>CurrentQuestAction == HoldAtPosition</c>.
/// This action handles its own movement, so no GoToObjective travel phase is needed.
/// Always wins over GoToObjective when active (higher base score).
/// </summary>
public sealed class HoldPositionTask : QuestUtilityTask
{
    public const float BaseScore = 0.70f;

    public override int BotActionTypeId
    {
        get { return UtilityAI.BotActionTypeId.HoldPosition; }
    }

    public override string ActionReason
    {
        get { return "HoldPosition"; }
    }

    public HoldPositionTask(float hysteresis = 0.10f)
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

        if (entity.CurrentQuestAction != QuestActionId.HoldAtPosition)
        {
            return 0f;
        }

        return BaseScore;
    }
}
