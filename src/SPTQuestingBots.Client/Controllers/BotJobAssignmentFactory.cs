using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Bootstrap;
using Comfort.Common;
using EFT;
using SPTQuestingBots.BotLogic.BotMonitor.Monitors;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.Objective;
using SPTQuestingBots.Components;
using SPTQuestingBots.Models.Questing;
using SPTQuestingBots.ZoneMovement.Integration;
using UnityEngine;

namespace SPTQuestingBots.Controllers
{
    public static class BotJobAssignmentFactory
    {
        private static CoroutineExtensions.EnumeratorWithTimeLimit enumeratorWithTimeLimit =
            new CoroutineExtensions.EnumeratorWithTimeLimit(ConfigController.Config.MaxCalcTimePerFrame);
        private static List<Quest> allQuests = new List<Quest>();
        private static readonly List<Quest> _possibleQuestsBuffer = new List<Quest>();
        private static readonly List<Quest> _zoneQuestsBuffer = new List<Quest>();
        private static double[] _questScoreBuffer = new double[64];
        private static float[] _questMinDistBuffer = new float[64];
        private static float[] _questMaxDistBuffer = new float[64];
        private static float[] _questMinAngleBuffer = new float[64];

        // Phase 8: Job assignment lists now stored on BotEntityBridge, keyed by entity ID.
        // Access via BotLogic.ECS.BotEntityBridge.GetJobAssignments() / EnsureJobAssignments().

        public static int QuestCount => allQuests.Count;

        public static IReadOnlyList<Quest> FindQuestsWithZone(string zoneId)
        {
            _zoneQuestsBuffer.Clear();
            for (int i = 0; i < allQuests.Count; i++)
            {
                if (allQuests[i].GetObjectiveForZoneID(zoneId) != null)
                {
                    _zoneQuestsBuffer.Add(allQuests[i]);
                }
            }

            return _zoneQuestsBuffer;
        }

        public static bool CanMoreBotsDoQuest(this Quest quest) => quest.NumberOfActiveBots() < quest.MaxBots;

        public static void Clear()
        {
            // Only remove quests that are not based on an EFT quest template
            allQuests.RemoveAll(q => q.Template == null);

            // Remove all objectives for remaining quests. New objectives will be generated after loading the map.
            foreach (Quest quest in allQuests)
            {
                quest.Clear();
            }

            // Phase 8: job assignment lists are cleared by BotEntityBridge.Clear() at raid end
        }

        public static IEnumerator ProcessAllQuests(Action<Quest> action)
        {
            enumeratorWithTimeLimit.Reset();
            yield return enumeratorWithTimeLimit.Run(allQuests, action);
        }

        public static IEnumerator ProcessAllQuests<T1>(Action<Quest, T1> action, T1 param1)
        {
            enumeratorWithTimeLimit.Reset();
            yield return enumeratorWithTimeLimit.Run(allQuests, action, param1);
        }

        public static IEnumerator ProcessAllQuests<T1, T2>(Action<Quest, T1, T2> action, T1 param1, T2 param2)
        {
            enumeratorWithTimeLimit.Reset();
            yield return enumeratorWithTimeLimit.Run(allQuests, action, param1, param2);
        }

        public static void AddQuest(Quest quest)
        {
            foreach (QuestObjective objective in quest.AllObjectives)
            {
                objective.UpdateQuestObjectiveStepNumbers();
            }

            if (quest.IsCamping && (ConfigController.Config.Questing.BotQuests.DesirabilityCampingMultiplier != 1))
            {
                float newDesirability = quest.Desirability * ConfigController.Config.Questing.BotQuests.DesirabilityCampingMultiplier;

                LoggingController.LogInfo(
                    "Adjusting desirability of camping quest " + quest.ToString() + " from " + quest.Desirability + " to " + newDesirability
                );

                quest.Desirability = newDesirability;
            }

            if (quest.IsSniping && (ConfigController.Config.Questing.BotQuests.DesirabilitySnipingMultiplier != 1))
            {
                float newDesirability = quest.Desirability * ConfigController.Config.Questing.BotQuests.DesirabilitySnipingMultiplier;

                LoggingController.LogInfo(
                    "Adjusting desirability of sniping quest " + quest.ToString() + " from " + quest.Desirability + " to " + newDesirability
                );

                quest.Desirability = newDesirability;
            }

            allQuests.Add(quest);
        }

        public static Quest FindQuest(string questID)
        {
            Quest found = null;
            for (int i = 0; i < allQuests.Count; i++)
            {
                if (allQuests[i].Template?.Id == questID)
                {
                    if (found != null)
                        return null; // More than one match — same behavior as original
                    found = allQuests[i];
                }
            }

            return found;
        }

        public static void RemoveBlacklistedQuestObjectives(string locationId)
        {
            foreach (Quest quest in allQuests.ToArray())
            {
                foreach (QuestObjective objective in quest.AllObjectives)
                {
                    // Check if Lightkeeper Island quests should be blacklisted
                    if (locationId == "Lighthouse")
                    {
                        bool visitsIsland = objective
                            .GetAllPositions()
                            .Where(p => p.HasValue)
                            .Any(position =>
                                Singleton<GameWorld>.Instance.GetComponent<LocationData>().IsPointOnLightkeeperIsland(position.Value)
                            );

                        if (visitsIsland && !ConfigController.Config.Questing.BotQuests.LightkeeperIslandQuests.Enabled)
                        {
                            if (quest.TryRemoveObjective(objective))
                            {
                                LoggingController.LogInfo(
                                    "Removing quest objective on Lightkeeper island: " + objective + " for quest " + quest
                                );
                            }
                            else
                            {
                                LoggingController.LogError(
                                    "Could not remove quest objective on Lightkeeper island: " + objective + " for quest " + quest
                                );
                            }
                        }
                    }

                    // https://github.com/dwesterwick/SPTQuestingBots/issues/18
                    // Disable quests that try to go to the Scav Island, pathing is broken there
                    if (locationId == "Shoreline")
                    {
                        bool visitsIsland = objective
                            .GetAllPositions()
                            .Where(p => p.HasValue)
                            .Any(position => position.Value.x > 160 && position.Value.z > 360);

                        if (visitsIsland)
                        {
                            if (quest.TryRemoveObjective(objective))
                            {
                                LoggingController.LogInfo("Removing quest objective on Scav island: " + objective + " for quest " + quest);
                            }
                            else
                            {
                                LoggingController.LogError(
                                    "Could not remove quest objective on Scav island: " + objective + " for quest " + quest
                                );
                            }
                        }
                    }

                    // If there are no remaining objectives, remove the quest too
                    if (quest.NumberOfObjectives == 0)
                    {
                        LoggingController.LogInfo("Removing quest with no valid objectives: " + quest + "...");
                        allQuests.Remove(quest);
                    }
                }
            }
        }

        public static void FailAllJobAssignmentsForBot(string botID)
        {
            var assignments = BotLogic.ECS.BotEntityBridge.GetJobAssignments(botID);
            if (assignments.Count == 0)
            {
                return;
            }

            foreach (BotJobAssignment assignment in assignments.Where(a => a.IsActive))
            {
                assignment.Fail();
            }

            BotLogic.ECS.BotEntityBridge.RecomputeConsecutiveFailedAssignments(botID);
        }

        public static void InactivateAllJobAssignmentsForBot(string botID)
        {
            var assignments = BotLogic.ECS.BotEntityBridge.GetJobAssignments(botID);
            foreach (BotJobAssignment assignment in assignments)
            {
                assignment.Inactivate();
            }
        }

        public static int NumberOfConsecutiveFailedAssignments(this BotOwner bot)
        {
            return BotLogic.ECS.BotEntityBridge.GetConsecutiveFailedAssignments(bot);
        }

        public static int NumberOfActiveBots(this Quest quest)
        {
            float pendingTimeLimit = 0.3f;

            int num = 0;
            var entities = BotLogic.ECS.BotEntityBridge.Registry.Entities;
            for (int e = 0; e < entities.Count; e++)
            {
                var entity = entities[e];
                if (!entity.IsActive)
                    continue;

                var owner = BotLogic.ECS.BotEntityBridge.GetBotOwner(entity);
                if (owner == null)
                    continue;

                var assignments = BotLogic.ECS.BotEntityBridge.GetJobAssignments(owner);
                for (int i = 0; i < assignments.Count; i++)
                {
                    var a = assignments[i];
                    if (
                        a.StartTime.HasValue
                        && (
                            (a.Status == JobAssignmentStatus.Active)
                            || ((a.Status == JobAssignmentStatus.Pending) && (a.TimeSinceStarted().Value < pendingTimeLimit))
                        )
                        && a.QuestAssignment == quest
                    )
                    {
                        num++;
                    }
                }
            }

            return num;
        }

        public static IEnumerable<QuestObjective> RemainingObjectivesForBot(this Quest quest, BotOwner bot)
        {
            if (bot == null)
            {
                throw new ArgumentNullException("Bot is null", nameof(bot));
            }

            if (quest == null)
            {
                throw new ArgumentNullException("Quest is null", nameof(quest));
            }

            var botAssignments = BotLogic.ECS.BotEntityBridge.GetJobAssignments(bot);
            if (botAssignments.Count == 0)
            {
                return quest.AllObjectives;
            }

            IEnumerable<BotJobAssignment> matchingAssignments = botAssignments
                .Where(a => a.QuestAssignment == quest)
                .Where(a => a.Status != JobAssignmentStatus.Archived);

            return quest.AllObjectives.Where(o => !matchingAssignments.Any(a => a.QuestObjectiveAssignment == o));
        }

        public static QuestObjective NearestToBot(this IEnumerable<QuestObjective> objectives, BotOwner bot)
        {
            QuestObjective nearest = null;
            float nearestDist = float.MaxValue;

            foreach (QuestObjective objective in objectives)
            {
                Vector3? firstStepPosition = objective.GetFirstStepPosition();
                if (!firstStepPosition.HasValue)
                {
                    continue;
                }

                float dist = Vector3.Distance(bot.Position, firstStepPosition.Value);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = objective;
                }
            }

            return nearest;
        }

        public static DateTime? TimeWhenLastEndedForBot(this Quest quest, BotOwner bot)
        {
            var botAssignments = BotLogic.ECS.BotEntityBridge.GetJobAssignments(bot);
            if (botAssignments.Count == 0)
            {
                return null;
            }

            // Find all of the bot's assignments with this quest that have not been archived yet
            IEnumerable<BotJobAssignment> matchingAssignments = botAssignments
                .Where(a => a.QuestAssignment == quest)
                .Where(a => a.Status != JobAssignmentStatus.Archived)
                .Reverse<BotJobAssignment>()
                .SkipWhile(a => !a.EndTime.HasValue);

            if (!matchingAssignments.Any())
            {
                return null;
            }

            return matchingAssignments.First().EndTime;
        }

        public static double? ElapsedTimeWhenLastEndedForBot(this Quest quest, BotOwner bot)
        {
            DateTime? lastObjectiveEndingTime = quest.TimeWhenLastEndedForBot(bot);
            if (!lastObjectiveEndingTime.HasValue)
            {
                return null;
            }

            return (DateTime.Now - lastObjectiveEndingTime.Value).TotalSeconds;
        }

        public static DateTime? TimeWhenBotStarted(this Quest quest, BotOwner bot)
        {
            var botAssignments = BotLogic.ECS.BotEntityBridge.GetJobAssignments(bot);
            if (botAssignments.Count == 0)
            {
                return null;
            }

            // If the bot is currently doing this quest, find the time it first started
            IEnumerable<BotJobAssignment> matchingAssignments = botAssignments
                .Reverse<BotJobAssignment>()
                .TakeWhile(a => a.QuestAssignment == quest);

            if (!matchingAssignments.Any())
            {
                return null;
            }

            return matchingAssignments.Last().EndTime;
        }

        public static double? ElapsedTimeSinceBotStarted(this Quest quest, BotOwner bot)
        {
            DateTime? firstObjectiveEndingTime = quest.TimeWhenBotStarted(bot);
            if (!firstObjectiveEndingTime.HasValue)
            {
                return null;
            }

            return (DateTime.Now - firstObjectiveEndingTime.Value).TotalSeconds;
        }

        public static bool CanAssignToBot(this Quest quest, BotOwner bot)
        {
            if (bot == null)
            {
                throw new ArgumentNullException("Bot is null", nameof(bot));
            }

            if (quest == null)
            {
                throw new ArgumentNullException("Quest is null", nameof(quest));
            }

            // Check if the bot is eligible to do the quest
            if (!quest.CanAssignBot(bot))
            {
                //LoggingController.LogInfo("Cannot assign " + bot.GetText() + " to quest " + quest.ToString());
                return false;
            }

            // If the bot has never been assigned a job, it should be able to do the quest
            // TO DO: Could this return a false positive?
            if (!BotLogic.ECS.BotEntityBridge.HasJobAssignments(bot.Profile.Id))
            {
                return true;
            }

            // Ensure the bot can do at least one of the objectives
            if (!quest.AllObjectives.Any(o => o.CanAssignBot(bot)))
            {
                //LoggingController.LogInfo("Cannot assign " + bot.GetText() + " to any objectives in quest " + quest.ToString());
                return false;
            }

            if (quest.HasBotBeingDoingQuestTooLong(bot, out double? timeDoingQuest))
            {
                return false;
            }

            // Check if at least one of the quest objectives has not been assigned to the bot
            if (quest.RemainingObjectivesForBot(bot).Count() > 0)
            {
                return true;
            }

            // Check if enough time has elasped from the bot's last assignment in the quest
            if (quest.TryArchiveIfBotCanRepeat(bot))
            {
                return true;
            }

            return false;
        }

        public static bool TryArchiveIfBotCanRepeat(this Quest quest, BotOwner bot)
        {
            if (!quest.IsRepeatable)
            {
                return false;
            }

            double? timeSinceQuestEnded = quest.ElapsedTimeWhenLastEndedForBot(bot);
            if (
                timeSinceQuestEnded.HasValue
                && (timeSinceQuestEnded >= ConfigController.Config.Questing.BotQuestingRequirements.RepeatQuestDelay)
            )
            {
                LoggingController.LogInfo(bot.GetText() + " is now allowed to repeat quest " + quest.ToString());

                IEnumerable<BotJobAssignment> matchingAssignments = BotLogic
                    .ECS.BotEntityBridge.GetJobAssignments(bot)
                    .Where(a => a.QuestAssignment == quest);

                foreach (BotJobAssignment assignment in matchingAssignments)
                {
                    assignment.Archive();
                }

                return true;
            }

            return false;
        }

        public static int TryArchiveRepeatableAssignments(this BotOwner bot)
        {
            var assignments = BotLogic.ECS.BotEntityBridge.GetJobAssignments(bot);
            int count = 0;

            for (int i = 0; i < assignments.Count; i++)
            {
                if (assignments[i].QuestAssignment.IsRepeatable && assignments[i].Status == JobAssignmentStatus.Completed)
                {
                    assignments[i].Archive();
                    count++;
                }
            }

            return count;
        }

        public static bool CanBotRepeatQuestObjective(this QuestObjective objective, BotOwner bot)
        {
            IEnumerable<BotJobAssignment> matchingAssignments = BotLogic
                .ECS.BotEntityBridge.GetJobAssignments(bot)
                .Where(a => a.QuestObjectiveAssignment == objective);

            if (!matchingAssignments.Any())
            {
                return true;
            }

            // If the assignment hasn't been archived yet, not enough time has elapsed to repeat it
            if (!objective.IsRepeatable && matchingAssignments.Any(a => a.Status == JobAssignmentStatus.Completed))
            {
                return false;
            }

            return objective.IsRepeatable && matchingAssignments.All(a => a.Status == JobAssignmentStatus.Archived);
        }

        public static bool HasBotBeingDoingQuestTooLong(this Quest quest, BotOwner bot, out double? time)
        {
            time = quest.ElapsedTimeSinceBotStarted(bot);
            if (time.HasValue && (time >= ConfigController.Config.Questing.BotQuestingRequirements.MaxTimePerQuest))
            {
                return true;
            }

            return false;
        }

        public static BotJobAssignment GetCurrentJobAssignment(this BotOwner bot, bool allowUpdate = true)
        {
            var assignments = BotLogic.ECS.BotEntityBridge.EnsureJobAssignments(bot.Profile.Id);

            if (allowUpdate && DoesBotHaveNewJobAssignment(bot))
            {
                // Re-fetch after DoesBotHaveNewJobAssignment may have added a new assignment
                assignments = BotLogic.ECS.BotEntityBridge.GetJobAssignments(bot);
                LoggingController.LogInfo("Bot " + bot.GetText() + " is now doing " + assignments[assignments.Count - 1].ToString());

                if (assignments.Count > 1)
                {
                    BotJobAssignment lastAssignment = assignments[assignments.Count - 2];
                    LoggingController.LogDebug("Bot " + bot.GetText() + " was previously doing " + lastAssignment.ToString());
                }
            }

            if (assignments.Count > 0)
            {
                return assignments[assignments.Count - 1];
            }

            if (allowUpdate)
            {
                LoggingController.LogWarning("Could not get a job assignment for bot " + bot.GetText());
            }

            return null;
        }

        public static bool DoesBotHaveNewJobAssignment(this BotOwner bot)
        {
            var assignments = BotLogic.ECS.BotEntityBridge.EnsureJobAssignments(bot.Profile.Id);

            if (assignments.Count > 0)
            {
                BotJobAssignment currentAssignment = assignments[assignments.Count - 1];

                // Check if the bot is currently doing an assignment
                if (currentAssignment.IsActive)
                {
                    return false;
                }

                // Check if more steps are available for the bot's current assignment
                if (currentAssignment.TrySetNextObjectiveStep(false))
                {
                    return true;
                }

                //LoggingController.LogInfo("There are no more steps available for " + bot.GetText() + " in " + (currentAssignment.QuestObjectiveAssignment?.ToString() ?? "???"));
            }

            if (bot.GetNewBotJobAssignment() != null)
            {
                return true;
            }

            return false;
        }

        public static BotJobAssignment GetNewBotJobAssignment(this BotOwner bot)
        {
            // Do not select another quest objective if the bot wants to extract
            BotObjectiveManager botObjectiveManager = bot.GetObjectiveManager();
            if (botObjectiveManager?.DoesBotWantToExtract() == true)
            {
                return null;
            }

            float maxDistanceBetweenExfils = Singleton<GameWorld>
                .Instance.GetComponent<Components.LocationData>()
                .GetMaxDistanceBetweenExfils();
            float minDistanceToSwitchExfil = maxDistanceBetweenExfils * ConfigController.Config.Questing.BotQuests.ExfilReachedMinFraction;

            // If the bot is close to its selected exfil (only used for quest selection), select a new one
            float? distanceToExfilPoint = botObjectiveManager?.DistanceToExfiltrationPointForQuesting();
            if (distanceToExfilPoint.HasValue && (distanceToExfilPoint.Value < minDistanceToSwitchExfil))
            {
                botObjectiveManager?.SetExfiliationPointForQuesting();
            }

            // Get the bot's most recent assingment if applicable
            var botAssignments = BotLogic.ECS.BotEntityBridge.GetJobAssignments(bot);
            Quest quest = null;
            QuestObjective objective = null;
            if (botAssignments.Count > 0)
            {
                quest = botAssignments[botAssignments.Count - 1].QuestAssignment;
                objective = botAssignments[botAssignments.Count - 1].QuestObjectiveAssignment;
            }

            // Clear the bot's assignment if it's been doing the same quest for too long
            if (quest?.HasBotBeingDoingQuestTooLong(bot, out double? timeDoingQuest) == true)
            {
                LoggingController.LogInfo(
                    bot.GetText()
                        + " has been performing quest "
                        + quest.ToString()
                        + " for "
                        + timeDoingQuest.Value
                        + "s and will get a new one."
                );
                quest = null;
                objective = null;
            }

            // Try to find a quest that has at least one objective that can be assigned to the bot
            List<Quest> invalidQuests = new List<Quest>();
            Stopwatch timeoutMonitor = Stopwatch.StartNew();
            do
            {
                // For zone quests, use field-based selection instead of nearest-to-bot
                if (quest?.Name == ConfigController.Config.Questing.ZoneMovement.QuestName)
                {
                    WorldGridManager gridManager = Singleton<GameWorld>.Instance?.GetComponent<WorldGridManager>();
                    objective = ZoneObjectiveCycler.SelectZoneObjective(bot, quest, gridManager);
                }

                // Find the nearest objective for the bot's currently assigned quest (if any)
                if (objective == null)
                {
                    objective = quest
                        ?.RemainingObjectivesForBot(bot)
                        ?.Where(o => o.CanAssignBot(bot))
                        ?.Where(o => o.CanBotRepeatQuestObjective(bot))
                        ?.NearestToBot(bot);
                }

                // Exit the loop if an objective was found for the bot
                if (objective != null)
                {
                    break;
                }
                if (quest != null)
                {
                    //LoggingController.LogInfo(bot.GetText() + " cannot select quest " + quest.ToString() + " because it has no valid objectives");
                    invalidQuests.Add(quest);
                }

                // If no objectives were found, select another quest
                quest = bot.GetRandomQuest(invalidQuests);

                // If a quest hasn't been found within a certain amount of time, something is wrong
                if (timeoutMonitor.ElapsedMilliseconds > ConfigController.Config.Questing.QuestSelectionTimeout)
                {
                    // First try allowing the bot to repeat quests it already completed
                    if (bot.TryArchiveRepeatableAssignments() > 0)
                    {
                        LoggingController.LogWarning(
                            bot.GetText() + " cannot select any quests. Trying to select a repeatable quest early instead..."
                        );
                        continue;
                    }

                    // If there are still no quests available for the bot to select, give up trying to select one
                    LoggingController.LogError(
                        bot.GetText() + " could not select any of the following quests: " + string.Join(", ", bot.GetAllPossibleQuests())
                    );
                    botObjectiveManager?.StopQuesting();

                    // Try making the bot extract because it has nothing to do
                    if (botObjectiveManager?.BotMonitor?.GetMonitor<BotExtractMonitor>()?.TryInstructBotToExtract() == true)
                    {
                        LoggingController.LogWarning(bot.GetText() + " cannot select any quests. Extracting instead...");
                        return null;
                    }

                    LoggingController.LogError(bot.GetText() + " cannot select any quests. Questing disabled.");
                    return null;
                }
            } while (objective == null);

            // Once a valid assignment is selected, assign it to the bot
            BotJobAssignment assignment = new BotJobAssignment(bot, quest, objective);
            BotLogic.ECS.BotEntityBridge.GetJobAssignments(bot).Add(assignment);

            return assignment;
        }

        public static IReadOnlyList<Quest> GetAllPossibleQuests(this BotOwner bot)
        {
            int botGroupSize = BotLogic.ECS.BotEntityBridge.GetFollowerCount(bot) + 1;
            _possibleQuestsBuffer.Clear();

            for (int i = 0; i < allQuests.Count; i++)
            {
                var q = allQuests[i];
                if (
                    q.Desirability != 0
                    && q.NumberOfValidObjectives > 0
                    && q.MaxBotsInGroup >= botGroupSize
                    && q.CanMoreBotsDoQuest()
                    && q.CanAssignToBot(bot)
                )
                {
                    _possibleQuestsBuffer.Add(q);
                }
            }

            return _possibleQuestsBuffer;
        }

        public static Quest GetRandomQuest(this BotOwner bot, IEnumerable<Quest> invalidQuests)
        {
            // Filter assignable quests into a local list to avoid .ToArray() allocation
            IReadOnlyList<Quest> possibleQuests = bot.GetAllPossibleQuests();
            int questCount = 0;

            // Ensure static buffers are large enough
            if (_questScoreBuffer.Length < possibleQuests.Count)
            {
                int newSize = Math.Max(possibleQuests.Count, 64);
                _questScoreBuffer = new double[newSize];
                _questMinDistBuffer = new float[newSize];
                _questMaxDistBuffer = new float[newSize];
                _questMinAngleBuffer = new float[newSize];
            }

            // We need a separate list for the filtered quests (excluding invalidQuests).
            // Reuse _zoneQuestsBuffer temporarily for this purpose.
            _zoneQuestsBuffer.Clear();
            for (int i = 0; i < possibleQuests.Count; i++)
            {
                if (!invalidQuests.Contains(possibleQuests[i]))
                {
                    _zoneQuestsBuffer.Add(possibleQuests[i]);
                }
            }

            questCount = _zoneQuestsBuffer.Count;
            if (questCount == 0)
            {
                return null;
            }

            BotObjectiveManager botObjectiveManager = bot.GetObjectiveManager();
            Vector3? vectorToExfil = botObjectiveManager?.VectorToExfiltrationPointForQuesting();

            // Calculate per-quest min/max distances and min exfil angles using static buffers
            float maxOverallDistance = 0f;
            for (int qi = 0; qi < questCount; qi++)
            {
                Quest quest = _zoneQuestsBuffer[qi];
                float minDist = float.MaxValue;
                float maxDist = 0f;
                float minAngle = float.MaxValue;

                foreach (QuestObjective objective in quest.ValidObjectives)
                {
                    Vector3? firstPos = objective.GetFirstStepPosition();
                    if (!firstPos.HasValue)
                        continue;

                    float dist = Vector3.Distance(bot.Position, firstPos.Value);
                    if (dist < minDist)
                        minDist = dist;
                    if (dist > maxDist)
                        maxDist = dist;

                    if (vectorToExfil.HasValue)
                    {
                        Vector3 toObjective = firstPos.Value - bot.Position;
                        float angle = Vector3.Angle(toObjective, vectorToExfil.Value);
                        if (angle < minAngle)
                            minAngle = angle;
                    }
                }

                _questMinDistBuffer[qi] = minDist == float.MaxValue ? 0f : minDist;
                _questMaxDistBuffer[qi] = maxDist;
                _questMinAngleBuffer[qi] = vectorToExfil.HasValue && minAngle != float.MaxValue ? minAngle : 0f;

                if (maxDist > maxOverallDistance)
                    maxOverallDistance = maxDist;
            }

            // Build scoring config from current configuration
            float exfilDirectionWeighting = 0;
            string locationId = Singleton<GameWorld>.Instance.GetComponent<Components.LocationData>().CurrentLocation.Id;
            if (ConfigController.Config.Questing.BotQuests.ExfilDirectionWeighting.ContainsKey(locationId))
            {
                exfilDirectionWeighting = ConfigController.Config.Questing.BotQuests.ExfilDirectionWeighting[locationId];
            }
            else if (ConfigController.Config.Questing.BotQuests.ExfilDirectionWeighting.ContainsKey("default"))
            {
                exfilDirectionWeighting = ConfigController.Config.Questing.BotQuests.ExfilDirectionWeighting["default"];
            }

            QuestScoringConfig scoringConfig = new QuestScoringConfig(
                distanceWeighting: ConfigController.Config.Questing.BotQuests.DistanceWeighting,
                desirabilityWeighting: ConfigController.Config.Questing.BotQuests.DesirabilityWeighting,
                exfilDirectionWeighting: exfilDirectionWeighting,
                distanceRandomness: ConfigController.Config.Questing.BotQuests.DistanceRandomness,
                desirabilityRandomness: ConfigController.Config.Questing.BotQuests.DesirabilityRandomness,
                maxExfilAngle: ConfigController.Config.Questing.BotQuests.ExfilDirectionMaxAngle,
                desirabilityActiveQuestMultiplier: ConfigController.Config.Questing.BotQuests.DesirabilityActiveQuestMultiplier
            );

            int maxRandomDistance = (int)
                Math.Ceiling(maxOverallDistance * ConfigController.Config.Questing.BotQuests.DistanceRandomness / 100.0);

            // Score all quests using QuestScorer (O(n) — replaces 5 dictionary allocations + OrderBy)
            System.Random random = new System.Random();
            for (int qi = 0; qi < questCount; qi++)
            {
                _questScoreBuffer[qi] = QuestScorer.ScoreQuest(
                    _questMinDistBuffer[qi],
                    maxOverallDistance,
                    maxRandomDistance,
                    _zoneQuestsBuffer[qi].Desirability,
                    _zoneQuestsBuffer[qi].IsActiveForPlayer,
                    _questMinAngleBuffer[qi],
                    scoringConfig,
                    random
                );
            }

            // Select highest-scoring quest (O(n) — replaces OrderBy + Last)
            int bestIndex = QuestScorer.SelectHighestIndex(_questScoreBuffer, questCount);

            return _zoneQuestsBuffer[bestIndex];
        }

        public static IEnumerable<BotJobAssignment> GetCompletedOrAchivedQuests(this BotOwner bot)
        {
            var assignments = BotLogic.ECS.BotEntityBridge.GetJobAssignments(bot);
            if (assignments.Count == 0)
            {
                return Enumerable.Empty<BotJobAssignment>();
            }

            return assignments.Where(a => a.IsCompletedOrArchived);
        }

        public static int NumberOfCompletedOrAchivedQuests(this BotOwner bot)
        {
            IEnumerable<BotJobAssignment> assignments = bot.GetCompletedOrAchivedQuests();

            return assignments.Distinct(a => a.QuestAssignment).Count();
        }

        public static int NumberOfCompletedOrAchivedEFTQuests(this BotOwner bot)
        {
            IEnumerable<BotJobAssignment> assignments = bot.GetCompletedOrAchivedQuests();

            return assignments.Distinct(a => a.QuestAssignment).Where(a => a.QuestAssignment.IsEFTQuest).Count();
        }

        public static void WriteQuestLogFile(long timestamp)
        {
            if (!ConfigController.Config.Debug.Enabled)
            {
                return;
            }

            LoggingController.LogDebug("Writing quest log file...");

            if (allQuests.Count == 0)
            {
                LoggingController.LogWarning("No quests to log.");
                return;
            }

            // Write the header row
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Quest Name,Objective,Steps,Min Level,Max Level,First Step Position");

            // Write a row for every objective in every quest
            foreach (Quest quest in allQuests)
            {
                foreach (QuestObjective objective in quest.AllObjectives)
                {
                    Vector3? firstPosition = objective.GetFirstStepPosition();
                    if (!firstPosition.HasValue)
                    {
                        continue;
                    }

                    sb.Append(quest.Name.Replace(",", "") + ",");
                    sb.Append("\"" + objective.ToString().Replace(",", "") + "\",");
                    sb.Append(objective.StepCount + ",");
                    sb.Append(quest.MinLevel + ",");
                    sb.Append(quest.MaxLevel + ",");
                    sb.AppendLine((firstPosition.HasValue ? "\"" + firstPosition.Value.ToString() + "\"" : "N/A"));
                }
            }

            string locationId = Singleton<GameWorld>.Instance.GetComponent<Components.LocationData>().CurrentLocation.Id;

            string filename = ConfigController.GetLoggingPath() + locationId.Replace(" ", "") + "_" + timestamp + "_quests.csv";

            LoggingController.CreateLogFile("quest", filename, sb.ToString());
        }

        public static void WriteBotJobAssignmentLogFile(long timestamp)
        {
            if (!ConfigController.Config.Debug.Enabled)
            {
                return;
            }

            LoggingController.LogDebug("Writing bot job assignment log file...");

            var allAssignments = BotLogic.ECS.BotEntityBridge.AllJobAssignments();

            // Write the header row
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(
                "Bot Name,Bot Nickname,Bot Difficulty,Bot Level,Assignment Status,Quest Name,Objective Name,Step Number,Start Time,End Time"
            );

            // Write a row for every quest, objective, and step that each bot was assigned to perform
            foreach (var kvp in allAssignments)
            {
                foreach (BotJobAssignment assignment in kvp.Value)
                {
                    sb.Append(assignment.BotName + ",");
                    sb.Append("\"" + assignment.BotNickname.Replace(",", "") + "\",");
                    sb.Append(assignment.BotOwner.Profile.Info.Settings.BotDifficulty.ToString() + ",");
                    sb.Append(assignment.BotLevel + ",");
                    sb.Append(assignment.Status.ToString() + ",");
                    sb.Append("\"" + (assignment.QuestAssignment?.ToString()?.Replace(",", "") ?? "N/A") + "\",");
                    sb.Append("\"" + (assignment.QuestObjectiveAssignment?.ToString()?.Replace(",", "") ?? "N/A") + "\",");
                    sb.Append("\"" + (assignment.QuestObjectiveStepAssignment?.StepNumber?.ToString() ?? "N/A") + "\",");
                    sb.Append("\"" + (assignment.StartTime?.ToLongTimeString() ?? "N/A") + "\",");
                    sb.AppendLine("\"" + (assignment.EndTime?.ToLongTimeString() ?? "N/A") + "\",");
                }
            }

            foreach (Profile profile in Components.Spawning.BotGenerator.GetAllGeneratedBotProfiles())
            {
                if (BotLogic.ECS.BotEntityBridge.HasJobAssignments(profile.Id))
                {
                    continue;
                }

                sb.Append("[Not Spawned]" + ",");
                sb.Append("\"" + profile.Info.Nickname.Replace(",", "") + "\",");
                sb.Append(profile.Info.Settings.BotDifficulty.ToString() + ",");
                sb.Append(profile.Info.Level + ",");
                sb.AppendLine(",,,,,,");
            }

            string locationId = Singleton<GameWorld>.Instance.GetComponent<Components.LocationData>().CurrentLocation.Id;

            string filename = ConfigController.GetLoggingPath() + locationId.Replace(" ", "") + "_" + timestamp + "_assignments.csv";

            LoggingController.CreateLogFile("bot job assignment", filename, sb.ToString());
        }

        public static IEnumerable<JobAssignment> CreateAllPossibleJobAssignments()
        {
            List<JobAssignment> allAssignments = new List<JobAssignment>();

            foreach (Quest quest in allQuests)
            {
                foreach (QuestObjective objective in quest.ValidObjectives)
                {
                    foreach (QuestObjectiveStep step in objective.AllSteps)
                    {
                        JobAssignment assignment = new JobAssignment(quest, objective, step);
                        allAssignments.Add(assignment);
                    }
                }
            }

            return allAssignments;
        }

        public static IEnumerable<QuestObjective> GetQuestObjectivesNearPosition(
            Vector3 position,
            float distance,
            bool allowEFTQuests = true
        )
        {
            List<QuestObjective> nearbyObjectives = new List<QuestObjective>();

            foreach (Quest quest in allQuests)
            {
                if (!allowEFTQuests && quest.IsEFTQuest)
                {
                    continue;
                }

                foreach (QuestObjective objective in quest.ValidObjectives)
                {
                    if (Vector3.Distance(position, objective.GetFirstStepPosition().Value) > distance)
                    {
                        continue;
                    }

                    nearbyObjectives.Add(objective);
                }
            }

            return nearbyObjectives;
        }

        public static void CheckBotJobAssignmentValidity(BotOwner bot)
        {
            BotJobAssignment botJobAssignment = GetCurrentJobAssignment(bot, false);
            if (botJobAssignment?.QuestAssignment == null)
            {
                return;
            }

            int botGroupSize = BotLogic.ECS.BotEntityBridge.GetFollowerCount(bot) + 1;
            if (botGroupSize > botJobAssignment.QuestAssignment.MaxBotsInGroup)
            {
                BotObjectiveManager botObjectiveManager = bot.GetObjectiveManager();

                if (botObjectiveManager.TryChangeObjective())
                {
                    LoggingController.LogWarning(
                        "Selected new quest for " + bot.GetText() + " because it has too many followers for its previous quest"
                    );
                }
                else
                {
                    LoggingController.LogError(
                        "Cannot select new quest for "
                            + bot.GetText()
                            + ". It has too many followers for quest "
                            + botJobAssignment.QuestAssignment.ToString()
                    );
                }
            }
        }
    }
}
