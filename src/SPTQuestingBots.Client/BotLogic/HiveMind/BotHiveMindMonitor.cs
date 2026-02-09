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

            foreach (BotHiveMindAbstractSensor sensor in sensors.Values)
            {
                sensor.Update();
            }
        }

        public static void UpdateValueForBot(BotHiveMindSensorType sensorType, BotOwner bot, bool value)
        {
            throwIfSensorNotRegistred(sensorType);
            sensors[sensorType].UpdateForBot(bot, value);
            ECS.BotEntityBridge.UpdateSensor(sensorType, bot, value);
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

        private void updateBosses()
        {
            foreach (BotOwner bot in botBosses.Keys)
            {
                // Need to check if the reference is for a null object, meaning the bot was despawned and disposed
                if ((bot == null) || bot.IsDead)
                {
                    continue;
                }

                if (botBosses[bot] == null)
                {
                    botBosses[bot] = bot.BotFollower?.BossToFollow?.Player()?.AIData?.BotOwner;
                }
                if (botBosses[bot] == null)
                {
                    continue;
                }

                if (deadBots.Contains(botBosses[bot]))
                {
                    botBosses[bot] = null;
                    continue;
                }

                if (botBosses[bot].IsDead)
                {
                    Controllers.LoggingController.LogDebug("Boss " + botBosses[bot].GetText() + " is now dead.");

                    if (botFollowers.ContainsKey(botBosses[bot]))
                    {
                        botFollowers.Remove(botBosses[bot]);
                    }

                    deadBots.Add(botBosses[bot]);
                    continue;
                }

                addBossFollower(botBosses[bot], bot);
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

        private static readonly List<BotOwner> _deadBossBuffer = new List<BotOwner>();

        private void updateBossFollowers()
        {
            _deadBossBuffer.Clear();

            foreach (BotOwner boss in botFollowers.Keys)
            {
                // Need to check if the reference is for a null object, meaning the bot was despawned and disposed
                if ((boss == null) || boss.IsDead)
                {
                    if (deadBots.Contains(boss))
                    {
                        continue;
                    }

                    Controllers.LoggingController.LogDebug("Boss " + boss.GetText() + " is now dead.");

                    _deadBossBuffer.Add(boss);
                    deadBots.Add(boss);

                    continue;
                }

                List<BotOwner> followers = botFollowers[boss];
                for (int i = followers.Count - 1; i >= 0; i--)
                {
                    BotOwner follower = followers[i];

                    if (follower == null)
                    {
                        Controllers.LoggingController.LogWarning("Removing null follower for " + boss.GetText());

                        deadBots.Add(follower);
                    }

                    if (deadBots.Contains(follower))
                    {
                        followers.RemoveAt(i);
                        continue;
                    }

                    if (follower.IsDead)
                    {
                        Controllers.LoggingController.LogDebug(
                            "Follower " + follower.GetText() + " for " + boss.GetText() + " is now dead."
                        );

                        deadBots.Add(follower);
                    }
                }
            }

            for (int i = 0; i < _deadBossBuffer.Count; i++)
            {
                botFollowers.Remove(_deadBossBuffer[i]);
            }
        }
    }
}
