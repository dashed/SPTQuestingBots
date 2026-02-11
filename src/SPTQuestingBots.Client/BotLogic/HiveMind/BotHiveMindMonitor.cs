using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using SPTQuestingBots.BehaviorExtensions;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ExternalMods;
using SPTQuestingBots.Components.Spawning;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Helpers;
using UnityEngine;

namespace SPTQuestingBots.BotLogic.HiveMind
{
    public enum BotHiveMindSensorType
    {
        Undefined,
        InCombat,
        IsSuspicious,
        CanQuest,
        CanSprintToObjective,
        WantsToLoot,
    }

    public class BotHiveMindMonitor : MonoBehaviourDelayedUpdate
    {
        private ECS.UtilityAI.SquadStrategyManager _squadStrategyManager;

        public BotHiveMindMonitor()
        {
            UpdateInterval = 50;
        }

        public static void Clear()
        {
            ECS.BotEntityBridge.Clear();
            HumanPlayerCache.Clear();
            CombatEventRegistry.Clear();
            GrenadeExplosionSubscriber.Clear();
            _lastScanTime.Clear();
        }

        /// <summary>Scratch buffers for refreshing HumanPlayerCache.</summary>
        private static readonly float[] _humanX = new float[HumanPlayerCache.MaxPlayers];
        private static readonly float[] _humanY = new float[HumanPlayerCache.MaxPlayers];
        private static readonly float[] _humanZ = new float[HumanPlayerCache.MaxPlayers];

        /// <summary>
        /// Deterministic tick order (50ms interval, Phobos-inspired):
        ///   1. updateBosses()              — discover/validate boss relationships from BSG API
        ///   2. updateBossFollowers()        — cleanup dead boss/follower references (CleanupDeadEntities)
        ///   3. updatePullSensors()          — CanQuest + CanSprintToObjective via dense ECS iteration
        ///   4. ResetInactiveEntitySensors() — clear sensor state on dead/despawned entities
        ///   5. updateSquadStrategies()      — squad lifecycle, tactical positions, formations
        ///   6. updateCombatEvents()         — cleanup expired events + per-bot combat event queries
        ///   7. updateLootScanning()         — loot target selection + squad coordination
        ///   8. refreshHumanPlayerCache()    — snapshot human positions for LOD + SleepingLayer
        ///   9. updateLodTiers()             — compute LOD tier + increment frame counter per entity
        ///
        /// Push sensors (InCombat, IsSuspicious, WantsToLoot) are event-driven via
        /// <see cref="UpdateValueForBot"/> and do not participate in the tick.
        /// </summary>
        protected void Update()
        {
            if (!canUpdate())
            {
                return;
            }

            var locationData = Singleton<GameWorld>.Instance?.GetComponent<Components.LocationData>();
            if (locationData?.CurrentLocation == null)
            {
                LoggingController.LogWarning("BotHiveMindMonitor: LocationData or CurrentLocation is null, skipping update");
                Clear();
                return;
            }

            // 1. Discover/validate boss relationships from BSG API
            updateBosses();

            // 2. Cleanup dead boss/follower references in ECS
            updateBossFollowers();

            // 3. Pull sensors: iterate dense ECS entity list (zero delegate allocation)
            updatePullSensors();

            // 4. Reset sensors for inactive (dead/despawned) entities
            ECS.Systems.HiveMindSystem.ResetInactiveEntitySensors(ECS.BotEntityBridge.Registry.Entities);

            // 5. Update squad strategies (position sync, objective sync, tactical positions)
            updateSquadStrategies();

            // 6. Cleanup expired combat events + per-bot combat event queries
            updateCombatEvents();

            // 7. Loot target selection + squad coordination
            updateLootScanning();

            // 8. Refresh human player position cache (used by LOD + SleepingLayer)
            refreshHumanPlayerCache();

            // 9. Compute LOD tiers for all active entities
            updateLodTiers();
        }

        public static void UpdateValueForBot(BotHiveMindSensorType sensorType, BotOwner bot, bool value)
        {
            ECS.BotEntityBridge.UpdateSensor(sensorType, bot, value);

            if (sensorType == BotHiveMindSensorType.WantsToLoot && value)
            {
                ECS.BotEntityBridge.UpdateLastLootingTime(bot);
            }
        }

        public static void SeparateBotFromGroup(BotOwner bot)
        {
            // Not necessary if the bot is solo
            if (bot.BotsGroup.MembersCount <= 1)
            {
                return;
            }

            Controllers.LoggingController.LogInfo("Separating " + bot.GetText() + " from its group...");

            // Sync group separation into ECS data layer
            ECS.BotEntityBridge.SeparateFromGroup(bot);

            // If the bot was spawned by this mod, create a new spawn group for it
            if (BotGenerator.TryGetBotGroupFromAnyGenerator(bot, out Models.BotSpawnInfo matchingGroupData))
            {
                matchingGroupData.SeparateBotOwner(bot);
            }

            // Check if the bot is the boss of its group
            bool isBoss = false;
            if (bot.BotFollower?.HaveBoss == true)
            {
                bot.BotFollower.BossToFollow.RemoveFollower(bot);
                bot.BotFollower.BossToFollow = null;
            }
            else if (bot.Boss.HaveFollowers() && (bot.BotsGroup.BossGroup != null))
            {
                isBoss = true;
            }

            // If the bot is a boss, instruct its followers to follow a new boss
            bot.Boss.RemoveFollower(bot);
            if (isBoss && (bot.Boss.Followers.Count >= 1))
            {
                bot.BotsGroup.BossGroup = null;

                foreach (BotOwner follower in bot.Boss.Followers)
                {
                    follower.BotFollower.BossToFollow = null;
                }

                // Setting a new boss is only required for groups that have more than 2 bots
                if (bot.Boss.Followers.Count > 1)
                {
                    BotOwner newBoss = bot.Boss.Followers.RandomElement();
                    newBoss.Boss.SetBoss(bot.Boss.Followers.Count);

                    LoggingController.LogInfo(
                        "Selected a new boss for " + bot.Boss.Followers.Count + " followers: " + bot.BotsGroup.BossGroup.Boss.GetText()
                    );
                }
            }

            // Dissociate the bot from its group
            BotsGroup currentGroup = bot.BotsGroup;

            // Create a new bot group for the bot
            BotSpawner botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
            BotZone closestBotZone = botSpawnerClass.GetClosestZone(bot.Position, out float dist);
            BotsGroup newGroup = BotGroupHelpers.CreateGroup(bot, closestBotZone, 1);
            bot.BotsGroup = newGroup;
            newGroup.Lock();

            currentGroup.Members.Remove(bot);

            // Make the bot's old group members friendly
            List<BotOwner> oldGroupMembers = SPT.Custom.CustomAI.AIExtensions.GetAllMembers(currentGroup);
            foreach (BotOwner oldGroupMember in oldGroupMembers)
            {
                newGroup.AddAlly(oldGroupMember.GetPlayer);
            }
        }

        private void updateSquadStrategies()
        {
            if (!QuestingBotsPluginConfig.SquadStrategyEnabled.Value)
                return;

            var config = ConfigController.Config?.Questing?.SquadStrategy;
            if (config == null || !config.Enabled)
                return;

            // Sync boss-follower into squads and sync positions
            var entities = ECS.BotEntityBridge.Registry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!entity.IsActive)
                    continue;

                var botOwner = ECS.BotEntityBridge.GetBotOwner(entity);
                if (botOwner == null)
                    continue;

                // Sync world position and earpiece for all entities
                ECS.BotEntityBridge.SyncPosition(botOwner);
                ECS.BotEntityBridge.SyncEarPiece(botOwner);

                // If this is a boss with followers, ensure squad exists
                if (entity.HasFollowers)
                {
                    var squad = ECS.BotEntityBridge.RegisterSquad(entity, botOwner.Id);
                    if (squad != null)
                    {
                        // Add any followers not yet in the squad
                        for (int j = 0; j < entity.Followers.Count; j++)
                        {
                            var follower = entity.Followers[j];
                            if (follower.IsActive && follower.Squad != squad)
                                ECS.BotEntityBridge.AddToSquad(follower, entity);
                        }

                        // Compute squad personality if not yet determined
                        if (squad.PersonalityType == ECS.SquadPersonalityType.None && squad.Members.Count > 0)
                        {
                            ECS.BotEntityBridge.ComputeSquadPersonality(squad);
                        }

                        // Sync boss objective into squad
                        ECS.BotEntityBridge.SyncSquadObjective(botOwner);

                        // Detect zone-based movement quest
                        var objectiveManager = botOwner.GetObjectiveManager();
                        var zoneConfig = ConfigController.Config?.Questing?.ZoneMovement;
                        squad.IsZoneObjective =
                            zoneConfig != null
                            && objectiveManager != null
                            && objectiveManager.IsJobAssignmentActive
                            && objectiveManager.CurrentQuestName == zoneConfig.QuestName;
                    }
                }
            }

            // Sync threat direction for squads with combat members
            if (config.EnableCombatAwarePositioning)
            {
                updateSquadThreatDirections(config);
            }

            // Run squad strategy manager (lazy-init)
            if (_squadStrategyManager == null)
            {
                NavMeshPositionValidator.SampleRadius = config.NavMeshSampleRadius;
                _squadStrategyManager = new ECS.UtilityAI.SquadStrategyManager(
                    new ECS.UtilityAI.SquadStrategy[]
                    {
                        new ECS.UtilityAI.GotoObjectiveStrategy(
                            config,
                            positionValidator: NavMeshPositionValidator.TrySnap,
                            reachabilityValidator: NavMeshPositionValidator.IsReachable,
                            losValidator: NavMeshPositionValidator.HasLineOfSight,
                            coverPositionSource: BsgCoverPointCollector.CollectCoverPositions
                        ),
                    }
                );
            }

            _squadStrategyManager.Update(ECS.BotEntityBridge.SquadRegistry.ActiveSquads);

            // 6. Override tactical positions with zone-derived spread for zone movement squads
            if (config.EnableZoneFollowerSpread)
            {
                updateZoneFollowerPositions(config);
            }

            // 7. Update formation positions and speed decisions for followers
            if (config.EnableFormationMovement)
            {
                updateFormationMovement(config);
            }

            // 8. Update squad voice commands (boss callouts, follower responses, combat warnings)
            if (config.EnableVoiceCommands)
            {
                updateSquadVoiceCommands(config);
            }
        }

        /// <summary>
        /// Sync threat direction for each active squad from combat members' GoalEnemy positions.
        /// Computes the average enemy direction relative to the squad's objective position.
        /// Bumps CombatVersion when threat state transitions (detected ↔ cleared).
        /// </summary>
        private static void updateSquadThreatDirections(SquadStrategyConfig config)
        {
            var squads = ECS.BotEntityBridge.SquadRegistry.ActiveSquads;
            for (int s = 0; s < squads.Count; s++)
            {
                var squad = squads[s];
                if (squad.Leader == null || !squad.Leader.IsActive)
                    continue;

                var obj = squad.Objective;
                if (!obj.HasObjective)
                    continue;

                // Accumulate enemy positions from combat members
                float sumEnemyX = 0f;
                float sumEnemyZ = 0f;
                int combatMemberCount = 0;

                for (int i = 0; i < squad.Members.Count; i++)
                {
                    var member = squad.Members[i];
                    if (!member.IsActive || !member.IsInCombat)
                        continue;

                    var botOwner = ECS.BotEntityBridge.GetBotOwner(member);
                    if (botOwner == null)
                        continue;

                    var goalEnemy = botOwner.Memory?.GoalEnemy;
                    if (goalEnemy == null)
                        continue;

                    var enemyPos = goalEnemy.CurrPosition;
                    sumEnemyX += enemyPos.x;
                    sumEnemyZ += enemyPos.z;
                    combatMemberCount++;
                }

                bool hadThreat = squad.HasThreatDirection;

                if (combatMemberCount > 0)
                {
                    // Compute average enemy position
                    float avgEnemyX = sumEnemyX / combatMemberCount;
                    float avgEnemyZ = sumEnemyZ / combatMemberCount;

                    // Direction from objective toward average enemy position
                    float dirX = avgEnemyX - obj.ObjectiveX;
                    float dirZ = avgEnemyZ - obj.ObjectiveZ;
                    float len = (float)System.Math.Sqrt(dirX * dirX + dirZ * dirZ);

                    if (len > 0.001f)
                    {
                        squad.ThreatDirectionX = dirX / len;
                        squad.ThreatDirectionZ = dirZ / len;
                        squad.HasThreatDirection = true;

                        // Bump version on new threat or direction change
                        if (!hadThreat)
                            squad.CombatVersion++;
                    }
                }
                else if (hadThreat)
                {
                    // No combat members — clear threat
                    squad.ThreatDirectionX = 0f;
                    squad.ThreatDirectionZ = 0f;
                    squad.HasThreatDirection = false;
                    squad.CombatVersion++;
                }
            }
        }

        /// <summary>Reusable buffer for zone candidate positions (max 4 neighbors × 3 floats).</summary>
        private static readonly float[] _zoneCandidateBuffer = new float[4 * 3];

        /// <summary>Reusable buffer for follower seeds (max 6 followers).</summary>
        private static readonly int[] _zoneFollowerSeedBuffer = new int[ECS.SquadObjective.MaxMembers];

        /// <summary>Reusable buffer for zone output positions (max 6 followers × 3 floats).</summary>
        private static readonly float[] _zoneOutputBuffer = new float[ECS.SquadObjective.MaxMembers * 3];

        /// <summary>
        /// Override tactical positions for zone movement squads with zone-derived spread.
        /// Each follower gets a different neighboring grid cell position with seed-based jitter,
        /// creating a search-party pattern instead of everyone clustering on the same cell.
        /// Runs after strategy manager assigns geometric positions, replacing them for zone squads.
        /// </summary>
        private static void updateZoneFollowerPositions(SquadStrategyConfig config)
        {
            var gridManager = Singleton<GameWorld>.Instance?.GetComponent<ZoneMovement.Integration.WorldGridManager>();
            if (gridManager == null || !gridManager.IsInitialized)
                return;

            var squads = ECS.BotEntityBridge.SquadRegistry.ActiveSquads;
            for (int s = 0; s < squads.Count; s++)
            {
                var squad = squads[s];
                if (!squad.IsZoneObjective)
                    continue;
                if (squad.Leader == null || !squad.Leader.IsActive)
                    continue;

                var leader = squad.Leader;

                // Get the boss's current grid cell
                var bossCell = gridManager.GetCellForBot(
                    new Vector3(leader.CurrentPositionX, leader.CurrentPositionY, leader.CurrentPositionZ)
                );
                if (bossCell == null)
                    continue;

                // Collect navigable neighboring cell centers into candidate buffer
                var neighbors = bossCell.Neighbors;
                int candidateCount = 0;
                for (int n = 0; n < neighbors.Count; n++)
                {
                    var neighbor = neighbors[n];
                    if (!neighbor.IsNavigable)
                        continue;
                    if (candidateCount >= _zoneCandidateBuffer.Length / 3)
                        break;

                    var center = neighbor.Center;
                    _zoneCandidateBuffer[candidateCount * 3] = center.x;
                    _zoneCandidateBuffer[candidateCount * 3 + 1] = center.y;
                    _zoneCandidateBuffer[candidateCount * 3 + 2] = center.z;
                    candidateCount++;
                }

                if (candidateCount == 0)
                    continue;

                // Collect follower seeds
                int followerCount = 0;
                for (int i = 0; i < squad.Members.Count; i++)
                {
                    var member = squad.Members[i];
                    if (member == leader || !member.IsActive)
                        continue;
                    if (followerCount >= _zoneFollowerSeedBuffer.Length)
                        break;

                    _zoneFollowerSeedBuffer[followerCount] = member.FieldNoiseSeed;
                    followerCount++;
                }

                if (followerCount == 0)
                    continue;

                // Distribute followers across candidate cells with jitter
                ECS.Systems.ZoneFollowerPositionCalculator.DistributeFollowers(
                    _zoneCandidateBuffer,
                    candidateCount,
                    _zoneFollowerSeedBuffer,
                    followerCount,
                    config.ZoneJitterRadius,
                    _zoneOutputBuffer
                );

                // Override follower tactical positions with zone-derived positions
                int posIdx = 0;
                for (int i = 0; i < squad.Members.Count; i++)
                {
                    var member = squad.Members[i];
                    if (member == leader || !member.IsActive)
                        continue;
                    if (posIdx >= followerCount)
                        break;

                    int off = posIdx * 3;
                    if (!float.IsNaN(_zoneOutputBuffer[off]))
                    {
                        member.TacticalPositionX = _zoneOutputBuffer[off];
                        member.TacticalPositionY = _zoneOutputBuffer[off + 1];
                        member.TacticalPositionZ = _zoneOutputBuffer[off + 2];
                        member.HasTacticalPosition = true;
                        member.SquadRole = ECS.SquadRole.Guard;
                    }
                    posIdx++;
                }
            }
        }

        /// <summary>
        /// Iterate dense ECS entity list to discover and validate boss relationships.
        /// Uses O(1) entity.IsActive checks for dead-bot detection.
        /// </summary>
        private void updateBosses()
        {
            var entities = ECS.BotEntityBridge.Registry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!entity.IsActive)
                    continue;

                var bot = ECS.BotEntityBridge.GetBotOwner(entity);
                if (bot == null || bot.IsDead)
                    continue;

                // Discover boss from BSG API if not yet assigned
                BotOwner bossBot = null;
                if (!entity.HasBoss)
                {
                    bossBot = bot.BotFollower?.BossToFollow?.Player()?.AIData?.BotOwner;
                }
                else
                {
                    bossBot = ECS.BotEntityBridge.GetBotOwner(entity.Boss);
                }

                if (bossBot == null)
                    continue;

                // Check if boss is inactive in ECS (O(1) entity.IsActive)
                if (ECS.BotEntityBridge.TryGetEntity(bossBot, out var bossEntity) && !bossEntity.IsActive)
                {
                    continue;
                }

                // Check if boss just died
                if (bossBot.IsDead)
                {
                    Controllers.LoggingController.LogDebug("Boss " + bossBot.GetText() + " is now dead.");
                    ECS.BotEntityBridge.DeactivateBot(bossBot);
                    continue;
                }

                addBossFollower(bossBot, bot);
            }
        }

        private void addBossFollower(BotOwner boss, BotOwner bot)
        {
            if (boss == null)
            {
                throw new ArgumentNullException("Boss argument cannot be null", nameof(boss));
            }

            if (bot == null)
            {
                throw new ArgumentNullException("Bot argument cannot be null", nameof(bot));
            }

            // Check ECS to see if this follower is already assigned to this boss
            if (
                ECS.BotEntityBridge.TryGetEntity(bot, out var followerEntity)
                && followerEntity.Boss != null
                && ECS.BotEntityBridge.TryGetEntity(boss, out var bossEntity)
                && followerEntity.Boss == bossEntity
            )
            {
                return;
            }

            Controllers.LoggingController.LogInfo("Bot " + bot.GetText() + " is now a follower for " + boss.GetText());
            ECS.BotEntityBridge.SyncBossFollower(bot, boss);

            BotJobAssignmentFactory.CheckBotJobAssignmentValidity(boss);
        }

        /// <summary>
        /// Dead follower cleanup via ECS entity.IsActive checks.
        /// HiveMindSystem.CleanupDeadEntities handles boss/follower reference cleanup in ECS.
        /// </summary>
        private void updateBossFollowers()
        {
            ECS.Systems.HiveMindSystem.CleanupDeadEntities(ECS.BotEntityBridge.Registry.Entities);
        }

        /// <summary>Reusable buffer for formation positions (max 6 followers × 3 floats).</summary>
        private static readonly float[] _formationPositionBuffer = new float[ECS.SquadObjective.MaxMembers * 3];

        /// <summary>
        /// Update en-route formation positions and per-follower speed decisions.
        /// Runs after squad strategy manager assigns tactical positions.
        /// </summary>
        private void updateFormationMovement(SquadStrategyConfig config)
        {
            var formationConfig = new FormationConfig(config.CatchUpDistance, config.MatchSpeedDistance, config.SlowApproachDistance, true);

            var squads = ECS.BotEntityBridge.SquadRegistry.ActiveSquads;
            for (int s = 0; s < squads.Count; s++)
            {
                var squad = squads[s];
                if (squad.Leader == null || !squad.Leader.IsActive)
                    continue;

                var leader = squad.Leader;

                // Compute heading from previous → current leader position
                bool hasHeading = FormationPositionUpdater.ComputeHeading(
                    squad.PreviousLeaderX,
                    squad.PreviousLeaderZ,
                    leader.CurrentPositionX,
                    leader.CurrentPositionZ,
                    out float hx,
                    out float hz
                );

                // Save current position for next tick
                squad.PreviousLeaderX = leader.CurrentPositionX;
                squad.PreviousLeaderZ = leader.CurrentPositionZ;

                // Check if leader is still moving toward objective (not close)
                bool leaderIsEnRoute = hasHeading && !leader.IsCloseToObjective && leader.HasActiveObjective;

                // Count active followers with tactical positions
                int followerCount = 0;
                for (int i = 0; i < squad.Members.Count; i++)
                {
                    var m = squad.Members[i];
                    if (m != leader && m.IsActive && m.HasTacticalPosition)
                        followerCount++;
                }

                if (leaderIsEnRoute && followerCount > 0)
                {
                    // Probe NavMesh for lateral width to select Column vs Spread
                    float pathWidth = NavMeshPathWidthProbe.MeasureWidth(
                        leader.CurrentPositionX,
                        leader.CurrentPositionY,
                        leader.CurrentPositionZ,
                        hx,
                        hz
                    );
                    var formationType = FormationSelector.SelectWithSpacing(
                        pathWidth,
                        config.FormationSwitchWidth,
                        out float spacing,
                        config.ColumnSpacing,
                        config.SpreadSpacing
                    );

                    // Compute en-route formation positions
                    int clampedCount = Math.Min(followerCount, ECS.SquadObjective.MaxMembers);
                    FormationPositionUpdater.ComputeFormationPositions(
                        formationType,
                        leader.CurrentPositionX,
                        leader.CurrentPositionY,
                        leader.CurrentPositionZ,
                        hx,
                        hz,
                        clampedCount,
                        spacing,
                        _formationPositionBuffer
                    );

                    // Override follower tactical positions with formation positions
                    int posIdx = 0;
                    for (int i = 0; i < squad.Members.Count; i++)
                    {
                        var member = squad.Members[i];
                        if (member == leader || !member.IsActive || !member.HasTacticalPosition)
                            continue;
                        if (posIdx >= clampedCount)
                            break;

                        member.TacticalPositionX = _formationPositionBuffer[posIdx * 3];
                        member.TacticalPositionY = _formationPositionBuffer[posIdx * 3 + 1];
                        member.TacticalPositionZ = _formationPositionBuffer[posIdx * 3 + 2];
                        member.IsEnRouteFormation = true;
                        posIdx++;
                    }
                }
                else
                {
                    // Leader is stationary or close to objective — clear en-route flag
                    for (int i = 0; i < squad.Members.Count; i++)
                    {
                        squad.Members[i].IsEnRouteFormation = false;
                    }
                }

                // Update per-follower formation speed decisions
                updateFollowerSpeedDecisions(squad, leader, formationConfig);
            }
        }

        private static void updateFollowerSpeedDecisions(ECS.SquadEntity squad, ECS.BotEntity leader, FormationConfig formationConfig)
        {
            // Detect if boss is sprinting via movement state or BotOwner
            bool bossIsSprinting = leader.Movement.IsSprinting;
            if (!bossIsSprinting)
            {
                var bossOwner = ECS.BotEntityBridge.GetBotOwner(leader);
                if (bossOwner != null)
                {
                    var objectiveManager = bossOwner.GetObjectiveManager();
                    bossIsSprinting = objectiveManager?.BotSprintingController?.IsSprinting == true;
                }
            }

            for (int i = 0; i < squad.Members.Count; i++)
            {
                var member = squad.Members[i];
                if (member == leader || !member.IsActive)
                    continue;

                // Distance to boss (squared)
                float dx = member.CurrentPositionX - leader.CurrentPositionX;
                float dy = member.CurrentPositionY - leader.CurrentPositionY;
                float dz = member.CurrentPositionZ - leader.CurrentPositionZ;
                member.DistanceToBossSqr = dx * dx + dy * dy + dz * dz;
                member.BossIsSprinting = bossIsSprinting;

                if (!member.IsEnRouteFormation)
                    continue;

                // Distance to tactical position (squared)
                float tdx = member.CurrentPositionX - member.TacticalPositionX;
                float tdy = member.CurrentPositionY - member.TacticalPositionY;
                float tdz = member.CurrentPositionZ - member.TacticalPositionZ;
                float distToTacticalSqr = tdx * tdx + tdy * tdy + tdz * tdz;

                member.FormationSpeed = FormationSpeedController.ComputeSpeedDecision(
                    bossIsSprinting,
                    member.DistanceToBossSqr,
                    distToTacticalSqr,
                    formationConfig
                );
            }
        }

        private void updateSquadVoiceCommands(SquadStrategyConfig config)
        {
            float currentTime = Time.time;
            float cooldown = config.VoiceCommandCooldown;
            float responseDelay = config.FollowerResponseDelay;
            bool useCommRange = config.EnableCommunicationRange;

            var squads = ECS.BotEntityBridge.SquadRegistry.ActiveSquads;
            for (int s = 0; s < squads.Count; s++)
            {
                var squad = squads[s];
                if (squad.Leader == null || !squad.Leader.IsActive)
                    continue;

                var leader = squad.Leader;
                var objective = squad.Objective;

                // 1. Process pending callouts (delayed follower responses from previous ticks)
                for (int i = 0; i < squad.Members.Count; i++)
                {
                    var member = squad.Members[i];
                    if (!member.IsActive || member.PendingCalloutId == SquadCalloutId.None)
                        continue;

                    if (currentTime >= member.PendingCalloutTime)
                    {
                        var pendingBotOwner = ECS.BotEntityBridge.GetBotOwner(member);
                        if (pendingBotOwner != null)
                        {
                            SquadVoiceHelper.TrySay(pendingBotOwner, member.PendingCalloutId);
                            member.LastCalloutTime = currentTime;
                        }
                        member.PendingCalloutId = SquadCalloutId.None;
                    }
                }

                // 2. Boss objective callout (edge-triggered on version change)
                if (objective.HasObjective && leader.LastSeenObjectiveVersion != objective.Version)
                {
                    if (!SquadCalloutDecider.IsOnCooldown(leader.LastCalloutTime, currentTime, cooldown))
                    {
                        bool objectiveChanged = leader.LastSeenObjectiveVersion > 0;
                        bool bossArrived = leader.IsCloseToObjective;
                        int bossCallout = SquadCalloutDecider.DecideBossCallout(objectiveChanged, bossArrived);

                        if (bossCallout != SquadCalloutId.None)
                        {
                            var bossBotOwner = ECS.BotEntityBridge.GetBotOwner(leader);
                            if (bossBotOwner != null && SquadVoiceHelper.TrySay(bossBotOwner, bossCallout))
                            {
                                leader.LastCalloutTime = currentTime;

                                // Queue follower responses with staggered delays
                                int followerIdx = 0;
                                for (int i = 0; i < squad.Members.Count; i++)
                                {
                                    var member = squad.Members[i];
                                    if (member == leader || !member.IsActive)
                                        continue;

                                    // Communication range gate
                                    if (useCommRange)
                                    {
                                        float dx = leader.CurrentPositionX - member.CurrentPositionX;
                                        float dy = leader.CurrentPositionY - member.CurrentPositionY;
                                        float dz = leader.CurrentPositionZ - member.CurrentPositionZ;
                                        float sqrDist = dx * dx + dy * dy + dz * dz;
                                        if (
                                            !CommunicationRange.IsInRange(
                                                leader.HasEarPiece,
                                                member.HasEarPiece,
                                                sqrDist,
                                                config.CommunicationRangeNoEarpiece,
                                                config.CommunicationRangeEarpiece
                                            )
                                        )
                                        {
                                            followerIdx++;
                                            continue;
                                        }
                                    }

                                    int response = SquadCalloutDecider.DecideFollowerResponse(bossCallout, followerIdx);
                                    if (response != SquadCalloutId.None)
                                    {
                                        member.PendingCalloutId = response;
                                        member.PendingCalloutTime = currentTime + responseDelay + followerIdx * 0.3f;
                                    }
                                    followerIdx++;
                                }
                            }
                        }
                    }
                }

                // 3. Follower tactical arrival callout
                float arrivalRadiusSqr = config.ArrivalRadius * config.ArrivalRadius;
                for (int i = 0; i < squad.Members.Count; i++)
                {
                    var member = squad.Members[i];
                    if (member == leader || !member.IsActive || !member.HasTacticalPosition)
                        continue;

                    if (SquadCalloutDecider.IsOnCooldown(member.LastCalloutTime, currentTime, cooldown))
                        continue;

                    float tdx = member.CurrentPositionX - member.TacticalPositionX;
                    float tdy = member.CurrentPositionY - member.TacticalPositionY;
                    float tdz = member.CurrentPositionZ - member.TacticalPositionZ;
                    float distSqr = tdx * tdx + tdy * tdy + tdz * tdz;

                    bool justArrived = distSqr < arrivalRadiusSqr;
                    int arrivalCallout = SquadCalloutDecider.DecideArrivalCallout(justArrived);
                    if (arrivalCallout != SquadCalloutId.None)
                    {
                        var memberBotOwner = ECS.BotEntityBridge.GetBotOwner(member);
                        if (memberBotOwner != null && SquadVoiceHelper.TrySay(memberBotOwner, arrivalCallout))
                        {
                            member.LastCalloutTime = currentTime;
                        }
                    }
                }

                // 4. Combat transition callout (any member entering combat)
                for (int i = 0; i < squad.Members.Count; i++)
                {
                    var member = squad.Members[i];
                    if (!member.IsActive)
                    {
                        member.PreviousIsInCombat = false;
                        continue;
                    }

                    bool wasInCombat = member.PreviousIsInCombat;
                    member.PreviousIsInCombat = member.IsInCombat;

                    if (!member.IsInCombat || wasInCombat)
                        continue;

                    if (SquadCalloutDecider.IsOnCooldown(member.LastCalloutTime, currentTime, cooldown))
                        continue;

                    // Compute enemy direction using dot products
                    var memberBotOwner = ECS.BotEntityBridge.GetBotOwner(member);
                    if (memberBotOwner == null)
                        continue;

                    var goalEnemy = memberBotOwner.Memory?.GoalEnemy;
                    if (goalEnemy == null)
                        continue;

                    var enemyPos = goalEnemy.CurrPosition;
                    var botPos = memberBotOwner.Position;
                    var botFwd = memberBotOwner.LookDirection;

                    float toEnemyX = enemyPos.x - botPos.x;
                    float toEnemyZ = enemyPos.z - botPos.z;
                    float toEnemyLen = (float)Math.Sqrt(toEnemyX * toEnemyX + toEnemyZ * toEnemyZ);
                    if (toEnemyLen < 0.001f)
                        continue;

                    toEnemyX /= toEnemyLen;
                    toEnemyZ /= toEnemyLen;

                    // Normalize forward in XZ
                    float fwdLen = (float)Math.Sqrt(botFwd.x * botFwd.x + botFwd.z * botFwd.z);
                    if (fwdLen < 0.001f)
                        continue;

                    float fwdX = botFwd.x / fwdLen;
                    float fwdZ = botFwd.z / fwdLen;

                    float dotForward = fwdX * toEnemyX + fwdZ * toEnemyZ;
                    // Right = (fwdZ, -fwdX) in XZ plane
                    float dotRight = fwdZ * toEnemyX + (-fwdX) * toEnemyZ;

                    int dirCallout = SquadCalloutDecider.DecideEnemyDirectionCallout(dotForward, dotRight);
                    if (dirCallout != SquadCalloutId.None)
                    {
                        if (SquadVoiceHelper.TrySay(memberBotOwner, dirCallout, aggressive: true))
                        {
                            member.LastCalloutTime = currentTime;
                        }
                    }
                }
            }
        }

        // ── Loot Scanning ────────────────────────────────────────────

        /// <summary>Shared scan result buffer. 32 max results per scan pass.</summary>
        private static readonly LootScanResult[] _lootScanResults = new LootScanResult[32];

        /// <summary>
        /// Tick step 6: Cleanup expired combat events and query per-bot combat state
        /// for vulture behavior. Writes HasNearbyEvent, CombatIntensity, IsInBossZone
        /// on each active entity.
        /// </summary>
        private static void updateCombatEvents()
        {
            var vultureConfig = ConfigController.Config?.Questing?.Vulture;
            if (vultureConfig == null || !vultureConfig.Enabled)
                return;

            float currentTime = Time.time;

            // Cleanup expired events once per tick
            CombatEventRegistry.CleanupExpired(currentTime, vultureConfig.MaxEventAge);

            // Update per-entity combat event fields
            CombatEventScanner.UpdateEntities(
                ECS.BotEntityBridge.Registry.Entities,
                currentTime,
                vultureConfig.MaxEventAge,
                vultureConfig.BaseDetectionRange,
                vultureConfig.BaseDetectionRange,
                vultureConfig.IntensityWindow,
                vultureConfig.BossAvoidanceRadius,
                vultureConfig.BossZoneDecay
            );
        }

        /// <summary>Per-entity last scan time for rate limiting (using Time.time).</summary>
        private static readonly Dictionary<int, float> _lastScanTime = new Dictionary<int, float>();

        /// <summary>
        /// Tick step 7: For each active bot, scan for loot if the scan interval has elapsed.
        /// Uses LootTargetSelector to pick the best target and SquadLootCoordinator for
        /// boss priority claims and follower shared-target picking.
        /// Gated by looting config and LootingBots compat check.
        /// </summary>
        private static void updateLootScanning()
        {
            var lootingConfig = ConfigController.Config?.Questing?.Looting;
            if (lootingConfig == null || !lootingConfig.Enabled)
                return;

            if (!ExternalMods.ExternalModHandler.IsNativeLootingEnabled())
                return;

            float now = Time.time;
            float scanInterval = lootingConfig.ScanIntervalSeconds;

            var scoringConfig = ECS.BotEntityBridge.BuildScoringConfig();
            var claims = ECS.BotEntityBridge.LootClaims;
            var entities = ECS.BotEntityBridge.Registry.Entities;

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!entity.IsActive || entity.IsInCombat || entity.IsSleeping)
                    continue;

                // Rate limit: skip if scanned recently
                if (_lastScanTime.TryGetValue(entity.Id, out float lastTime) && (now - lastTime) < scanInterval)
                    continue;

                _lastScanTime[entity.Id] = now;

                // Skip if already has an active loot target they're pursuing
                if (entity.HasLootTarget && (entity.IsApproachingLoot || entity.IsLooting))
                    continue;

                // Get BotOwner for physics scan
                if (!ECS.BotEntityBridge.TryGetBotOwner(entity, out var bot))
                    continue;

                // Scan nearby loot
                int scanCount = LootScanHelper.ScanForLoot(
                    bot.Position,
                    lootingConfig.DetectContainerDistance,
                    lootingConfig.DetectItemDistance,
                    lootingConfig.DetectCorpseDistance,
                    lootingConfig.ContainerLootingEnabled,
                    lootingConfig.LooseItemLootingEnabled,
                    lootingConfig.CorpseLootingEnabled,
                    null,
                    _lootScanResults,
                    _lootScanResults.Length
                );

                if (scanCount == 0)
                    continue;

                // Boss/follower coordination
                bool isBoss = entity.Boss == null && entity.Followers.Count > 0;
                bool isFollower = entity.Boss != null;

                if (isBoss && lootingConfig.SquadLootCoordination)
                {
                    // Boss gets priority pick
                    int bestIdx = SquadLootCoordinator.BossPriorityClaim(_lootScanResults, scanCount, claims, entity.Id);
                    if (bestIdx >= 0)
                    {
                        setLootTargetFromScan(entity, _lootScanResults[bestIdx]);
                    }

                    // Share scan results with squad
                    if (entity.Squad != null)
                    {
                        SquadLootCoordinator.ShareScanResults(entity.Squad, _lootScanResults, scanCount);
                    }
                }
                else if (isFollower && lootingConfig.SquadLootCoordination)
                {
                    // Follower: check if allowed to loot
                    float commRangeSqr = lootingConfig.DetectContainerDistance * lootingConfig.DetectContainerDistance;
                    if (!SquadLootCoordinator.ShouldFollowerLoot(entity, entity.Boss, commRangeSqr))
                        continue;

                    // Try shared results from squad first
                    if (entity.Squad != null && entity.Squad.SharedLootCount > 0)
                    {
                        int bossLootId = entity.Boss.HasLootTarget ? entity.Boss.LootTargetId : -1;
                        int sharedIdx = SquadLootCoordinator.PickSharedTargetForFollower(entity.Squad, entity.Id, bossLootId, claims);
                        if (sharedIdx >= 0)
                        {
                            var sq = entity.Squad;
                            var sharedResult = new LootScanResult
                            {
                                Id = sq.SharedLootIds[sharedIdx],
                                X = sq.SharedLootX[sharedIdx],
                                Y = sq.SharedLootY[sharedIdx],
                                Z = sq.SharedLootZ[sharedIdx],
                                Type = sq.SharedLootTypes[sharedIdx],
                                Value = sq.SharedLootValues[sharedIdx],
                            };
                            setLootTargetFromScan(entity, sharedResult);
                            continue;
                        }
                    }

                    // Fallback: own scan results
                    float timeSinceLastLoot = (float)(DateTime.Now - entity.LastLootingTime).TotalSeconds;
                    float objDistSqr = entity.HasActiveObjective ? entity.DistanceToObjective * entity.DistanceToObjective : 10000f;
                    int bestIdx = LootTargetSelector.SelectBest(
                        _lootScanResults,
                        scanCount,
                        entity.InventorySpaceFree,
                        false,
                        objDistSqr,
                        timeSinceLastLoot,
                        claims,
                        entity.Id,
                        scoringConfig
                    );
                    if (bestIdx >= 0)
                    {
                        setLootTargetFromScan(entity, _lootScanResults[bestIdx]);
                    }
                }
                else
                {
                    // Solo bot: use scoring
                    float timeSinceLastLoot = (float)(DateTime.Now - entity.LastLootingTime).TotalSeconds;
                    float objDistSqr = entity.HasActiveObjective ? entity.DistanceToObjective * entity.DistanceToObjective : 10000f;
                    int bestIdx = LootTargetSelector.SelectBest(
                        _lootScanResults,
                        scanCount,
                        entity.InventorySpaceFree,
                        false,
                        objDistSqr,
                        timeSinceLastLoot,
                        claims,
                        entity.Id,
                        scoringConfig
                    );
                    if (bestIdx >= 0)
                    {
                        setLootTargetFromScan(entity, _lootScanResults[bestIdx]);
                    }
                }
            }
        }

        /// <summary>
        /// Set a loot target on an entity from a scan result and claim it.
        /// </summary>
        private static void setLootTargetFromScan(ECS.BotEntity entity, LootScanResult result)
        {
            entity.HasLootTarget = true;
            entity.LootTargetId = result.Id;
            entity.LootTargetX = result.X;
            entity.LootTargetY = result.Y;
            entity.LootTargetZ = result.Z;
            entity.LootTargetType = result.Type;
            entity.LootTargetValue = result.Value;

            ECS.BotEntityBridge.LootClaims.TryClaim(entity.Id, result.Id);
        }

        /// <summary>
        /// Snapshot alive human player positions into HumanPlayerCache.
        /// Called once per tick — all bots then use ComputeMinSqrDistance with zero allocation.
        /// </summary>
        private static void refreshHumanPlayerCache()
        {
            var allPlayers = Singleton<GameWorld>.Instance.AllAlivePlayersList;
            int count = 0;
            for (int i = 0; i < allPlayers.Count && count < HumanPlayerCache.MaxPlayers; i++)
            {
                var p = allPlayers[i];
                if (p.IsAI)
                    continue;

                var pos = p.Position;
                _humanX[count] = pos.x;
                _humanY[count] = pos.y;
                _humanZ[count] = pos.z;
                count++;
            }

            HumanPlayerCache.SetPositions(_humanX, _humanY, _humanZ, count);
        }

        /// <summary>
        /// Compute LOD tier for every active entity based on distance to nearest human.
        /// Increments per-entity LodFrameCounter each tick.
        /// </summary>
        private static void updateLodTiers()
        {
            var lodConfig = ConfigController.Config?.Questing?.BotLod;
            if (lodConfig == null || !lodConfig.Enabled)
                return;

            float reducedSqr = lodConfig.ReducedDistance * lodConfig.ReducedDistance;
            float minimalSqr = lodConfig.MinimalDistance * lodConfig.MinimalDistance;

            var entities = ECS.BotEntityBridge.Registry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!entity.IsActive)
                    continue;

                float sqrDist = HumanPlayerCache.ComputeMinSqrDistance(
                    entity.CurrentPositionX,
                    entity.CurrentPositionY,
                    entity.CurrentPositionZ
                );

                entity.LodTier = BotLodCalculator.ComputeTier(sqrDist, reducedSqr, minimalSqr);
                entity.LodFrameCounter++;
            }
        }

        /// <summary>
        /// Pull sensors iterate dense ECS entity list instead of old dictionary keys.
        /// Reads game state from BotObjectiveManager and writes directly to BotEntity sensor fields.
        /// No Action delegate allocation — zero GC pressure per tick.
        /// </summary>
        private static void updatePullSensors()
        {
            var entities = ECS.BotEntityBridge.Registry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!entity.IsActive)
                    continue;

                var bot = ECS.BotEntityBridge.GetBotOwner(entity);
                if (bot == null || !bot.isActiveAndEnabled || bot.IsDead)
                    continue;

                var objectiveManager = bot.GetObjectiveManager();

                // CanQuest: default false
                entity.CanQuest = objectiveManager != null && objectiveManager.IsQuestingAllowed;

                // CanSprintToObjective: default true
                entity.CanSprintToObjective = objectiveManager == null || objectiveManager.CanSprintToObjective();
            }
        }
    }
}
