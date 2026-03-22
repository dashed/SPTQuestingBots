using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Scores high when the bot is close to a snipe position.
/// Only active when <c>CurrentQuestAction == Snipe</c> and <c>IsCloseToObjective</c>.
/// Close weapons (pistol/shotgun/revolver) are penalized — they can't snipe effectively.
/// Low ammo also reduces the score.
/// </summary>
public sealed class SnipeTask : QuestUtilityTask
{
    public const float BaseScore = 0.65f;

    /// <summary>
    /// Multiplier applied when the bot has a close weapon (pistol/shotgun/revolver).
    /// Sniping with a shotgun is not effective.
    /// </summary>
    public const float CloseWeaponPenalty = 0.3f;

    /// <summary>
    /// Minimum ammo ratio multiplier (same as AmbushTask for consistency).
    /// </summary>
    public const float MinAmmoMultiplier = 0.3f;

    public override int BotActionTypeId
    {
        get { return UtilityAI.BotActionTypeId.Snipe; }
    }

    public override string ActionReason
    {
        get { return "Snipe"; }
    }

    public SnipeTask(float hysteresis = 0.15f)
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

        if (entity.CurrentQuestAction != QuestActionId.Snipe)
        {
            return 0f;
        }

        if (!entity.IsCloseToObjective)
        {
            return 0f;
        }

        float score = BaseScore;

        // Close weapon penalty: pistols/shotguns/revolvers can't snipe effectively
        if (entity.IsCloseWeapon)
        {
            score *= CloseWeaponPenalty;
        }

        // Ammo penalty: low magazine reduces combat task viability
        float ammoMultiplier = MinAmmoMultiplier + (1f - MinAmmoMultiplier) * entity.AmmoRatio;
        score *= ammoMultiplier;

        return score;
    }
}
