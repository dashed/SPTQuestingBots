using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Static system methods for HiveMind logic operating on BotEntity lists.
    /// Follows Phobos pattern: static methods iterate dense entity lists, read/write embedded state.
    /// Pure C# — no Unity or EFT dependencies — fully testable in net9.0.
    /// </summary>
    public static class HiveMindSystem
    {
        // ── Sensor Lifecycle ────────────────────────────────────

        /// <summary>
        /// Reset all sensor booleans to their defaults for entities that are no longer active.
        /// Mirrors BotHiveMindAbstractSensor.Update() default-value reset.
        /// </summary>
        public static void ResetInactiveEntitySensors(List<BotEntity> entities)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.IsActive)
                    continue;

                entity.IsInCombat = false;
                entity.IsSuspicious = false;
                entity.CanQuest = false;
                entity.CanSprintToObjective = true; // default is true
                entity.WantsToLoot = false;
            }
        }

        // ── Boss / Follower Lifecycle ───────────────────────────

        /// <summary>
        /// Clean up boss/follower references for inactive entities.
        /// If a boss becomes inactive, its followers lose their boss reference.
        /// If a follower becomes inactive, it is removed from its boss's follower list.
        /// Mirrors BotHiveMindMonitor.updateBossFollowers() dead-bot cleanup.
        /// </summary>
        public static void CleanupDeadEntities(List<BotEntity> entities)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.IsActive)
                    continue;

                // If this dead entity was a boss, detach all its followers
                for (int j = entity.Followers.Count - 1; j >= 0; j--)
                {
                    entity.Followers[j].Boss = null;
                }

                entity.Followers.Clear();

                // If this dead entity was a follower, detach from its boss
                if (entity.Boss != null)
                {
                    entity.Boss.Followers.Remove(entity);
                    entity.Boss = null;
                }
            }
        }

        /// <summary>
        /// Establish a bidirectional boss-follower relationship.
        /// If the follower already has a different boss, detaches from the old boss first.
        /// </summary>
        public static void AssignBoss(BotEntity follower, BotEntity boss)
        {
            if (follower == null || boss == null || follower == boss)
                return;

            // Detach from old boss if switching bosses
            if (follower.Boss != null && follower.Boss != boss)
            {
                follower.Boss.Followers.Remove(follower);
            }

            follower.Boss = boss;

            if (!boss.Followers.Contains(follower))
            {
                boss.Followers.Add(follower);
            }
        }

        /// <summary>
        /// Remove the boss-follower relationship for a follower.
        /// </summary>
        public static void RemoveBoss(BotEntity follower)
        {
            if (follower?.Boss == null)
                return;

            follower.Boss.Followers.Remove(follower);
            follower.Boss = null;
        }

        /// <summary>
        /// Completely separate an entity from its group.
        /// Removes all boss and follower references bidirectionally.
        /// Mirrors the pure data part of BotHiveMindMonitor.SeparateBotFromGroup().
        /// </summary>
        public static void SeparateFromGroup(BotEntity entity)
        {
            if (entity == null)
                return;

            // If this entity is a follower, detach from boss
            if (entity.Boss != null)
            {
                entity.Boss.Followers.Remove(entity);
                entity.Boss = null;
            }

            // If this entity is a boss, detach all followers
            for (int i = entity.Followers.Count - 1; i >= 0; i--)
            {
                entity.Followers[i].Boss = null;
            }

            entity.Followers.Clear();
        }

        // ── Movement State ─────────────────────────────────────

        /// <summary>
        /// Reset movement state for inactive entities.
        /// Ensures dead/removed bots don't appear as "following" or "stuck".
        /// </summary>
        public static void ResetMovementForInactiveEntities(List<BotEntity> entities)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.IsActive)
                    continue;

                entity.Movement.Reset();
            }
        }

        /// <summary>
        /// Count entities by path-follow status (dense iteration).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountByMovementStatus(List<BotEntity> entities, PathFollowStatus status)
        {
            int count = 0;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].IsActive && entities[i].Movement.Status == status)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Count active entities that are currently stuck (any phase).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountStuckBots(List<BotEntity> entities)
        {
            int count = 0;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].IsActive && entities[i].Movement.StuckStatus != StuckPhase.None)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Count active entities that are currently sprinting.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountSprintingBots(List<BotEntity> entities)
        {
            int count = 0;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].IsActive && entities[i].Movement.IsSprinting)
                    count++;
            }

            return count;
        }

        // ── Counting Queries ────────────────────────────────────

        /// <summary>
        /// Count active, non-sleeping entities.
        /// Replaces the O(n²) NumberOfActiveBots() pattern with O(n) dense iteration.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountActive(List<BotEntity> entities)
        {
            int count = 0;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].IsActive && !entities[i].IsSleeping)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Count active, non-sleeping entities of a specific type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountActiveByType(List<BotEntity> entities, BotType type)
        {
            int count = 0;
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e.IsActive && !e.IsSleeping && e.BotType == type)
                    count++;
            }

            return count;
        }
    }
}
