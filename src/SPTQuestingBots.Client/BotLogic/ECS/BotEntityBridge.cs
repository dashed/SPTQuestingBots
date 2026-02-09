using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EFT;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.HiveMind;
using SPTQuestingBots.Helpers;
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
        /// Get the followers of a bot as a read-only collection of BotOwners.
        /// Replaces BotHiveMindMonitor.GetFollowers().
        /// </summary>
        public static ReadOnlyCollection<BotOwner> GetFollowers(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
            {
                var result = new List<BotOwner>(entity.Followers.Count);
                for (int i = 0; i < entity.Followers.Count; i++)
                {
                    var owner = GetBotOwner(entity.Followers[i]);
                    if (owner != null)
                        result.Add(owner);
                }

                return new ReadOnlyCollection<BotOwner>(result);
            }

            return new ReadOnlyCollection<BotOwner>(Array.Empty<BotOwner>());
        }

        /// <summary>
        /// Get all group members (boss + followers) excluding the given bot.
        /// Replaces BotHiveMindMonitor.GetAllGroupMembers().
        /// </summary>
        public static ReadOnlyCollection<BotOwner> GetAllGroupMembers(BotOwner bot)
        {
            if (bot == null || !_ownerToEntity.TryGetValue(bot, out var entity))
                return new ReadOnlyCollection<BotOwner>(Array.Empty<BotOwner>());

            var groupBoss = entity.Boss ?? entity;
            var bossOwner = GetBotOwner(groupBoss);

            var result = new List<BotOwner>(groupBoss.Followers.Count + 1);
            for (int i = 0; i < groupBoss.Followers.Count; i++)
            {
                var followerOwner = GetBotOwner(groupBoss.Followers[i]);
                if (followerOwner != null && followerOwner != bot)
                    result.Add(followerOwner);
            }

            if (bossOwner != null && bossOwner != bot)
                result.Add(bossOwner);

            return new ReadOnlyCollection<BotOwner>(result);
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
        /// Get the position of the nearest group member.
        /// Replaces BotHiveMindMonitor.GetLocationOfNearestGroupMember().
        /// </summary>
        public static Vector3 GetLocationOfNearestGroupMember(BotOwner bot)
        {
            var members = GetAllGroupMembers(bot);
            if (members.Count == 0)
                return bot.Position;

            BotOwner nearest = null;
            float nearestDist = float.MaxValue;
            for (int i = 0; i < members.Count; i++)
            {
                float dist = Vector3.Distance(bot.Position, members[i].Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = members[i];
                }
            }

            return nearest.Position;
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
