using System.Runtime.CompilerServices;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Pick the best loot target from scan results.
    /// Pure C# — no Unity or EFT dependencies.
    /// </summary>
    public static class LootTargetSelector
    {
        /// <summary>
        /// Select the best loot target from an array of scan results.
        /// Returns the index of the highest-scoring result, or -1 if none suitable.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SelectBest(
            LootScanResult[] results,
            int count,
            float inventorySpaceFree,
            bool isInCombat,
            float distanceToObjectiveSqr,
            float timeSinceLastLoot,
            LootClaimRegistry claims,
            int botId,
            in LootScoringConfig config
        )
        {
            if (results == null || count <= 0)
                return -1;

            int bestIndex = -1;
            float bestScore = 0f;

            for (int i = 0; i < count; i++)
            {
                if (claims != null && claims.IsClaimedByOther(botId, results[i].Id))
                    continue;

                float score = LootScorer.Score(
                    results[i].Value,
                    results[i].DistanceSqr,
                    inventorySpaceFree,
                    isInCombat,
                    distanceToObjectiveSqr,
                    timeSinceLastLoot,
                    results[i].IsGearUpgrade,
                    config
                );

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }
    }
}
