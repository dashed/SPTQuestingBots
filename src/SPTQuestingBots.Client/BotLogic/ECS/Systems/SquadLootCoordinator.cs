using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Squad-aware looting coordination. Boss gets priority on high-value loot,
    /// followers loot during idle windows, scan results are shared within comm range.
    /// Pure C# — no Unity or EFT dependencies.
    /// </summary>
    public static class SquadLootCoordinator
    {
        /// <summary>
        /// Boss claims the highest-value loot target from scan results.
        /// Returns the index of the claimed target, or -1 if none suitable.
        /// </summary>
        /// <param name="results">Scan results buffer.</param>
        /// <param name="count">Number of valid results.</param>
        /// <param name="claims">Claim registry for deconfliction.</param>
        /// <param name="bossEntityId">Boss entity ID for claiming.</param>
        public static int BossPriorityClaim(LootScanResult[] results, int count, LootClaimRegistry claims, int bossEntityId)
        {
            if (count == 0)
                return -1;

            int bestIndex = -1;
            float bestValue = -1f;

            for (int i = 0; i < count; i++)
            {
                if (claims.IsClaimedByOther(bossEntityId, results[i].Id))
                    continue;

                if (results[i].Value > bestValue)
                {
                    bestValue = results[i].Value;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                claims.TryClaim(bossEntityId, results[bestIndex].Id);
                LoggingController.LogInfo(
                    "[SquadLootCoordinator] Boss "
                        + bossEntityId
                        + " claiming loot "
                        + results[bestIndex].Id
                        + " (value="
                        + bestValue.ToString("F0")
                        + ", from "
                        + count
                        + " candidates)"
                );
            }

            return bestIndex;
        }

        /// <summary>
        /// Determines whether a follower is allowed to loot right now based on boss state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldFollowerLoot(BotEntity follower, BotEntity boss, float commRangeSqr)
        {
            // Never loot during combat
            if (follower.IsInCombat || boss.IsInCombat)
                return false;

            // Must be within communication range
            float dx = follower.CurrentPositionX - boss.CurrentPositionX;
            float dz = follower.CurrentPositionZ - boss.CurrentPositionZ;
            float distSqr = dx * dx + dz * dz;
            if (distSqr > commRangeSqr)
            {
                LoggingController.LogDebug(
                    "[SquadLootCoordinator] Follower " + follower.Id + ": denied loot — out of comm range from boss " + boss.Id
                );
                return false;
            }

            // Follower can loot when boss is:
            // 1. Currently looting (idle time)
            if (boss.IsLooting)
            {
                LoggingController.LogDebug(
                    "[SquadLootCoordinator] Follower " + follower.Id + ": allowed to loot — boss " + boss.Id + " is looting"
                );
                return true;
            }

            // 2. At objective and holding/ambushing (waiting phase)
            if (boss.IsCloseToObjective && boss.HasActiveObjective)
            {
                LoggingController.LogDebug(
                    "[SquadLootCoordinator] Follower " + follower.Id + ": allowed to loot — boss " + boss.Id + " at objective"
                );
                return true;
            }

            // 3. Follower has arrived at tactical position
            if (follower.HasTacticalPosition && !follower.IsApproachingLoot)
            {
                float tdx = follower.CurrentPositionX - follower.TacticalPositionX;
                float tdz = follower.CurrentPositionZ - follower.TacticalPositionZ;
                float tactDistSqr = tdx * tdx + tdz * tdz;
                // Within 5m of tactical position
                if (tactDistSqr < 25f)
                {
                    LoggingController.LogDebug(
                        "[SquadLootCoordinator] Follower " + follower.Id + ": allowed to loot — at tactical position"
                    );
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Share boss scan results with followers within communication range.
        /// Writes to the squad's shared loot buffers.
        /// </summary>
        /// <param name="squad">Squad entity to store shared results.</param>
        /// <param name="bossResults">Boss's scan results.</param>
        /// <param name="bossResultCount">Number of valid boss results.</param>
        /// <param name="bossPosition">Boss position for comm range check.</param>
        public static void ShareScanResults(SquadEntity squad, LootScanResult[] bossResults, int bossResultCount)
        {
            int maxShared = squad.SharedLootIds.Length; // 8
            int shareCount = bossResultCount < maxShared ? bossResultCount : maxShared;

            for (int i = 0; i < shareCount; i++)
            {
                squad.SharedLootIds[i] = bossResults[i].Id;
                squad.SharedLootX[i] = bossResults[i].X;
                squad.SharedLootY[i] = bossResults[i].Y;
                squad.SharedLootZ[i] = bossResults[i].Z;
                squad.SharedLootValues[i] = bossResults[i].Value;
                squad.SharedLootTypes[i] = bossResults[i].Type;
            }

            squad.SharedLootCount = shareCount;

            if (shareCount > 0)
            {
                LoggingController.LogDebug(
                    "[SquadLootCoordinator] Shared "
                        + shareCount
                        + " loot targets with squad"
                        + " (from "
                        + bossResultCount
                        + " boss results)"
                );
            }
        }

        /// <summary>
        /// Pick a shared loot target for a follower from the squad's shared results.
        /// Skips already-claimed targets and the boss's own target.
        /// Returns the index in shared buffers, or -1 if none available.
        /// </summary>
        public static int PickSharedTargetForFollower(
            SquadEntity squad,
            int followerEntityId,
            int bossLootTargetId,
            LootClaimRegistry claims
        )
        {
            int bestIndex = -1;
            float bestValue = -1f;

            for (int i = 0; i < squad.SharedLootCount; i++)
            {
                int lootId = squad.SharedLootIds[i];

                // Skip boss's current target
                if (lootId == bossLootTargetId)
                    continue;

                // Skip already claimed by others
                if (claims.IsClaimedByOther(followerEntityId, lootId))
                    continue;

                if (squad.SharedLootValues[i] > bestValue)
                {
                    bestValue = squad.SharedLootValues[i];
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                LoggingController.LogDebug(
                    "[SquadLootCoordinator] Follower "
                        + followerEntityId
                        + " picked shared loot "
                        + squad.SharedLootIds[bestIndex]
                        + " (value="
                        + bestValue.ToString("F0")
                        + ", shared="
                        + squad.SharedLootCount
                        + ")"
                );
            }

            return bestIndex;
        }

        /// <summary>
        /// Clear shared loot state on a squad (e.g., at raid end or squad dissolution).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearSharedLoot(SquadEntity squad)
        {
            squad.SharedLootCount = 0;
        }
    }
}
