using System;
using System.Runtime.CompilerServices;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Configuration parameters for loot scoring.
    /// </summary>
    public readonly struct LootScoringConfig
    {
        public readonly float MinItemValue;
        public readonly float ValueScoreCap;
        public readonly float DistancePenaltyFactor;
        public readonly float QuestProximityBonus;
        public readonly float GearUpgradeScoreBonus;
        public readonly float LootCooldownSeconds;

        public LootScoringConfig(
            float minItemValue,
            float valueScoreCap,
            float distancePenaltyFactor,
            float questProximityBonus,
            float gearUpgradeScoreBonus,
            float lootCooldownSeconds
        )
        {
            MinItemValue = minItemValue;
            ValueScoreCap = valueScoreCap;
            DistancePenaltyFactor = distancePenaltyFactor;
            QuestProximityBonus = questProximityBonus;
            GearUpgradeScoreBonus = gearUpgradeScoreBonus;
            LootCooldownSeconds = lootCooldownSeconds;
        }
    }

    /// <summary>
    /// Score a loot opportunity against current bot state.
    /// Pure C# — no Unity or EFT dependencies.
    /// </summary>
    public static class LootScorer
    {
        /// <summary>
        /// Compute a 0-1 score for a loot target.
        /// Higher score = more desirable loot to pursue.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Score(
            float targetValue,
            float distanceSqr,
            float inventorySpaceFree,
            bool isInCombat,
            float distanceToObjectiveSqr,
            float timeSinceLastLoot,
            bool isGearUpgrade,
            in LootScoringConfig config
        )
        {
            if (isInCombat)
                return 0f;

            if (inventorySpaceFree <= 0f && !isGearUpgrade)
                return 0f;

            if (targetValue < config.MinItemValue && !isGearUpgrade)
                return 0f;

            float valueScore = ItemValueEstimator.NormalizeValue(targetValue, config.ValueScoreCap) * 0.5f;

            float distancePenalty = distanceSqr * config.DistancePenaltyFactor;
            if (distancePenalty < 0f)
                distancePenalty = 0f;
            if (distancePenalty > 0.4f)
                distancePenalty = 0.4f;

            float proximityBonus = distanceToObjectiveSqr < 400f ? config.QuestProximityBonus : 0f;

            float gearBonus = isGearUpgrade ? config.GearUpgradeScoreBonus : 0f;

            float cooldownFactor =
                (config.LootCooldownSeconds > 0f && timeSinceLastLoot < config.LootCooldownSeconds)
                    ? timeSinceLastLoot / config.LootCooldownSeconds
                    : 1f;

            float score = (valueScore + proximityBonus + gearBonus - distancePenalty) * cooldownFactor;

            if (score < 0f)
                return 0f;
            if (score > 1f)
                return 1f;
            return score;
        }
    }
}
