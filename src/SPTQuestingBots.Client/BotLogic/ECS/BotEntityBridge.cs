using System;
using System.Collections.Generic;
using EFT;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.HiveMind;
using SPTQuestingBots.Helpers;
using SPTQuestingBots.Models.Questing;
using SPTQuestingBots.ZoneMovement.Core;
using UnityEngine;

namespace SPTQuestingBots.BotLogic.ECS
{
    /// <summary>
    /// Thin static adapter bridging BotOwner (game type) to BotEntity (ECS data).
    /// Maintains a bidirectional BotOwner ↔ BotEntity mapping and delegates to
    /// BotRegistry / HiveMindSystem for all state mutations.
    ///
    /// Dual-write + read layer: game events flow through here into dense ECS
    /// entities, and read callers use the ECS data instead of dictionary lookups.
    /// </summary>
    public static class BotEntityBridge
    {
        private static readonly BotRegistry _registry = new BotRegistry(64);

        private static readonly Dictionary<BotOwner, BotEntity> _ownerToEntity = new Dictionary<BotOwner, BotEntity>();

        private static readonly Dictionary<int, BotOwner> _entityToOwner = new Dictionary<int, BotOwner>();

        /// <summary>Reusable buffer for GetFollowers(). Callers must not hold references across calls.</summary>
        private static readonly List<BotOwner> _followersBuffer = new List<BotOwner>();

        /// <summary>Reusable buffer for GetAllGroupMembers(). Callers must not hold references across calls.</summary>
        private static readonly List<BotOwner> _groupMembersBuffer = new List<BotOwner>();

        /// <summary>
        /// Phase 5E: ProfileId → BotEntity mapping for patches that only have a string ProfileId
        /// (e.g. CheckLookEnemyPatch). O(1) dictionary lookup replaces O(n) list scan.
        /// </summary>
        private static readonly Dictionary<string, BotEntity> _profileIdToEntity = new Dictionary<string, BotEntity>();

        /// <summary>
        /// Phase 6: Per-entity BotFieldState for zone movement, keyed by entity.Id.
        /// Replaces WorldGridManager.botFieldStates dictionary.
        /// </summary>
        private static readonly Dictionary<int, BotFieldState> _entityFieldStates = new Dictionary<int, BotFieldState>();

        /// <summary>
        /// Phase 8: Per-entity job assignment list, keyed by entity.Id.
        /// Replaces BotJobAssignmentFactory.botJobAssignments dictionary.
        /// </summary>
        private static readonly Dictionary<int, List<BotJobAssignment>> _jobAssignments = new Dictionary<int, List<BotJobAssignment>>();

        /// <summary>Empty list returned when a bot has no job assignments (avoids null).</summary>
        private static readonly List<BotJobAssignment> _emptyAssignments = new List<BotJobAssignment>();

        /// <summary>Dense entity registry for systems that need to iterate all bots.</summary>
        public static BotRegistry Registry => _registry;

        /// <summary>Number of entities currently tracked.</summary>
        public static int Count => _ownerToEntity.Count;

        /// <summary>
        /// Register a newly spawned bot, creating a BotEntity with the correct type.
        /// Call after BotHiveMindMonitor.RegisterBot().
        /// </summary>
        public static BotEntity RegisterBot(BotOwner bot, Controllers.BotType controllerBotType)
        {
            if (_ownerToEntity.TryGetValue(bot, out var existing))
                return existing;

            // Phase 7A: register with BSG ID for O(1) sparse-array lookup
            var entity = _registry.Add(bot.Id);
            entity.BotType = MapBotType(controllerBotType);
            _ownerToEntity[bot] = entity;
            _entityToOwner[entity.Id] = bot;

            // Phase 5E: populate ProfileId mapping for string-based lookups
            var profileId = bot.Profile?.Id;
            if (profileId != null)
                _profileIdToEntity[profileId] = entity;

            // Phase 6: create per-bot field state for zone movement
            if (profileId != null)
            {
                int noiseSeed = profileId.GetHashCode();
                entity.FieldNoiseSeed = noiseSeed;
                entity.HasFieldState = true;
                _entityFieldStates[entity.Id] = new BotFieldState(noiseSeed);
            }

            // Phase 8: create empty job assignment list
            _jobAssignments[entity.Id] = new List<BotJobAssignment>();

            return entity;
        }

        /// <summary>
        /// Try to look up the BotEntity for a given BotOwner.
        /// </summary>
        public static bool TryGetEntity(BotOwner bot, out BotEntity entity)
        {
            if (bot != null)
                return _ownerToEntity.TryGetValue(bot, out entity);

            entity = null;
            return false;
        }

        /// <summary>
        /// Phase 7A: O(1) lookup by BSG integer ID (BotOwner.Id).
        /// Uses sparse-array on BotRegistry — no hash computation.
        /// Returns null if the ID is out of range or the bot was removed.
        /// </summary>
        public static BotEntity GetEntityByBsgId(int bsgId)
        {
            return _registry.GetByBsgId(bsgId);
        }

        /// <summary>
        /// Mark a bot as inactive (dead/despawned).
        /// </summary>
        public static void DeactivateBot(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
            {
                entity.IsActive = false;
            }
        }

        /// <summary>
        /// Sync the sleeping flag when the AI limiter puts a bot to sleep or wakes it.
        /// </summary>
        public static void SetSleeping(BotOwner bot, bool sleeping)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
            {
                entity.IsSleeping = sleeping;
            }
        }

        /// <summary>
        /// Sync a sensor write from BotHiveMindMonitor into the ECS entity.
        /// Accepts the HiveMind sensor type and maps it internally.
        /// </summary>
        public static void UpdateSensor(BotHiveMindSensorType sensorType, BotOwner bot, bool value)
        {
            var ecsSensor = MapSensorType(sensorType);
            if (ecsSensor.HasValue && bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
            {
                entity.SetSensor(ecsSensor.Value, value);
            }
        }

        /// <summary>
        /// Sync a boss-follower relationship into ECS.
        /// </summary>
        public static void SyncBossFollower(BotOwner bot, BotOwner boss)
        {
            if (
                bot != null
                && boss != null
                && _ownerToEntity.TryGetValue(bot, out var followerEntity)
                && _ownerToEntity.TryGetValue(boss, out var bossEntity)
            )
            {
                HiveMindSystem.AssignBoss(followerEntity, bossEntity);
            }
        }

        /// <summary>
        /// Sync group separation into ECS.
        /// </summary>
        public static void SeparateFromGroup(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
            {
                HiveMindSystem.SeparateFromGroup(entity);
            }
        }

        /// <summary>
        /// Sync LastLootingTime into ECS when a bot starts looting.
        /// </summary>
        public static void UpdateLastLootingTime(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
            {
                entity.LastLootingTime = DateTime.Now;
            }
        }

        // ── Reverse Lookup ────────────────────────────────────

        /// <summary>
        /// Get the BotOwner for a given BotEntity via reverse lookup.
        /// Returns null if the entity has no associated BotOwner.
        /// </summary>
        public static BotOwner GetBotOwner(BotEntity entity)
        {
            if (entity != null && _entityToOwner.TryGetValue(entity.Id, out var owner))
                return owner;
            return null;
        }

        // ── Read Methods (replace BotHiveMindMonitor reads) ───

        /// <summary>
        /// Check whether a bot is registered in the ECS.
        /// Replaces BotHiveMindMonitor.IsRegistered().
        /// </summary>
        public static bool IsRegistered(BotOwner bot)
        {
            return bot != null && _ownerToEntity.ContainsKey(bot);
        }

        /// <summary>
        /// Read a sensor value for a specific bot.
        /// Replaces BotHiveMindMonitor.GetValueForBot().
        /// </summary>
        public static bool GetSensorForBot(BotOwner bot, BotSensor sensor)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                return entity.GetSensor(sensor);
            return false;
        }

        /// <summary>
        /// Read a sensor value for the boss of a given bot.
        /// Replaces BotHiveMindMonitor.GetValueForBossOfBot().
        /// </summary>
        public static bool GetSensorForBossOfBot(BotOwner bot, BotSensor sensor)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                return entity.CheckSensorForBoss(sensor);
            return false;
        }

        /// <summary>
        /// Read a sensor value for any group member (boss + all followers).
        /// Replaces BotHiveMindMonitor.GetValueForGroup().
        /// </summary>
        public static bool GetSensorForGroup(BotOwner bot, BotSensor sensor)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                return entity.CheckSensorForGroup(sensor);
            return false;
        }

        /// <summary>
        /// Get the last looting time for the boss of a given bot.
        /// Replaces BotHiveMindMonitor.GetLastLootingTimeForBoss().
        /// </summary>
        public static DateTime GetLastLootingTimeForBoss(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
            {
                var boss = entity.Boss;
                if (boss != null)
                    return boss.LastLootingTime;
            }

            return DateTime.MinValue;
        }

        /// <summary>
        /// Check whether a bot has a boss assigned.
        /// Replaces BotHiveMindMonitor.HasBoss().
        /// </summary>
        public static bool HasBoss(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                return entity.HasBoss;
            return false;
        }

        /// <summary>
        /// Get the BotOwner of a bot's boss.
        /// Replaces BotHiveMindMonitor.GetBoss().
        /// </summary>
        public static BotOwner GetBoss(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity) && entity.Boss != null)
                return GetBotOwner(entity.Boss);
            return null;
        }

        /// <summary>
        /// Get the number of followers for a bot. O(1), zero allocation.
        /// Use this instead of GetFollowers().Count when only the count is needed.
        /// </summary>
        public static int GetFollowerCount(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                return entity.Followers.Count;
            return 0;
        }

        /// <summary>
        /// Get the followers of a bot as a read-only list of BotOwners.
        /// Uses a static reusable buffer — callers must not hold references across calls.
        /// </summary>
        public static IReadOnlyList<BotOwner> GetFollowers(BotOwner bot)
        {
            _followersBuffer.Clear();

            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
            {
                for (int i = 0; i < entity.Followers.Count; i++)
                {
                    var owner = GetBotOwner(entity.Followers[i]);
                    if (owner != null)
                        _followersBuffer.Add(owner);
                }
            }

            return _followersBuffer;
        }

        /// <summary>
        /// Get all group members (boss + followers) excluding the given bot.
        /// Uses a static reusable buffer — callers must not hold references across calls.
        /// </summary>
        public static IReadOnlyList<BotOwner> GetAllGroupMembers(BotOwner bot)
        {
            _groupMembersBuffer.Clear();

            if (bot == null || !_ownerToEntity.TryGetValue(bot, out var entity))
                return _groupMembersBuffer;

            var groupBoss = entity.Boss ?? entity;
            var bossOwner = GetBotOwner(groupBoss);

            for (int i = 0; i < groupBoss.Followers.Count; i++)
            {
                var followerOwner = GetBotOwner(groupBoss.Followers[i]);
                if (followerOwner != null && followerOwner != bot)
                    _groupMembersBuffer.Add(followerOwner);
            }

            if (bossOwner != null && bossOwner != bot)
                _groupMembersBuffer.Add(bossOwner);

            return _groupMembersBuffer;
        }

        /// <summary>
        /// Get the active brain layer name of a bot's boss.
        /// Replaces BotHiveMindMonitor.GetActiveBrainLayerOfBoss().
        /// </summary>
        public static string GetActiveBrainLayerOfBoss(BotOwner bot)
        {
            if (bot == null || !_ownerToEntity.TryGetValue(bot, out var entity) || entity.Boss == null)
                return null;

            var bossOwner = GetBotOwner(entity.Boss);
            if (bossOwner == null || bossOwner.IsDead)
                return null;

            return bossOwner.GetActiveLayerTypeName();
        }

        /// <summary>
        /// Get the distance from a bot to its boss.
        /// Replaces BotHiveMindMonitor.GetDistanceToBoss().
        /// </summary>
        public static float GetDistanceToBoss(BotOwner bot)
        {
            if (bot == null || !_ownerToEntity.TryGetValue(bot, out var entity) || entity.Boss == null)
                return 0f;

            var bossOwner = GetBotOwner(entity.Boss);
            if (bossOwner == null)
                return 0f;

            return Vector3.Distance(bot.Position, bossOwner.Position);
        }

        /// <summary>
        /// Get the position of a bot's boss.
        /// Replaces BotHiveMindMonitor.GetLocationOfBoss().
        /// </summary>
        public static Vector3? GetLocationOfBoss(BotOwner bot)
        {
            if (bot == null || !_ownerToEntity.TryGetValue(bot, out var entity) || entity.Boss == null)
                return null;

            var bossOwner = GetBotOwner(entity.Boss);
            return bossOwner?.Position;
        }

        /// <summary>
        /// Get the position of the nearest group member. Zero allocation — iterates
        /// boss/follower references directly on the entity without intermediate collections.
        /// </summary>
        public static Vector3 GetLocationOfNearestGroupMember(BotOwner bot)
        {
            if (bot == null || !_ownerToEntity.TryGetValue(bot, out var entity))
                return bot.Position;

            var groupBoss = entity.Boss ?? entity;
            float nearestDist = float.MaxValue;
            Vector3 nearestPos = bot.Position;

            // Check boss
            var bossOwner = GetBotOwner(groupBoss);
            if (bossOwner != null && bossOwner != bot)
            {
                float dist = Vector3.Distance(bot.Position, bossOwner.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestPos = bossOwner.Position;
                }
            }

            // Check each follower
            for (int i = 0; i < groupBoss.Followers.Count; i++)
            {
                var followerOwner = GetBotOwner(groupBoss.Followers[i]);
                if (followerOwner != null && followerOwner != bot)
                {
                    float dist = Vector3.Distance(bot.Position, followerOwner.Position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestPos = followerOwner.Position;
                    }
                }
            }

            return nearestPos;
        }

        /// <summary>
        /// Check whether a bot has any followers.
        /// Replaces BotHiveMindMonitor.HasFollowers().
        /// </summary>
        public static bool HasFollowers(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                return entity.HasFollowers;
            return false;
        }

        /// <summary>
        /// Clear all entity data. Called at raid end from BotsControllerStopPatch.
        /// </summary>
        public static void Clear()
        {
            _ownerToEntity.Clear();
            _entityToOwner.Clear();
            _profileIdToEntity.Clear();
            _entityFieldStates.Clear();
            _jobAssignments.Clear();
            _registry.Clear();
        }

        // ── Phase 5E: Read shortcuts replacing BotRegistrationManager ──

        /// <summary>
        /// Check if a bot is sleeping by ProfileId (string). O(1) dictionary lookup.
        /// Replaces BotRegistrationManager.IsBotSleeping() O(n) list scan.
        /// </summary>
        public static bool IsBotSleeping(string profileId)
        {
            if (profileId != null && _profileIdToEntity.TryGetValue(profileId, out var entity))
                return entity.IsSleeping;
            return false;
        }

        /// <summary>
        /// Check if a bot is a PMC via ECS entity type.
        /// Replaces BotRegistrationManager.IsBotAPMC() HashSet lookup.
        /// </summary>
        public static bool IsBotAPMC(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                return entity.BotType == BotType.PMC;
            return false;
        }

        /// <summary>
        /// Get the bot type from ECS entity.
        /// Replaces BotRegistrationManager.GetBotType() multi-collection lookup.
        /// </summary>
        public static Controllers.BotType GetBotType(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                return MapBotTypeReverse(entity.BotType);
            return Controllers.BotType.Undetermined;
        }

        // ── Phase 6: Field State Access ──────────────────────────

        /// <summary>
        /// Get the BotFieldState for a given BotOwner.
        /// Replaces WorldGridManager.GetOrCreateBotState() dictionary lookup.
        /// </summary>
        public static BotFieldState GetFieldState(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                return _entityFieldStates.TryGetValue(entity.Id, out var state) ? state : null;
            return null;
        }

        /// <summary>
        /// Get the BotFieldState for a given profile ID string.
        /// Replaces WorldGridManager.GetOrCreateBotState(profileId) dictionary lookup.
        /// </summary>
        public static BotFieldState GetFieldState(string profileId)
        {
            if (profileId != null && _profileIdToEntity.TryGetValue(profileId, out var entity))
                return _entityFieldStates.TryGetValue(entity.Id, out var state) ? state : null;
            return null;
        }

        // ── Movement State Access ────────────────────────────────

        /// <summary>
        /// Check if a bot's custom mover is active by profile ID. O(1) lookup.
        /// Used by BotMoverFixedUpdatePatch to conditionally skip BSG's ManualFixedUpdate.
        /// </summary>
        public static bool IsCustomMoverActive(string profileId)
        {
            if (profileId != null && _profileIdToEntity.TryGetValue(profileId, out var entity))
                return entity.Movement.IsCustomMoverActive;
            return false;
        }

        /// <summary>
        /// Activate the custom mover for a bot (disables BSG's BotMover via patch).
        /// </summary>
        public static void ActivateCustomMover(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
            {
                entity.Movement.IsCustomMoverActive = true;
            }
        }

        /// <summary>
        /// Deactivate the custom mover for a bot and reset movement state.
        /// BSG's BotMover will resume via patch.
        /// </summary>
        public static void DeactivateCustomMover(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
            {
                entity.Movement.Reset();
            }
        }

        /// <summary>
        /// Get the BotEntity for a profile ID for direct movement state access.
        /// Used by CustomMoverController for per-frame movement state updates.
        /// Returns null if the bot is not registered.
        /// </summary>
        public static BotEntity GetEntityByProfileId(string profileId)
        {
            if (profileId != null && _profileIdToEntity.TryGetValue(profileId, out var entity))
                return entity;
            return null;
        }

        // ── Phase 8: Job Assignment Access ──────────────────────

        /// <summary>
        /// Get the job assignment list for a bot by BotOwner.
        /// Returns an empty list if the bot is not registered (safe fallback).
        /// </summary>
        public static List<BotJobAssignment> GetJobAssignments(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                return _jobAssignments.TryGetValue(entity.Id, out var list) ? list : _emptyAssignments;
            return _emptyAssignments;
        }

        /// <summary>
        /// Get the job assignment list for a bot by profile ID string.
        /// Returns an empty list if the bot is not registered (safe fallback).
        /// </summary>
        public static List<BotJobAssignment> GetJobAssignments(string profileId)
        {
            if (profileId != null && _profileIdToEntity.TryGetValue(profileId, out var entity))
                return _jobAssignments.TryGetValue(entity.Id, out var list) ? list : _emptyAssignments;
            return _emptyAssignments;
        }

        /// <summary>
        /// Get the job assignment list for a bot by profile ID, creating it if necessary.
        /// This handles edge cases where bots access job assignments before full ECS registration.
        /// </summary>
        public static List<BotJobAssignment> EnsureJobAssignments(string profileId)
        {
            if (profileId != null && _profileIdToEntity.TryGetValue(profileId, out var entity))
            {
                if (!_jobAssignments.TryGetValue(entity.Id, out var list))
                {
                    list = new List<BotJobAssignment>();
                    _jobAssignments[entity.Id] = list;
                }

                return list;
            }

            return _emptyAssignments;
        }

        /// <summary>
        /// Check whether a bot has any job assignments by profile ID.
        /// </summary>
        public static bool HasJobAssignments(string profileId)
        {
            if (profileId != null && _profileIdToEntity.TryGetValue(profileId, out var entity))
                return _jobAssignments.TryGetValue(entity.Id, out var list) && list.Count > 0;
            return false;
        }

        /// <summary>
        /// Get the ConsecutiveFailedAssignments count from the entity.
        /// O(1) read replacing O(n) reverse scan.
        /// </summary>
        public static int GetConsecutiveFailedAssignments(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                return entity.ConsecutiveFailedAssignments;
            return 0;
        }

        /// <summary>
        /// Increment the consecutive failed assignments counter.
        /// Called after a single assignment fails.
        /// </summary>
        public static void IncrementConsecutiveFailedAssignments(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                entity.ConsecutiveFailedAssignments++;
        }

        /// <summary>
        /// Reset the consecutive failed assignments counter to zero.
        /// Called after an assignment completes successfully.
        /// </summary>
        public static void ResetConsecutiveFailedAssignments(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                entity.ConsecutiveFailedAssignments = 0;
        }

        /// <summary>
        /// Recompute consecutive failed assignments by scanning the assignment list tail.
        /// Called after bulk operations like FailAllJobAssignmentsForBot().
        /// </summary>
        public static void RecomputeConsecutiveFailedAssignments(string profileId)
        {
            if (profileId == null || !_profileIdToEntity.TryGetValue(profileId, out var entity))
                return;

            if (!_jobAssignments.TryGetValue(entity.Id, out var assignments))
            {
                entity.ConsecutiveFailedAssignments = 0;
                return;
            }

            int count = 0;
            for (int i = assignments.Count - 1; i >= 0; i--)
            {
                if (assignments[i].Status != JobAssignmentStatus.Failed)
                    break;
                count++;
            }

            entity.ConsecutiveFailedAssignments = count;
        }

        /// <summary>
        /// Iterate all registered bots' job assignment lists (for debug logging).
        /// Yields (profileId, assignments) pairs.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, List<BotJobAssignment>>> AllJobAssignments()
        {
            foreach (var kvp in _profileIdToEntity)
            {
                if (_jobAssignments.TryGetValue(kvp.Value.Id, out var list))
                    yield return new KeyValuePair<string, List<BotJobAssignment>>(kvp.Key, list);
            }
        }

        // ── Enum Mapping ────────────────────────────────────────

        /// <summary>
        /// Map Controllers.BotType (game enum) to ECS.BotType (pure data enum).
        /// </summary>
        public static BotType MapBotType(Controllers.BotType controllerType)
        {
            switch (controllerType)
            {
                case Controllers.BotType.PMC:
                    return BotType.PMC;
                case Controllers.BotType.Boss:
                    return BotType.Boss;
                case Controllers.BotType.Scav:
                    return BotType.Scav;
                case Controllers.BotType.PScav:
                    return BotType.PScav;
                default:
                    return BotType.Unknown;
            }
        }

        /// <summary>
        /// Map ECS.BotType back to Controllers.BotType (reverse of MapBotType).
        /// </summary>
        public static Controllers.BotType MapBotTypeReverse(BotType ecsType)
        {
            switch (ecsType)
            {
                case BotType.PMC:
                    return Controllers.BotType.PMC;
                case BotType.Boss:
                    return Controllers.BotType.Boss;
                case BotType.Scav:
                    return Controllers.BotType.Scav;
                case BotType.PScav:
                    return Controllers.BotType.PScav;
                default:
                    return Controllers.BotType.Undetermined;
            }
        }

        /// <summary>
        /// Map BotHiveMindSensorType to BotSensor enum.
        /// Returns null for Undefined or unknown sensor types.
        /// </summary>
        public static BotSensor? MapSensorType(BotHiveMindSensorType sensorType)
        {
            switch (sensorType)
            {
                case BotHiveMindSensorType.InCombat:
                    return BotSensor.InCombat;
                case BotHiveMindSensorType.IsSuspicious:
                    return BotSensor.IsSuspicious;
                case BotHiveMindSensorType.CanQuest:
                    return BotSensor.CanQuest;
                case BotHiveMindSensorType.CanSprintToObjective:
                    return BotSensor.CanSprintToObjective;
                case BotHiveMindSensorType.WantsToLoot:
                    return BotSensor.WantsToLoot;
                default:
                    return null;
            }
        }
    }
}
