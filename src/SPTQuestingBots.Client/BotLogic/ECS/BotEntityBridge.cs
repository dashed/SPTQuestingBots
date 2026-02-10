using System;
using System.Collections.Generic;
using EFT;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.HiveMind;
using SPTQuestingBots.Controllers;
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

        /// <summary>Squad registry mapping BSG group IDs to squad entities.</summary>
        private static readonly SquadRegistry _squadRegistry = new SquadRegistry();

        /// <summary>
        /// Raid-scoped loot claim registry for deconflicting loot targets across bots.
        /// One instance per raid, cleared at raid end.
        /// </summary>
        private static readonly LootClaimRegistry _lootClaims = new LootClaimRegistry();

        /// <summary>Public accessor for squad registry (used by strategy manager).</summary>
        public static SquadRegistry SquadRegistry => _squadRegistry;

        /// <summary>Public accessor for the raid-scoped loot claim registry.</summary>
        public static LootClaimRegistry LootClaims => _lootClaims;

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

            LoggingController.LogInfo(
                "[BotEntityBridge] Registered entity "
                    + entity.Id
                    + " for bot "
                    + bot.GetText()
                    + " (type="
                    + controllerBotType
                    + ", bsgId="
                    + bot.Id
                    + ", count="
                    + _ownerToEntity.Count
                    + ")"
            );
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
                LoggingController.LogInfo("[BotEntityBridge] Deactivating entity " + entity.Id + " for bot " + bot.GetText());
                entity.IsActive = false;

                // Release loot claims on deactivation (death/despawn)
                if (entity.HasLootTarget)
                {
                    LoggingController.LogDebug("[BotEntityBridge] Releasing loot claims for deactivated entity " + entity.Id);
                    _lootClaims.ReleaseAll(entity.Id);
                    entity.HasLootTarget = false;
                    entity.IsLooting = false;
                    entity.IsApproachingLoot = false;
                }
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
                LoggingController.LogDebug(
                    "[BotEntityBridge] Syncing boss-follower: "
                        + bot.GetText()
                        + " (entity "
                        + followerEntity.Id
                        + ") -> boss "
                        + boss.GetText()
                        + " (entity "
                        + bossEntity.Id
                        + ")"
                );
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
                LoggingController.LogInfo("[BotEntityBridge] Separating " + bot.GetText() + " (entity " + entity.Id + ") from group");
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

        /// <summary>
        /// Try-pattern accessor for entity → BotOwner reverse lookup.
        /// Returns false if the entity is null or has no mapped BotOwner.
        /// </summary>
        public static bool TryGetBotOwner(BotEntity entity, out BotOwner owner)
        {
            if (entity != null && _entityToOwner.TryGetValue(entity.Id, out owner))
                return true;

            owner = null;
            return false;
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

        // ── Squad Methods ────────────────────────────────────────

        /// <summary>
        /// Get or create a squad for a BSG group, adding the boss as leader.
        /// </summary>
        public static SquadEntity RegisterSquad(BotEntity boss, int bsgGroupId)
        {
            if (boss == null)
            {
                LoggingController.LogWarning("[BotEntityBridge] RegisterSquad called with null boss");
                return null;
            }
            var squad = _squadRegistry.GetOrCreate(bsgGroupId, 1, 6); // 1 strategy for now
            if (squad.Leader == null)
            {
                _squadRegistry.AddMember(squad, boss);
                LoggingController.LogInfo("[BotEntityBridge] Registered squad for group " + bsgGroupId + " with leader entity " + boss.Id);
            }
            return squad;
        }

        /// <summary>
        /// Add a follower bot to its boss's squad.
        /// </summary>
        public static void AddToSquad(BotEntity follower, BotEntity boss)
        {
            if (follower == null || boss == null || boss.Squad == null)
                return;
            _squadRegistry.AddMember(boss.Squad, follower);
            LoggingController.LogDebug(
                "[BotEntityBridge] Added entity "
                    + follower.Id
                    + " to squad of boss entity "
                    + boss.Id
                    + " (members="
                    + boss.Squad.Members.Count
                    + ")"
            );
        }

        /// <summary>
        /// Remove a bot from its squad.
        /// </summary>
        public static void RemoveFromSquad(BotEntity member)
        {
            if (member == null || member.Squad == null)
                return;
            LoggingController.LogDebug("[BotEntityBridge] Removing entity " + member.Id + " from squad");
            _squadRegistry.RemoveMember(member.Squad, member);
        }

        /// <summary>
        /// Check if a bot has a tactical position assigned.
        /// </summary>
        public static bool HasTacticalPosition(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
                return entity.HasTacticalPosition;
            return false;
        }

        /// <summary>
        /// Sync bot's current world position into entity fields for pure-logic access.
        /// </summary>
        public static void SyncPosition(BotOwner bot)
        {
            if (bot != null && _ownerToEntity.TryGetValue(bot, out var entity))
            {
                var pos = bot.Position;
                entity.CurrentPositionX = pos.x;
                entity.CurrentPositionY = pos.y;
                entity.CurrentPositionZ = pos.z;
            }
        }

        /// <summary>
        /// Sync the boss's current quest objective position into the squad's shared objective.
        /// Call before SquadStrategyManager.Update().
        /// </summary>
        public static void SyncSquadObjective(BotOwner boss)
        {
            if (boss == null || !_ownerToEntity.TryGetValue(boss, out var entity))
                return;
            if (entity.Squad == null || entity.Squad.Leader != entity)
                return;

            var objectiveManager = boss.GetObjectiveManager();
            if (objectiveManager == null || !objectiveManager.IsJobAssignmentActive)
                return;

            var obj = entity.Squad.Objective;
            var targetPos = objectiveManager.Position;
            if (targetPos.HasValue)
            {
                var pos = targetPos.Value;
                if (!obj.HasObjective || obj.ObjectiveX != pos.x || obj.ObjectiveY != pos.y || obj.ObjectiveZ != pos.z)
                {
                    obj.SetObjective(pos.x, pos.y, pos.z);
                    LoggingController.LogDebug(
                        "[BotEntityBridge] Squad objective updated for entity "
                            + entity.Id
                            + " to ("
                            + pos.x.ToString("F1")
                            + ", "
                            + pos.y.ToString("F1")
                            + ", "
                            + pos.z.ToString("F1")
                            + ")"
                    );
                }
            }
        }

        /// <summary>
        /// Clear all entity data. Called at raid end from BotsControllerStopPatch.
        /// </summary>
        public static void Clear()
        {
            int entityCount = _ownerToEntity.Count;
            LoggingController.LogInfo("[BotEntityBridge] Clearing all entity data (count=" + entityCount + ")");
            _ownerToEntity.Clear();
            _entityToOwner.Clear();
            _profileIdToEntity.Clear();
            _entityFieldStates.Clear();
            _jobAssignments.Clear();
            _lootClaims.Clear();
            _squadRegistry.Clear();
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

        // ── Quest State Sync (for Utility AI) ────────────────────

        /// <summary>
        /// Sync quest state from <c>BotObjectiveManager</c> into the <c>BotEntity</c> fields
        /// used by utility task scoring. Call before <c>UtilityTaskManager.ScoreAndPick()</c>.
        /// </summary>
        public static void SyncQuestState(BotOwner botOwner)
        {
            if (botOwner == null || !_ownerToEntity.TryGetValue(botOwner, out var entity))
                return;

            var objectiveManager = botOwner.GetObjectiveManager();
            if (objectiveManager == null)
                return;

            int previousAction = entity.CurrentQuestAction;
            bool previousHasActiveObjective = entity.HasActiveObjective;
            entity.CurrentQuestAction = (int)objectiveManager.CurrentQuestAction;
            entity.DistanceToObjective = objectiveManager.DistanceToObjective;
            entity.IsCloseToObjective = objectiveManager.IsCloseToObjective();
            entity.MustUnlockDoor = objectiveManager.MustUnlockDoor;
            entity.HasActiveObjective = objectiveManager.IsJobAssignmentActive;

            // Sync game time for linger scoring
            entity.CurrentGameTime = UnityEngine.Time.time;

            // Track objective completion: true→false transition triggers linger
            if (previousHasActiveObjective && !entity.HasActiveObjective && !entity.IsLingering)
            {
                entity.ObjectiveCompletedTime = UnityEngine.Time.time;
                var lingerConfig = Controllers.ConfigController.Config?.Questing?.Linger;
                float durationMin = lingerConfig?.DurationMin ?? 10f;
                float durationMax = lingerConfig?.DurationMax ?? 30f;
                entity.LingerDuration = durationMin + (float)(new System.Random().NextDouble() * (durationMax - durationMin));
                LoggingController.LogDebug(
                    "[BotEntityBridge] Entity "
                        + entity.Id
                        + " objective completed, linger duration="
                        + entity.LingerDuration.ToString("F1")
                        + "s"
                );
            }

            if (previousAction != entity.CurrentQuestAction)
            {
                LoggingController.LogDebug(
                    "[BotEntityBridge] Entity "
                        + entity.Id
                        + " quest action changed from "
                        + previousAction
                        + " to "
                        + entity.CurrentQuestAction
                );
            }

            // Sync inventory space for loot scoring
            try
            {
                entity.InventorySpaceFree = Helpers.InventorySpaceHelper.ComputeFreeSlots(botOwner);
            }
            catch
            {
                // Inventory access can fail during bot initialization
            }
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
                LoggingController.LogDebug("[BotEntityBridge] Activated custom mover for entity " + entity.Id);
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
                LoggingController.LogDebug("[BotEntityBridge] Deactivating custom mover for entity " + entity.Id);
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
                    LoggingController.LogDebug("[BotEntityBridge] Created job assignment list for entity " + entity.Id);
                }

                return list;
            }

            LoggingController.LogWarning("[BotEntityBridge] EnsureJobAssignments called for unknown profileId");
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
            {
                entity.ConsecutiveFailedAssignments++;
                LoggingController.LogDebug(
                    "[BotEntityBridge] Entity " + entity.Id + " consecutive failed assignments: " + entity.ConsecutiveFailedAssignments
                );
            }
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

        // ── Earpiece + Personality Sync ───────────────────────────

        /// <summary>
        /// Sync earpiece equipment status from BotOwner to BotEntity.
        /// </summary>
        public static void SyncEarPiece(BotOwner bot)
        {
            if (bot == null || !_ownerToEntity.TryGetValue(bot, out var entity))
                return;
            try
            {
                var player = bot.GetPlayer;
                if (player?.Equipment != null)
                {
                    var slot = player.Equipment.GetSlot(EFT.InventoryLogic.EquipmentSlot.Earpiece);
                    entity.HasEarPiece = slot?.ContainedItem != null;
                    LoggingController.LogDebug("[BotEntityBridge] Entity " + entity.Id + " earpiece=" + entity.HasEarPiece);
                }
            }
            catch
            {
                entity.HasEarPiece = false;
                LoggingController.LogWarning("[BotEntityBridge] Failed to sync earpiece for entity " + entity.Id);
            }
        }

        /// <summary>Reusable buffer for personality computation.</summary>
        private static readonly BotType[] _personalityBuffer = new BotType[6];

        /// <summary>
        /// Compute and assign squad personality from member BotType distribution.
        /// </summary>
        public static void ComputeSquadPersonality(SquadEntity squad)
        {
            if (squad == null || squad.Members.Count == 0)
                return;
            LoggingController.LogDebug("[BotEntityBridge] Computing squad personality for " + squad.Members.Count + " members");
            int count = Math.Min(squad.Members.Count, _personalityBuffer.Length);
            for (int i = 0; i < count; i++)
                _personalityBuffer[i] = squad.Members[i].BotType;

            squad.PersonalityType = Systems.SquadPersonalityCalculator.DeterminePersonality(_personalityBuffer, count);
            var settings = SquadPersonalitySettings.ForType(squad.PersonalityType);
            squad.CoordinationLevel = settings.CoordinationLevel;
            squad.AggressionLevel = settings.AggressionLevel;
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

        // ── Loot Wiring ──────────────────────────────────────────────

        /// <summary>
        /// Set a loot target on a bot entity. Claims the target in the registry.
        /// </summary>
        public static void SetLootTarget(BotOwner bot, int lootId, float x, float y, float z, byte type, float value)
        {
            if (!_ownerToEntity.TryGetValue(bot, out var entity))
                return;

            entity.HasLootTarget = true;
            entity.LootTargetId = lootId;
            entity.LootTargetX = x;
            entity.LootTargetY = y;
            entity.LootTargetZ = z;
            entity.LootTargetType = type;
            entity.LootTargetValue = value;

            _lootClaims.TryClaim(entity.Id, lootId);
            LoggingController.LogDebug(
                "[BotEntityBridge] Set loot target for entity "
                    + entity.Id
                    + " (lootId="
                    + lootId
                    + ", value="
                    + value
                    + ", type="
                    + type
                    + ")"
            );
        }

        /// <summary>
        /// Clear a bot's loot target and release the claim.
        /// </summary>
        public static void ClearLootTarget(BotOwner bot)
        {
            if (!_ownerToEntity.TryGetValue(bot, out var entity))
                return;

            if (entity.HasLootTarget)
            {
                LoggingController.LogDebug(
                    "[BotEntityBridge] Clearing loot target for entity " + entity.Id + " (lootId=" + entity.LootTargetId + ")"
                );
                _lootClaims.Release(entity.Id, entity.LootTargetId);
            }

            entity.HasLootTarget = false;
            entity.LootTargetId = 0;
            entity.LootTargetX = 0f;
            entity.LootTargetY = 0f;
            entity.LootTargetZ = 0f;
            entity.LootTargetType = LootTargetType.None;
            entity.LootTargetValue = 0f;
            entity.IsLooting = false;
            entity.IsApproachingLoot = false;
        }

        /// <summary>
        /// Release all loot claims for a bot (call on bot death/despawn).
        /// </summary>
        public static void ReleaseLootClaims(BotOwner bot)
        {
            if (_ownerToEntity.TryGetValue(bot, out var entity))
            {
                LoggingController.LogDebug("[BotEntityBridge] Releasing all loot claims for entity " + entity.Id);
                _lootClaims.ReleaseAll(entity.Id);
                entity.HasLootTarget = false;
                entity.IsLooting = false;
                entity.IsApproachingLoot = false;
            }
        }

        /// <summary>
        /// Build a LootScoringConfig from the current LootingConfig settings.
        /// </summary>
        public static LootScoringConfig BuildScoringConfig()
        {
            var looting = Controllers.ConfigController.Config?.Questing?.Looting;
            if (looting == null)
            {
                return new LootScoringConfig(5000f, 50000f, 0.001f, 0.15f, 0.3f, 15f);
            }

            return new LootScoringConfig(
                looting.MinItemValue,
                looting.ValueScoreCap,
                looting.DistancePenaltyFactor,
                looting.QuestProximityBonus,
                looting.GearUpgradeScoreBonus,
                looting.LootCooldownSeconds
            );
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
