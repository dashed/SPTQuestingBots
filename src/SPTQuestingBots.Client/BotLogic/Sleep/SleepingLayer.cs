using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using SPTQuestingBots.BotLogic.BotMonitor.Monitors;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.BotLogic.Sleep
{
    internal class SleepingLayer : BehaviorExtensions.CustomLayerDelayedUpdate
    {
        private Components.BotObjectiveManager objectiveManager = null;

        public SleepingLayer(BotOwner _botOwner, int _priority)
            : base(_botOwner, _priority, 250)
        {
            objectiveManager = _botOwner.GetOrAddObjectiveManager();
        }

        public override string GetName()
        {
            return "SleepingLayer";
        }

        public override Action GetNextAction()
        {
            return base.GetNextAction();
        }

        public override bool IsCurrentActionEnding()
        {
            return base.IsCurrentActionEnding();
        }

        public override bool IsActive()
        {
            // Check if AI limiting is enabled in the F12 menu
            if (!QuestingBotsPluginConfig.SleepingEnabled.Value)
            {
                return updatePreviousState(false);
            }

            // Don't run this method too often or performance will be impacted (ironically)
            if (!canUpdate())
            {
                return previousState;
            }

            if ((BotOwner.BotState != EBotState.Active) || BotOwner.IsDead)
            {
                return updatePreviousState(false);
            }

            if (isSleeplessBot())
            {
                return updatePreviousState(false);
            }

            // Determine the distance from human players beyond which bots will be disabled
            var locationData = Singleton<GameWorld>.Instance?.GetComponent<Components.LocationData>();
            if (locationData?.CurrentLocation == null)
            {
                return updatePreviousState(false);
            }

            TarkovMaps currentMap = default;
            int mapSpecificHumanDistance = 1000;
            if (QuestingBotsPluginConfig.TarkovMapIDToEnum.TryGetValue(locationData.CurrentLocation.Id, out currentMap))
            {
                mapSpecificHumanDistance = getMapSpecificHumanDistance(currentMap);
            }
            int distanceFromHumans = Math.Min(mapSpecificHumanDistance, QuestingBotsPluginConfig.SleepingMinDistanceToHumansGlobal.Value);

            // Check if the bot is currently allowed to quest
            if ((objectiveManager?.IsQuestingAllowed == true) || (objectiveManager?.IsInitialized == false))
            {
                // Check if bots that can quest are allowed to sleep
                if (!QuestingBotsPluginConfig.SleepingEnabledForQuestingBots.Value)
                {
                    return updatePreviousState(false);
                }

                // If the bot can quest and is allowed to sleep, ensure it's allowed to sleep on the current map
                if (!QuestingBotsPluginConfig.MapsToAllowSleepingForQuestingBots.Value.HasFlag(currentMap))
                {
                    return updatePreviousState(false);
                }
            }

            // Allow bots to extract so new ones can spawn
            if (
                !QuestingBotsPluginConfig.SleepingEnabledForQuestingBots.Value
                && (objectiveManager?.BotMonitor?.GetMonitor<BotExtractMonitor>()?.IsTryingToExtract == true)
            )
            {
                return updatePreviousState(false);
            }

            // Use cached human player positions (zero-allocation) instead of LINQ
            if (!Helpers.HumanPlayerCache.HasPlayers)
            {
                return updatePreviousState(false);
            }

            // Check squared distance to nearest human player
            float distanceFromHumansSqr = (float)distanceFromHumans * distanceFromHumans;
            Vector3 pos = BotOwner.Position;
            float minSqrDist = Helpers.HumanPlayerCache.ComputeMinSqrDistance(pos.x, pos.y, pos.z);
            if (minSqrDist < distanceFromHumansSqr)
            {
                return updatePreviousState(false);
            }

            // Count alive active bots without LINQ allocation
            int aliveBotCount = 0;
            foreach (BotOwner b in Singleton<IBotGame>.Instance.BotsController.Bots.BotOwners)
            {
                if (b.BotState == EBotState.Active && !b.IsDead)
                    aliveBotCount++;
            }

            // Only allow bots to sleep if there are at least a certain number in total on the map
            if (aliveBotCount <= QuestingBotsPluginConfig.MinBotsToEnableSleeping.Value)
            {
                return updatePreviousState(false);
            }

            // Check proximity to questing bots without LINQ allocation
            float questDistSqr =
                QuestingBotsPluginConfig.SleepingMinDistanceToQuestingBots.Value
                * QuestingBotsPluginConfig.SleepingMinDistanceToQuestingBots.Value;
            foreach (BotOwner bot in Singleton<IBotGame>.Instance.BotsController.Bots.BotOwners)
            {
                if (bot.BotState != EBotState.Active || bot.IsDead || !bot.gameObject.activeSelf || bot.Id == BotOwner.Id)
                    continue;

                // We only care about other bots that can quest
                Components.BotObjectiveManager otherBotObjectiveManager = bot.GetObjectiveManager();
                if (otherBotObjectiveManager?.IsQuestingAllowed != true)
                    continue;

                // Ignore bots that are in the same group
                List<BotOwner> groupMemberList = SPT.Custom.CustomAI.AIExtensions.GetAllMembers(bot.BotsGroup);
                if (groupMemberList.Contains(BotOwner))
                    continue;

                // Use squared distance instead of Vector3.Distance
                Vector3 delta = pos - bot.Position;
                if (delta.sqrMagnitude <= questDistSqr)
                {
                    return updatePreviousState(false);
                }
            }

            setNextAction(BehaviorExtensions.BotActionType.Sleep, "Sleep");
            return updatePreviousState(true);
        }

        private bool isSleeplessBot()
        {
            if (!QuestingBotsPluginConfig.ExceptionFlagForWildSpawnType.ContainsKey(BotOwner.Profile.Info.Settings.Role))
            {
                return false;
            }

            BotTypeException botTypeException = QuestingBotsPluginConfig.ExceptionFlagForWildSpawnType[BotOwner.Profile.Info.Settings.Role];
            BotTypeException shouldBeSleepless = botTypeException & QuestingBotsPluginConfig.SleeplessBotTypes.Value;

            return shouldBeSleepless > 0;
        }

        private int getMapSpecificHumanDistance(TarkovMaps map)
        {
            switch (map)
            {
                case TarkovMaps.Customs:
                    return QuestingBotsPluginConfig.SleepingMinDistanceToHumansCustoms.Value;
                case TarkovMaps.Factory:
                    return QuestingBotsPluginConfig.SleepingMinDistanceToHumansFactory.Value;
                case TarkovMaps.Interchange:
                    return QuestingBotsPluginConfig.SleepingMinDistanceToHumansInterchange.Value;
                case TarkovMaps.Labs:
                    return QuestingBotsPluginConfig.SleepingMinDistanceToHumansLabs.Value;
                case TarkovMaps.Lighthouse:
                    return QuestingBotsPluginConfig.SleepingMinDistanceToHumansLighthouse.Value;
                case TarkovMaps.Reserve:
                    return QuestingBotsPluginConfig.SleepingMinDistanceToHumansReserve.Value;
                case TarkovMaps.Shoreline:
                    return QuestingBotsPluginConfig.SleepingMinDistanceToHumansShoreline.Value;
                case TarkovMaps.Streets:
                    return QuestingBotsPluginConfig.SleepingMinDistanceToHumansStreets.Value;
                case TarkovMaps.Woods:
                    return QuestingBotsPluginConfig.SleepingMinDistanceToHumansWoods.Value;
                case TarkovMaps.GroundZero:
                    return QuestingBotsPluginConfig.SleepingMinDistanceToHumansGroundZero.Value;
            }

            return int.MaxValue;
        }
    }
}
