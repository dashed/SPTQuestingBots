using SPTQuestingBots.Controllers;

namespace SPTQuestingBots
{
    /// <summary>
    /// Synchronizes BepInEx ConfigEntry values with the runtime ModConfig model.
    /// After config.json loads and ConfigEntry fields are bound, this pushes
    /// F12 values into ConfigController.Config so the rest of the code works unchanged.
    /// Called once after BuildConfigOptions and on any SettingChanged event.
    /// </summary>
    public static class ConfigSync
    {
        public static void SyncToModConfig()
        {
            var cfg = ConfigController.Config;
            if (cfg == null)
                return;

            var q = cfg.Questing;

            // 01. General
            cfg.MaxCalcTimePerFrame = QuestingBotsPluginConfig.MaxCalcTimePerFrame.Value;
            q.SquadStrategy.Enabled = QuestingBotsPluginConfig.SquadStrategyEnabled.Value;

            // 02. Bot Spawns
            cfg.BotSpawns.Enabled = QuestingBotsPluginConfig.SpawnsEnabled.Value;
            cfg.BotSpawns.SpawnInitialBossesFirst = QuestingBotsPluginConfig.SpawnInitialBossesFirst.Value;
            cfg.BotSpawns.SpawnRetryTime = QuestingBotsPluginConfig.SpawnRetryTime.Value;
            cfg.BotSpawns.DelayGameStartUntilBotGenFinishes = QuestingBotsPluginConfig.DelayGameStartUntilBotGenFinishes.Value;

            // 03. PMC Spawns (conditional — only bound when BotSpawns.Enabled)
            if (QuestingBotsPluginConfig.PMCSpawnsEnabled != null)
            {
                cfg.BotSpawns.PMCs.Enabled = QuestingBotsPluginConfig.PMCSpawnsEnabled.Value;
                cfg.BotSpawns.PMCs.MinRaidTimeRemaining = QuestingBotsPluginConfig.PMCMinRaidTimeRemaining.Value;
                cfg.BotSpawns.PMCs.MinDistanceFromPlayersInitial = QuestingBotsPluginConfig.PMCMinDistFromPlayersInitial.Value;
                cfg.BotSpawns.PMCs.MinDistanceFromPlayersDuringRaid = QuestingBotsPluginConfig.PMCMinDistFromPlayersDuringRaid.Value;
                cfg.BotSpawns.PMCs.FractionOfMaxPlayers = QuestingBotsPluginConfig.PMCFractionOfMaxPlayers.Value;
            }

            // 04. PScav Spawns (conditional)
            if (QuestingBotsPluginConfig.PScavSpawnsEnabled != null)
            {
                cfg.BotSpawns.PScavs.Enabled = QuestingBotsPluginConfig.PScavSpawnsEnabled.Value;
                cfg.BotSpawns.PScavs.MinRaidTimeRemaining = QuestingBotsPluginConfig.PScavMinRaidTimeRemaining.Value;
                cfg.BotSpawns.PScavs.MinDistanceFromPlayersInitial = QuestingBotsPluginConfig.PScavMinDistFromPlayersInitial.Value;
                cfg.BotSpawns.PScavs.MinDistanceFromPlayersDuringRaid = QuestingBotsPluginConfig.PScavMinDistFromPlayersDuringRaid.Value;
                cfg.BotSpawns.PScavs.FractionOfMaxPlayers = QuestingBotsPluginConfig.PScavFractionOfMaxPlayers.Value;
            }

            // 06. Bot Pathing
            q.BotPathing.UseCustomMover = QuestingBotsPluginConfig.UseCustomMover.Value;
            q.BotPathing.BypassDoorColliders = QuestingBotsPluginConfig.BypassDoorColliders.Value;
            q.BotPathing.IncompletePathRetryInterval = QuestingBotsPluginConfig.IncompletePathRetryInterval.Value;

            // 07. Bot LOD
            q.BotLod.Enabled = QuestingBotsPluginConfig.BotLodEnabled.Value;
            q.BotLod.ReducedDistance = QuestingBotsPluginConfig.BotLodReducedDistance.Value;
            q.BotLod.MinimalDistance = QuestingBotsPluginConfig.BotLodMinimalDistance.Value;
            q.BotLod.ReducedFrameSkip = QuestingBotsPluginConfig.BotLodReducedFrameSkip.Value;
            q.BotLod.MinimalFrameSkip = QuestingBotsPluginConfig.BotLodMinimalFrameSkip.Value;

            // 08. Looting
            q.Looting.Enabled = QuestingBotsPluginConfig.LootingEnabled.Value;
            q.Looting.DetectContainerDistance = QuestingBotsPluginConfig.LootDetectContainerDistance.Value;
            q.Looting.DetectItemDistance = QuestingBotsPluginConfig.LootDetectItemDistance.Value;
            q.Looting.DetectCorpseDistance = QuestingBotsPluginConfig.LootDetectCorpseDistance.Value;
            q.Looting.MinItemValue = QuestingBotsPluginConfig.LootMinItemValue.Value;
            q.Looting.MaxConcurrentLooters = QuestingBotsPluginConfig.LootMaxConcurrentLooters.Value;
            q.Looting.ContainerLootingEnabled = QuestingBotsPluginConfig.LootContainersEnabled.Value;
            q.Looting.LooseItemLootingEnabled = QuestingBotsPluginConfig.LootLooseItemsEnabled.Value;
            q.Looting.CorpseLootingEnabled = QuestingBotsPluginConfig.LootCorpsesEnabled.Value;
            q.Looting.GearSwapEnabled = QuestingBotsPluginConfig.LootGearSwapEnabled.Value;
            q.Looting.LootDuringCombat = QuestingBotsPluginConfig.LootDuringCombat.Value;

            // 09. Vulture
            q.Vulture.Enabled = QuestingBotsPluginConfig.VultureEnabled.Value;
            q.Vulture.BaseDetectionRange = QuestingBotsPluginConfig.VultureDetectionRange.Value;
            q.Vulture.CourageThreshold = QuestingBotsPluginConfig.VultureCourageThreshold.Value;
            q.Vulture.AmbushDuration = QuestingBotsPluginConfig.VultureAmbushDuration.Value;
            q.Vulture.EnableSilentApproach = QuestingBotsPluginConfig.VultureSilentApproach.Value;
            q.Vulture.EnableBaiting = QuestingBotsPluginConfig.VultureBaiting.Value;
            q.Vulture.EnableBossAvoidance = QuestingBotsPluginConfig.VultureBossAvoidance.Value;
            q.Vulture.EnableAirdropVulturing = QuestingBotsPluginConfig.VultureAirdropVulturing.Value;

            // 10. Investigate
            q.Investigate.Enabled = QuestingBotsPluginConfig.InvestigateEnabled.Value;
            q.Investigate.DetectionRange = QuestingBotsPluginConfig.InvestigateDetectionRange.Value;
            q.Investigate.IntensityThreshold = QuestingBotsPluginConfig.InvestigateIntensityThreshold.Value;
            q.Investigate.MovementTimeout = QuestingBotsPluginConfig.InvestigateMovementTimeout.Value;

            // 11. Linger
            q.Linger.Enabled = QuestingBotsPluginConfig.LingerEnabled.Value;
            q.Linger.DurationMin = QuestingBotsPluginConfig.LingerDurationMin.Value;
            q.Linger.DurationMax = QuestingBotsPluginConfig.LingerDurationMax.Value;
            q.Linger.Pose = QuestingBotsPluginConfig.LingerPose.Value;

            // 12. Spawn Entry
            q.SpawnEntry.Enabled = QuestingBotsPluginConfig.SpawnEntryEnabled.Value;
            q.SpawnEntry.BaseDurationMin = QuestingBotsPluginConfig.SpawnEntryDurationMin.Value;
            q.SpawnEntry.BaseDurationMax = QuestingBotsPluginConfig.SpawnEntryDurationMax.Value;
            q.SpawnEntry.Pose = QuestingBotsPluginConfig.SpawnEntryPose.Value;

            // 13. Room Clear
            q.RoomClear.Enabled = QuestingBotsPluginConfig.RoomClearEnabled.Value;
            q.RoomClear.DurationMin = QuestingBotsPluginConfig.RoomClearDurationMin.Value;
            q.RoomClear.DurationMax = QuestingBotsPluginConfig.RoomClearDurationMax.Value;
            q.RoomClear.CornerPauseDuration = QuestingBotsPluginConfig.RoomClearCornerPauseDuration.Value;
            q.RoomClear.Pose = QuestingBotsPluginConfig.RoomClearPose.Value;

            // 14. Patrol
            q.Patrol.Enabled = QuestingBotsPluginConfig.PatrolEnabled.Value;
            q.Patrol.BaseScore = QuestingBotsPluginConfig.PatrolBaseScore.Value;
            q.Patrol.CooldownSec = QuestingBotsPluginConfig.PatrolCooldownSec.Value;
            q.Patrol.Pose = QuestingBotsPluginConfig.PatrolPose.Value;

            // 15. Dynamic Objectives
            q.DynamicObjectives.Enabled = QuestingBotsPluginConfig.DynamicObjectivesEnabled.Value;
            q.DynamicObjectives.ScanIntervalSec = QuestingBotsPluginConfig.DynObjScanIntervalSec.Value;
            q.DynamicObjectives.MaxActiveQuests = QuestingBotsPluginConfig.DynObjMaxActiveQuests.Value;
            q.DynamicObjectives.FirefightEnabled = QuestingBotsPluginConfig.DynObjFirefightEnabled.Value;
            q.DynamicObjectives.CorpseEnabled = QuestingBotsPluginConfig.DynObjCorpseEnabled.Value;
            q.DynamicObjectives.BuildingClearEnabled = QuestingBotsPluginConfig.DynObjBuildingClearEnabled.Value;

            // 16. Personality
            q.Personality.Enabled = QuestingBotsPluginConfig.PersonalityEnabled.Value;
            q.Personality.RaidTimeEnabled = QuestingBotsPluginConfig.RaidTimeProgressionEnabled.Value;

            // 17. Look Variance
            q.LookVariance.Enabled = QuestingBotsPluginConfig.LookVarianceEnabled.Value;

            // 18. Zone Movement
            q.ZoneMovement.Enabled = QuestingBotsPluginConfig.ZoneMovementEnabled.Value;
            q.ZoneMovement.ConvergenceWeight = QuestingBotsPluginConfig.ZoneConvergenceWeight.Value;
            q.ZoneMovement.AdvectionWeight = QuestingBotsPluginConfig.ZoneAdvectionWeight.Value;
            q.ZoneMovement.MomentumWeight = QuestingBotsPluginConfig.ZoneMomentumWeight.Value;
            q.ZoneMovement.NoiseWeight = QuestingBotsPluginConfig.ZoneNoiseWeight.Value;
            q.ZoneMovement.TargetCellCount = QuestingBotsPluginConfig.ZoneTargetCellCount.Value;
        }
    }
}
