using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Scores high when the bot is close to an ambush position.
/// Only active when <c>CurrentQuestAction == Ambush</c> and <c>IsCloseToObjective</c>.
/// Gets a small bonus when the game's BotAmbushData also has a valid cover point,
/// indicating the game's tactical AI agrees an ambush is appropriate.
/// </summary>
public sealed class AmbushTask : QuestUtilityTask
{
    public const float BaseScore = 0.65f;

    /// <summary>
    /// Score bonus applied when the game's BotAmbushData has a valid, unspotted cover point.
    /// This reinforces our ambush decision when the game's tactical AI agrees.
    /// </summary>
    public const float GameAmbushBonus = 0.10f;

    /// <summary>
    /// Score bonus when BSG's Mind profile has AMBUSH_WHEN_UNDER_FIRE enabled.
    /// Indicates the bot's difficulty settings favor ambush behavior.
    /// </summary>
    public const float MindAmbushBonus = 0.08f;

    /// <summary>
    /// Minimum ammo ratio multiplier. Even at 0 ammo, score is scaled by this floor
    /// rather than going to zero (bot may still want to hold position while reloading).
    /// </summary>
    public const float MinAmmoMultiplier = 0.3f;

    public override int BotActionTypeId
    {
        get { return UtilityAI.BotActionTypeId.Ambush; }
    }

    public override string ActionReason
    {
        get { return "Ambush"; }
    }

    public AmbushTask(float hysteresis = 0.15f)
        : base(hysteresis) { }

    public override void ScoreEntity(int ordinal, BotEntity entity)
    {
        float score = Score(entity);
        float coverInfluence = ScoringModifiers.ComputeCoverInfluence(entity.IsInCover, entity.HasEnemyInfo);
        entity.TaskScores[ordinal] =
            score
            * ScoringModifiers.CombinedModifier(
                entity.Aggression,
                entity.RaidTimeNormalized,
                entity.HumanPlayerProximity,
                coverInfluence,
                entity.IsInDogFight,
                BotActionTypeId
            );
    }

    internal static float Score(BotEntity entity)
    {
        if (!entity.HasActiveObjective)
        {
            return 0f;
        }

        if (entity.CurrentQuestAction != QuestActionId.Ambush)
        {
            return 0f;
        }

        if (!entity.IsCloseToObjective)
        {
            return 0f;
        }

        float score = BaseScore;

        // Bonus when game's BotAmbushData has a valid cover point
        if (entity.HasGameAmbushPoint)
        {
            score += GameAmbushBonus;
        }

        // Bonus when BSG Mind profile favors ambush behavior
        if (entity.MindAmbushWhenUnderFire)
        {
            score += MindAmbushBonus;
        }

        // Ammo penalty: low magazine reduces combat task viability.
        // Lerp from MinAmmoMultiplier (empty) to 1.0 (full).
        float ammoMultiplier = MinAmmoMultiplier + (1f - MinAmmoMultiplier) * entity.AmmoRatio;
        score *= ammoMultiplier;

        return score;
    }
}
