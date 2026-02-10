using System;
using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Configuration parameters for quest scoring.
    /// Mirrors the weighting/randomness values from BotQuests config.
    /// </summary>
    public readonly struct QuestScoringConfig
    {
        public readonly float DistanceWeighting;
        public readonly float DesirabilityWeighting;
        public readonly float ExfilDirectionWeighting;
        public readonly int DistanceRandomness;
        public readonly int DesirabilityRandomness;
        public readonly float MaxExfilAngle;
        public readonly float DesirabilityActiveQuestMultiplier;

        public QuestScoringConfig(
            float distanceWeighting,
            float desirabilityWeighting,
            float exfilDirectionWeighting,
            int distanceRandomness,
            int desirabilityRandomness,
            float maxExfilAngle,
            float desirabilityActiveQuestMultiplier
        )
        {
            DistanceWeighting = distanceWeighting;
            DesirabilityWeighting = desirabilityWeighting;
            ExfilDirectionWeighting = exfilDirectionWeighting;
            DistanceRandomness = distanceRandomness;
            DesirabilityRandomness = desirabilityRandomness;
            MaxExfilAngle = maxExfilAngle;
            DesirabilityActiveQuestMultiplier = desirabilityActiveQuestMultiplier;
        }
    }

    /// <summary>
    /// Pure-logic quest scoring system. Extracts the scoring math from
    /// BotJobAssignmentFactory.GetRandomQuest() into a testable static class.
    /// Replaces 5+ dictionary allocations + OrderBy with pre-allocated arrays + O(n) max scan.
    /// </summary>
    public static class QuestScorer
    {
        /// <summary>
        /// Compute a composite score for a single quest candidate.
        /// Higher score = more desirable quest to assign.
        /// </summary>
        /// <param name="minDistance">Minimum distance from bot to any quest objective.</param>
        /// <param name="maxOverallDistance">Maximum distance across all candidate quests (for normalization).</param>
        /// <param name="maxRandomDistance">Maximum random distance offset (derived from maxOverallDistance * randomness%).</param>
        /// <param name="desirability">Quest desirability value (0-100 range typically).</param>
        /// <param name="isActiveForPlayer">Whether this quest is active for the human player.</param>
        /// <param name="minExfilAngle">Minimum angle between bot-to-objective and bot-to-exfil vectors.</param>
        /// <param name="config">Scoring configuration (weights, randomness, multipliers).</param>
        /// <param name="rng">Random number generator for adding controlled noise.</param>
        /// <returns>Composite score. Higher is better.</returns>
        public static double ScoreQuest(
            float minDistance,
            float maxOverallDistance,
            int maxRandomDistance,
            float desirability,
            bool isActiveForPlayer,
            float minExfilAngle,
            in QuestScoringConfig config,
            System.Random rng
        )
        {
            // Distance fraction: closer quests score higher (inverted, normalized to 0-1)
            double distanceFraction =
                maxOverallDistance > 0 ? 1.0 - (minDistance + rng.Next(-maxRandomDistance, maxRandomDistance)) / maxOverallDistance : 1.0;

            // Desirability fraction: higher desirability scores higher (normalized to ~0-1)
            float activeMultiplier = isActiveForPlayer ? config.DesirabilityActiveQuestMultiplier : 1f;
            float desirabilityFraction =
                (desirability * activeMultiplier + rng.Next(-config.DesirabilityRandomness, config.DesirabilityRandomness)) / 100f;

            // Exfil angle factor: quests aligned with exfil direction are penalized less
            // Angles below MaxExfilAngle get zero penalty; above that, penalty scales linearly to 1.0
            double angleDenominator = 180f - config.MaxExfilAngle;
            double exfilAngleFactor = angleDenominator > 0 ? Math.Max(0, minExfilAngle - config.MaxExfilAngle) / angleDenominator : 0.0;

            return (distanceFraction * config.DistanceWeighting)
                + (desirabilityFraction * config.DesirabilityWeighting)
                - (exfilAngleFactor * config.ExfilDirectionWeighting);
        }

        /// <summary>
        /// Find the index of the highest-scoring element in a score array.
        /// O(n) single pass — replaces OrderBy + Last which is O(n log n) + allocations.
        /// </summary>
        /// <param name="scores">Array of pre-computed scores.</param>
        /// <param name="count">Number of valid elements in the array (may be less than array length).</param>
        /// <returns>Index of the highest score, or -1 if count is 0.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SelectHighestIndex(double[] scores, int count)
        {
            if (count <= 0)
                return -1;

            int bestIndex = 0;
            double bestScore = scores[0];

            for (int i = 1; i < count; i++)
            {
                if (scores[i] > bestScore)
                {
                    bestScore = scores[i];
                    bestIndex = i;
                }
            }

            LoggingController.LogDebug(
                "[QuestScorer] Selected quest index "
                    + bestIndex
                    + " with score "
                    + bestScore.ToString("F3")
                    + " out of "
                    + count
                    + " candidates"
            );
            return bestIndex;
        }
    }
}
