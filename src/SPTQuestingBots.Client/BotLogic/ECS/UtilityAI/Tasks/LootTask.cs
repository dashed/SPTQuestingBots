using System;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Scores high when the bot has a loot target available and conditions allow looting.
/// Score is based on target value, distance, inventory space, and cooldown.
/// <para>
/// Integrates with the hybrid looting system: utility AI decides <b>when</b> to loot,
/// the dedicated loot controller handles <b>how</b>.
/// </para>
/// </summary>
public sealed class LootTask : QuestUtilityTask
{
    /// <summary>Maximum base score for looting (below GoToObjective's 0.65 by default).</summary>
    public const float MaxBaseScore = 0.55f;

    /// <summary>Squared distance threshold for quest proximity bonus (20m).</summary>
    private const float QuestProximityThresholdSqr = 400f;

    public override int BotActionTypeId
    {
        get { return UtilityAI.BotActionTypeId.Loot; }
    }

    public override string ActionReason
    {
        get { return "Looting"; }
    }

    public LootTask(float hysteresis = 0.15f)
        : base(hysteresis) { }

    public override void ScoreEntity(int ordinal, BotEntity entity)
    {
        float score = Score(entity);
        entity.TaskScores[ordinal] =
            score * ScoringModifiers.CombinedModifier(entity.Aggression, entity.RaidTimeNormalized, BotActionTypeId);
    }

    internal static float Score(BotEntity entity)
    {
        // No loot target → no score
        if (!entity.HasLootTarget)
        {
            return 0f;
        }

        // Combat suppresses looting
        if (entity.IsInCombat)
        {
            return 0f;
        }

        // No inventory space and not a gear upgrade → skip
        // (IsGearUpgrade is encoded in LootTargetValue as negative: not used here;
        //  instead, high value + gear upgrade bonus from scanner already set entity fields)
        if (entity.InventorySpaceFree <= 0f && entity.LootTargetValue < 0f)
        {
            return 0f;
        }

        // Value component: normalize against cap, weight at 50%
        float valueScore = Math.Min(entity.LootTargetValue / 50000f, 1f) * 0.5f;

        // Distance penalty: farther loot = lower score
        float lootDistSqr = Systems.LootScanResult.ComputeDistanceSqr(
            entity.CurrentPositionX,
            entity.CurrentPositionY,
            entity.CurrentPositionZ,
            entity.LootTargetX,
            entity.LootTargetY,
            entity.LootTargetZ
        );
        float distancePenalty = Math.Min(lootDistSqr * 0.001f, 0.4f);

        // Quest proximity bonus: loot near current objective gets a boost
        float proximityBonus = 0f;
        if (entity.HasActiveObjective && entity.DistanceToObjective * entity.DistanceToObjective < QuestProximityThresholdSqr)
        {
            proximityBonus = 0.15f;
        }

        float score = valueScore + proximityBonus - distancePenalty;

        // Clamp to [0, MaxBaseScore]
        if (score < 0f)
        {
            return 0f;
        }

        if (score > MaxBaseScore)
        {
            return MaxBaseScore;
        }

        LoggingController.LogDebug(
            "[LootTask] Entity "
                + entity.Id
                + ": value="
                + valueScore.ToString("F3")
                + ", proximity="
                + proximityBonus.ToString("F2")
                + ", distPenalty="
                + distancePenalty.ToString("F3")
                + ", total="
                + score.ToString("F3")
        );

        return score;
    }
}
