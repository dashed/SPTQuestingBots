using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using SPTQuestingBots.BehaviorExtensions;
using SPTQuestingBots.BotLogic.ECS.Systems;
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
        }

        /// <summary>
        /// Deterministic tick order (50ms interval, Phobos-inspired):
        ///   1. updateBosses()              — discover/validate boss relationships from BSG API
        ///   2. updateBossFollowers()        — cleanup dead boss/follower references (CleanupDeadEntities)
        ///   3. updatePullSensors()          — CanQuest + CanSprintToObjective via dense ECS iteration
        ///   4. ResetInactiveEntitySensors() — clear sensor state on dead/despawned entities
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

            if (Singleton<GameWorld>.Instance.GetComponent<Components.LocationData>().CurrentLocation == null)
            {
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
                    }
                }
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

            // 6. Update formation positions and speed decisions for followers
            if (config.EnableFormationMovement)
            {
                updateFormationMovement(config);
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
                    // Compute en-route formation positions
                    int clampedCount = Math.Min(followerCount, ECS.SquadObjective.MaxMembers);
                    FormationPositionUpdater.ComputeFormationPositions(
                        FormationType.Column,
                        leader.CurrentPositionX,
                        leader.CurrentPositionY,
                        leader.CurrentPositionZ,
                        hx,
                        hz,
                        clampedCount,
                        config.ColumnSpacing,
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
