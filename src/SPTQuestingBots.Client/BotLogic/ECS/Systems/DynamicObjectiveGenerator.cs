using System.Collections.Generic;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Models.Questing;
using UnityEngine;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Generates dynamic quest objectives from live game state data.
    /// Creates Quest objects from combat event clusters (firefight investigation),
    /// death event locations (corpse scavenging), and indoor zone positions
    /// (building clear). All methods accept data parameters and return Quest lists.
    /// The <see cref="Components.DynamicObjectiveScanner"/> orchestrator queries
    /// CombatEventRegistry and passes data to these methods.
    /// </summary>
    public static class DynamicObjectiveGenerator
    {
        /// <summary>
        /// Generate firefight investigation quests from clustered combat events.
        /// Clusters nearby events, filters by minimum intensity, and creates
        /// Ambush quests at each cluster center.
        /// </summary>
        public static List<Quest> GenerateFirefightObjectives(
            CombatEvent[] events,
            int eventCount,
            float currentTime,
            float maxAge,
            float clusterRadius,
            int minIntensity,
            float desirability,
            int maxQuests
        )
        {
            var quests = new List<Quest>();
            if (events == null || eventCount <= 0 || maxQuests <= 0)
                return quests;

            float clusterRadiusSqr = clusterRadius * clusterRadius;
            var clusters = new CombatEventClustering.ClusterResult[maxQuests];
            int clusterCount = CombatEventClustering.ClusterEvents(
                events,
                eventCount,
                currentTime,
                maxAge,
                clusterRadiusSqr,
                clusters,
                maxQuests
            );

            for (int i = 0; i < clusterCount; i++)
            {
                if (clusters[i].Intensity < minIntensity)
                    continue;

                var position = new Vector3(clusters[i].X, clusters[i].Y, clusters[i].Z);
                var step = new QuestObjectiveStep(position, QuestAction.Ambush, new MinMaxConfig(10, 30));
                var objective = new QuestObjective(step);
                objective.SetName("Firefight at (" + clusters[i].X.ToString("F0") + ", " + clusters[i].Z.ToString("F0") + ")");

                var quest = new Quest("Firefight Investigation");
                quest.Desirability = desirability;
                quest.IsRepeatable = false;
                quest.MaxBots = 3;
                quest.AddObjective(objective);

                quests.Add(quest);
            }

            return quests;
        }

        /// <summary>
        /// Generate corpse scavenging quests from recent death events.
        /// Creates MoveToPosition quests near each death location.
        /// </summary>
        public static List<Quest> GenerateCorpseObjectives(
            CombatEvent[] events,
            int eventCount,
            float currentTime,
            float maxAge,
            float desirability,
            int maxQuests
        )
        {
            var quests = new List<Quest>();
            if (events == null || eventCount <= 0 || maxQuests <= 0)
                return quests;

            var deathBuffer = new CombatEvent[maxQuests];
            int deathCount = CombatEventClustering.FilterDeathEvents(events, eventCount, currentTime, maxAge, deathBuffer);

            for (int i = 0; i < deathCount; i++)
            {
                var position = new Vector3(deathBuffer[i].X, deathBuffer[i].Y, deathBuffer[i].Z);
                var step = new QuestObjectiveStep(position, QuestAction.MoveToPosition);
                var objective = new QuestObjective(step);
                objective.SetName("Corpse at (" + deathBuffer[i].X.ToString("F0") + ", " + deathBuffer[i].Z.ToString("F0") + ")");

                var quest = new Quest("Scavenge Corpse");
                quest.Desirability = desirability;
                quest.IsRepeatable = false;
                quest.MaxBots = 2;
                quest.AddObjective(objective);

                quests.Add(quest);
            }

            return quests;
        }

        /// <summary>
        /// Generate building clear quests from indoor zone positions.
        /// Creates HoldAtPosition quests with configurable hold duration.
        /// </summary>
        public static List<Quest> GenerateBuildingClearObjectives(
            Vector3[] indoorPositions,
            int positionCount,
            float desirability,
            float holdMin,
            float holdMax,
            int maxQuests
        )
        {
            var quests = new List<Quest>();
            if (indoorPositions == null || positionCount <= 0 || maxQuests <= 0)
                return quests;

            int limit = positionCount < maxQuests ? positionCount : maxQuests;

            for (int i = 0; i < limit; i++)
            {
                var position = indoorPositions[i];
                var minElapsedTime = new MinMaxConfig(holdMin, holdMax);
                var step = new QuestObjectiveStep(position, QuestAction.HoldAtPosition, minElapsedTime);
                var objective = new QuestObjective(step);
                objective.SetName("Clear building at (" + position.x.ToString("F0") + ", " + position.z.ToString("F0") + ")");

                var quest = new Quest("Clear Building");
                quest.Desirability = desirability;
                quest.IsRepeatable = true;
                quest.MaxBots = 4;
                quest.AddObjective(objective);

                quests.Add(quest);
            }

            return quests;
        }
    }
}
