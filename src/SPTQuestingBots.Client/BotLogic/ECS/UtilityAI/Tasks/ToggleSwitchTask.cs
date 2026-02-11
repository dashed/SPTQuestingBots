using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Scores high when <c>CurrentQuestAction == ToggleSwitch</c>.
/// This action handles its own movement.
/// </summary>
public sealed class ToggleSwitchTask : QuestUtilityTask
{
    public const float BaseScore = 0.70f;

    public override int BotActionTypeId
    {
        get { return UtilityAI.BotActionTypeId.ToggleSwitch; }
    }

    public override string ActionReason
    {
        get { return "ToggleSwitch"; }
    }

    public ToggleSwitchTask(float hysteresis = 0.10f)
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

        if (entity.CurrentQuestAction != QuestActionId.ToggleSwitch)
        {
            return 0f;
        }

        return BaseScore;
    }
}
