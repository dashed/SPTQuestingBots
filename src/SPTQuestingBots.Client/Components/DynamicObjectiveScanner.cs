using System.Collections.Generic;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Helpers;
using SPTQuestingBots.Models.Questing;
using UnityEngine;

namespace SPTQuestingBots.Components
{
    /// <summary>
    /// MonoBehaviour orchestrator for dynamic objective generation.
    /// Periodically scans CombatEventRegistry and passes data to
    /// <see cref="DynamicObjectiveGenerator"/> to create quests at runtime.
    /// Tracks active dynamic quests and removes expired ones.
    /// </summary>
    public class DynamicObjectiveScanner : MonoBehaviour
    {
        private TimePacing _scanPacing;
        private DynamicObjectiveConfig _config;
        private readonly List<TrackedQuest> _activeQuests = new List<TrackedQuest>();
        private Vector3[] _indoorPositions;
        private int _indoorPositionCount;
        private bool _buildingClearGenerated;

        // Reusable buffer for reading events from the registry
        private CombatEvent[] _eventBuffer;

        private struct TrackedQuest
        {
            public Quest Quest;
            public float CreatedTime;
            public float MaxAge;
        }

        protected void Awake()
        {
            _config = ConfigController.Config.Questing.DynamicObjectives;

            if (!_config.Enabled)
            {
                LoggingController.LogInfo("[DynamicObjectiveScanner] Disabled by config");
                enabled = false;
                return;
            }

            _scanPacing = new TimePacing(_config.ScanIntervalSec);
            _eventBuffer = new CombatEvent[CombatEventRegistry.DefaultCapacity];

            GatherIndoorPositions();

            LoggingController.LogInfo(
                "[DynamicObjectiveScanner] Initialized: scan="
                    + _config.ScanIntervalSec
                    + "s, maxActive="
                    + _config.MaxActiveQuests
                    + ", indoor="
                    + _indoorPositionCount
            );
        }

        protected void Update()
        {
            float currentTime = Time.time;

            if (!_scanPacing.ShouldRun(currentTime))
                return;

            RemoveExpiredQuests(currentTime);

            int remaining = _config.MaxActiveQuests - _activeQuests.Count;
            if (remaining <= 0)
                return;

            // Read all active events from registry into buffer
            int eventCount = GatherActiveEvents(currentTime);

            // Generate firefight objectives
            if (_config.FirefightEnabled && remaining > 0)
            {
                var quests = DynamicObjectiveGenerator.GenerateFirefightObjectives(
                    _eventBuffer,
                    eventCount,
                    currentTime,
                    _config.FirefightMaxAgeSec,
                    _config.FirefightClusterRadius,
                    _config.FirefightMinIntensity,
                    _config.FirefightDesirability,
                    remaining
                );

                for (int i = 0; i < quests.Count && remaining > 0; i++)
                {
                    RegisterQuest(quests[i], currentTime, _config.FirefightMaxAgeSec);
                    remaining--;
                }
            }

            // Generate corpse objectives
            if (_config.CorpseEnabled && remaining > 0)
            {
                var quests = DynamicObjectiveGenerator.GenerateCorpseObjectives(
                    _eventBuffer,
                    eventCount,
                    currentTime,
                    _config.CorpseMaxAgeSec,
                    _config.CorpseDesirability,
                    remaining
                );

                for (int i = 0; i < quests.Count && remaining > 0; i++)
                {
                    RegisterQuest(quests[i], currentTime, _config.CorpseMaxAgeSec);
                    remaining--;
                }
            }

            // Generate building clear objectives (once)
            if (_config.BuildingClearEnabled && !_buildingClearGenerated && remaining > 0)
            {
                var quests = DynamicObjectiveGenerator.GenerateBuildingClearObjectives(
                    _indoorPositions,
                    _indoorPositionCount,
                    _config.BuildingClearDesirability,
                    _config.BuildingClearHoldMin,
                    _config.BuildingClearHoldMax,
                    remaining
                );

                for (int i = 0; i < quests.Count && remaining > 0; i++)
                {
                    RegisterQuest(quests[i], currentTime, float.MaxValue); // Building clears don't expire
                    remaining--;
                }

                _buildingClearGenerated = true;
            }
        }

        private void RegisterQuest(Quest quest, float currentTime, float maxAge)
        {
            BotJobAssignmentFactory.AddQuest(quest);
            _activeQuests.Add(
                new TrackedQuest
                {
                    Quest = quest,
                    CreatedTime = currentTime,
                    MaxAge = maxAge,
                }
            );

            LoggingController.LogInfo(
                "[DynamicObjectiveScanner] Registered quest: "
                    + quest.Name
                    + " (desirability="
                    + quest.Desirability
                    + ", maxBots="
                    + quest.MaxBots
                    + ")"
            );
        }

        private void RemoveExpiredQuests(float currentTime)
        {
            for (int i = _activeQuests.Count - 1; i >= 0; i--)
            {
                var tracked = _activeQuests[i];
                if (currentTime - tracked.CreatedTime > tracked.MaxAge)
                {
                    LoggingController.LogDebug("[DynamicObjectiveScanner] Expired quest: " + tracked.Quest.Name);
                    _activeQuests.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Read all active events from CombatEventRegistry into the reusable buffer.
        /// Returns the number of events read.
        /// </summary>
        private int GatherActiveEvents(float currentTime)
        {
            // Use GatherCombatPull's iteration pattern but read raw events.
            // Since CombatEventRegistry doesn't expose a raw event iterator,
            // we rely on the fact that the generator methods filter by age/type internally.
            // Just pass the max age as the larger of firefight and corpse max ages.
            float maxAge = _config.FirefightMaxAgeSec;
            if (_config.CorpseMaxAgeSec > maxAge)
                maxAge = _config.CorpseMaxAgeSec;

            // The buffer is sized to DefaultCapacity, matching the registry.
            // We need to gather events from the registry. Since there's no direct
            // "get all events" API, we use the existing static accessor pattern.
            // For now, return the count — the generator methods accept the buffer.
            return CombatEventRegistry.GatherActiveEvents(_eventBuffer, currentTime, maxAge);
        }

        /// <summary>
        /// Gather zone positions from LocationData spawn points.
        /// Selects a spatially diverse subset of spawn points for building clear objectives.
        /// Used once at startup for building clear objective generation.
        /// </summary>
        private void GatherIndoorPositions()
        {
            _indoorPositions = new Vector3[64];
            _indoorPositionCount = 0;

            var locationData = Comfort.Common.Singleton<EFT.GameWorld>.Instance?.GetComponent<LocationData>();
            if (locationData == null)
                return;

            var spawnPoints = locationData.GetAllValidSpawnPointParams();
            if (spawnPoints == null || spawnPoints.Length == 0)
                return;

            // Select a spatially diverse subset by stepping through spawn points
            int step = spawnPoints.Length > _indoorPositions.Length ? spawnPoints.Length / _indoorPositions.Length : 1;

            for (int i = 0; i < spawnPoints.Length && _indoorPositionCount < _indoorPositions.Length; i += step)
            {
                _indoorPositions[_indoorPositionCount] = spawnPoints[i].Position;
                _indoorPositionCount++;
            }

            LoggingController.LogInfo(
                "[DynamicObjectiveScanner] Gathered "
                    + _indoorPositionCount
                    + " zone positions from "
                    + spawnPoints.Length
                    + " spawn points"
            );
        }
    }
}
