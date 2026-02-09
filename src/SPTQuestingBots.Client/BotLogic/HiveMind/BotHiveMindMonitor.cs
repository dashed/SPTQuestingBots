using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using SPTQuestingBots.BehaviorExtensions;
using SPTQuestingBots.Components.Spawning;
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
        internal static List<BotOwner> deadBots = new List<BotOwner>();
        internal static Dictionary<BotOwner, BotOwner> botBosses = new Dictionary<BotOwner, BotOwner>();
        internal static Dictionary<BotOwner, List<BotOwner>> botFollowers = new Dictionary<BotOwner, List<BotOwner>>();

        private static Dictionary<BotHiveMindSensorType, BotHiveMindAbstractSensor> sensors =
            new Dictionary<BotHiveMindSensorType, BotHiveMindAbstractSensor>();

        public BotHiveMindMonitor()
        {
            UpdateInterval = 50;

            sensors.Add(BotHiveMindSensorType.InCombat, new BotHiveMindIsInCombatSensor());
            sensors.Add(BotHiveMindSensorType.IsSuspicious, new BotHiveMindIsSuspiciousSensor());
            sensors.Add(BotHiveMindSensorType.CanQuest, new BotHiveMindCanQuestSensor());
            sensors.Add(BotHiveMindSensorType.CanSprintToObjective, new BotHiveMindCanSprintToObjectiveSensor());
            sensors.Add(BotHiveMindSensorType.WantsToLoot, new BotHiveMindWantsToLootSensor());
        }

        public static void Clear()
        {
            deadBots.Clear();
            botBosses.Clear();
            botFollowers.Clear();

            sensors.Clear();

            ECS.BotEntityBridge.Clear();
        }

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

            updateBosses();
            updateBossFollowers();

            // Phase 5C: Pull sensors iterate dense ECS entity list instead of old dictionaries.
            // Push sensors (InCombat, IsSuspicious, WantsToLoot) are set externally via
            // UpdateValueForBot → BotEntityBridge.UpdateSensor, no tick needed.
            updatePullSensors();

            // Reset sensors for inactive (dead/despawned) entities
            ECS.Systems.HiveMindSystem.ResetInactiveEntitySensors(ECS.BotEntityBridge.Registry.Entities);
        }

        public static void UpdateValueForBot(BotHiveMindSensorType sensorType, BotOwner bot, bool value)
        {
            // Phase 5B: Write only to ECS entity, skip old dictionary write.
            // Old dictionaries are still populated for pull sensors (Phase 5C will migrate those).
            ECS.BotEntityBridge.UpdateSensor(sensorType, bot, value);

            if (sensorType == BotHiveMindSensorType.WantsToLoot && value)
            {
                ECS.BotEntityBridge.UpdateLastLootingTime(bot);
            }
        }

        public static void RegisterBot(BotOwner bot)
        {
            if (bot == null)
            {
                throw new ArgumentNullException("Cannot register a null bot", nameof(bot));
            }

            if (!botBosses.ContainsKey(bot))
            {
                botBosses.Add(bot, null);
            }

            if (!botFollowers.ContainsKey(bot))
            {
                botFollowers.Add(bot, new List<BotOwner>());
            }

            foreach (BotHiveMindAbstractSensor sensor in sensors.Values)
            {
                sensor.RegisterBot(bot);
            }
        }

        public static BotOwner GetBoss(BotOwner bot)
        {
            return botBosses.ContainsKey(bot) ? botBosses[bot] : null;
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

            // Clear stored information about the bot's boss (if applicable)
            foreach (BotOwner follower in botBosses.Keys)
            {
                if (botBosses[follower] == bot)
                {
                    botBosses[follower] = null;
                }

                if (follower == bot)
                {
                    botBosses[bot] = null;
                }
            }

            // Clear stored information about the bot's followers (if applicable)
            foreach (BotOwner boss in botFollowers.Keys)
            {
                if (boss == bot)
                {
                    botFollowers[boss].Clear();
                }

                if (botFollowers[boss].Contains(bot))
                {
                    botFollowers[boss].Remove(bot);
                }
            }

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

        private static void throwIfSensorNotRegistred(BotHiveMindSensorType sensorType)
        {
            if (!sensors.ContainsKey(sensorType))
            {
                throw new InvalidOperationException("Sensor type " + sensorType.ToString() + " has not been registerd.");
            }
        }

        /// <summary>
        /// Phase 5D: Iterate dense ECS entity list to discover and validate boss relationships.
        /// Replaces old dictionary iteration over botBosses.Keys with O(1) entity.IsActive checks.
        /// Old dictionary writes retained for Phase 5F removal.
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

                    // Old dictionary write (Phase 5F will remove)
                    if (botBosses.ContainsKey(bot))
                        botBosses[bot] = bossBot;
                }
                else
                {
                    bossBot = ECS.BotEntityBridge.GetBotOwner(entity.Boss);
                }

                if (bossBot == null)
                    continue;

                // Check if boss is inactive in ECS (replaces deadBots.Contains O(n))
                if (ECS.BotEntityBridge.TryGetEntity(bossBot, out var bossEntity) && !bossEntity.IsActive)
                {
                    // Old dictionary write (Phase 5F will remove)
                    if (botBosses.ContainsKey(bot))
                        botBosses[bot] = null;

                    continue;
                }

                // Check if boss just died
                if (bossBot.IsDead)
                {
                    Controllers.LoggingController.LogDebug("Boss " + bossBot.GetText() + " is now dead.");

                    ECS.BotEntityBridge.DeactivateBot(bossBot);

                    // Old dictionary writes (Phase 5F will remove)
                    if (botFollowers.ContainsKey(bossBot))
                        botFollowers.Remove(bossBot);
                    deadBots.Add(bossBot);

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

            if (!botFollowers.ContainsKey(boss))
            {
                //throw new InvalidOperationException("Boss " + boss.GetText() + " has not been added to the follower dictionary");
                botFollowers.Add(boss, new List<BotOwner>());
            }

            if (!botFollowers[boss].Contains(bot))
            {
                Controllers.LoggingController.LogInfo("Bot " + bot.GetText() + " is now a follower for " + boss.GetText());
                botFollowers[boss].Add(bot);
                ECS.BotEntityBridge.SyncBossFollower(bot, boss);

                BotJobAssignmentFactory.CheckBotJobAssignmentValidity(boss);
            }
        }

        /// <summary>
        /// Phase 5D: Dead follower cleanup now uses ECS entity.IsActive checks.
        /// HiveMindSystem.CleanupDeadEntities handles boss/follower reference cleanup in ECS.
        /// Old dictionary cleanup retained for Phase 5F removal.
        /// </summary>
        private void updateBossFollowers()
        {
            // ECS cleanup: remove boss/follower references for dead entities
            ECS.Systems.HiveMindSystem.CleanupDeadEntities(ECS.BotEntityBridge.Registry.Entities);

            // Old dictionary cleanup (Phase 5F will remove this entire block)
            var deadBossBuffer = _deadBossBuffer;
            deadBossBuffer.Clear();

            foreach (BotOwner boss in botFollowers.Keys)
            {
                if ((boss == null) || boss.IsDead)
                {
                    if (!deadBots.Contains(boss))
                    {
                        Controllers.LoggingController.LogDebug("Boss " + boss.GetText() + " is now dead.");
                        ECS.BotEntityBridge.DeactivateBot(boss);
                        deadBossBuffer.Add(boss);
                        deadBots.Add(boss);
                    }

                    continue;
                }

                List<BotOwner> followers = botFollowers[boss];
                for (int i = followers.Count - 1; i >= 0; i--)
                {
                    BotOwner follower = followers[i];

                    // Use ECS entity.IsActive instead of deadBots.Contains O(n)
                    bool isFollowerDead = false;
                    if (follower == null)
                    {
                        Controllers.LoggingController.LogWarning("Removing null follower for " + boss.GetText());
                        isFollowerDead = true;
                    }
                    else if (ECS.BotEntityBridge.TryGetEntity(follower, out var followerEntity))
                    {
                        isFollowerDead = !followerEntity.IsActive;
                    }
                    else if (follower.IsDead)
                    {
                        isFollowerDead = true;
                    }

                    if (isFollowerDead)
                    {
                        if (follower != null && !deadBots.Contains(follower))
                        {
                            Controllers.LoggingController.LogDebug(
                                "Follower " + follower.GetText() + " for " + boss.GetText() + " is now dead."
                            );
                            ECS.BotEntityBridge.DeactivateBot(follower);
                            deadBots.Add(follower);
                        }

                        followers.RemoveAt(i);
                    }
                }
            }

            for (int i = 0; i < deadBossBuffer.Count; i++)
            {
                botFollowers.Remove(deadBossBuffer[i]);
            }
        }

        private static readonly List<BotOwner> _deadBossBuffer = new List<BotOwner>();

        /// <summary>
        /// Phase 5C: Pull sensors iterate dense ECS entity list instead of old dictionary keys.
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
