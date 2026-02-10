using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Helpers;
using SPTQuestingBots.Models;
using UnityEngine;

namespace SPTQuestingBots
{
    [Flags]
    public enum TarkovMaps
    {
        Customs = 1,
        Factory = 2,
        Interchange = 4,
        Labs = 8,
        Lighthouse = 16,
        Reserve = 32,
        Shoreline = 64,
        Streets = 128,
        Woods = 256,
        GroundZero = 512,

        All = Customs | Factory | Interchange | Labs | Lighthouse | Reserve | Shoreline | Streets | Woods | GroundZero,
    }

    [Flags]
    public enum BotTypeException
    {
        SniperScavs = 1,
        Rogues = 2,
        Raiders = 4,
        BossesAndFollowers = 8,

        All = SniperScavs | Rogues | Raiders | BossesAndFollowers,
    }

    [Flags]
    public enum QuestingBotType
    {
        QuestingLeaders = 1,
        QuestingFollowers = 2,
        NonQuestingBots = 4,
        AllQuestingBots = QuestingLeaders | QuestingFollowers,
        All = QuestingLeaders | QuestingFollowers | NonQuestingBots,
    }

    [Flags]
    public enum BotPathOverlayType
    {
        QuestTarget = 1,
        EFTTarget = 2,
        EFTCurrentCorner = 4,
        AllEFT = EFTTarget | EFTCurrentCorner,
        All = QuestTarget | EFTTarget | EFTCurrentCorner,
    }

    public static class QuestingBotsPluginConfig
    {
        // ── Lookup Tables ───────────────────────────────────────────
        public static Dictionary<string, TarkovMaps> TarkovMapIDToEnum = new Dictionary<string, TarkovMaps>();
        public static Dictionary<WildSpawnType, BotTypeException> ExceptionFlagForWildSpawnType =
            new Dictionary<WildSpawnType, BotTypeException>();

        // Section constants are defined in ConfigConfigSections.cs (linkable, testable)

        // ── 01. General ─────────────────────────────────────────────
        public static ConfigEntry<bool> QuestingEnabled;
        public static ConfigEntry<bool> SprintingEnabled;
        public static ConfigEntry<int> MinSprintingDistance;
        public static ConfigEntry<bool> UseUtilityAI;
        public static ConfigEntry<bool> SquadStrategyEnabled;
        public static ConfigEntry<float> MaxCalcTimePerFrame;

        // ── 02. Bot Spawns ──────────────────────────────────────────
        public static ConfigEntry<bool> SpawnsEnabled;
        public static ConfigEntry<bool> SpawnInitialBossesFirst;
        public static ConfigEntry<float> SpawnRetryTime;
        public static ConfigEntry<bool> DelayGameStartUntilBotGenFinishes;

        // ── 03. PMC Spawns ──────────────────────────────────────────
        public static ConfigEntry<bool> PMCSpawnsEnabled;
        public static ConfigEntry<float> PMCMinRaidTimeRemaining;
        public static ConfigEntry<float> PMCMinDistFromPlayersInitial;
        public static ConfigEntry<float> PMCMinDistFromPlayersDuringRaid;
        public static ConfigEntry<float> PMCFractionOfMaxPlayers;

        // ── 04. PScav Spawns ────────────────────────────────────────
        public static ConfigEntry<bool> PScavSpawnsEnabled;
        public static ConfigEntry<float> PScavMinRaidTimeRemaining;
        public static ConfigEntry<float> PScavMinDistFromPlayersInitial;
        public static ConfigEntry<float> PScavMinDistFromPlayersDuringRaid;
        public static ConfigEntry<float> PScavFractionOfMaxPlayers;

        // ── 05. Scav Limits ─────────────────────────────────────────
        public static ConfigEntry<bool> ScavLimitsEnabled;
        public static ConfigEntry<float> ScavSpawningExclusionRadiusMapFraction;
        public static ConfigEntry<float> ScavSpawnRateLimit;
        public static ConfigEntry<int> ScavSpawnLimitThreshold;
        public static ConfigEntry<int> ScavMaxAliveLimit;

        // ── 06. Bot Pathing ─────────────────────────────────────────
        public static ConfigEntry<bool> UseCustomMover;
        public static ConfigEntry<bool> BypassDoorColliders;
        public static ConfigEntry<float> IncompletePathRetryInterval;

        // ── 07. Bot LOD ─────────────────────────────────────────────
        public static ConfigEntry<bool> BotLodEnabled;
        public static ConfigEntry<float> BotLodReducedDistance;
        public static ConfigEntry<float> BotLodMinimalDistance;
        public static ConfigEntry<int> BotLodReducedFrameSkip;
        public static ConfigEntry<int> BotLodMinimalFrameSkip;

        // ── 08. Looting ─────────────────────────────────────────────
        public static ConfigEntry<bool> LootingEnabled;
        public static ConfigEntry<float> LootDetectContainerDistance;
        public static ConfigEntry<float> LootDetectItemDistance;
        public static ConfigEntry<float> LootDetectCorpseDistance;
        public static ConfigEntry<int> LootMinItemValue;
        public static ConfigEntry<int> LootMaxConcurrentLooters;
        public static ConfigEntry<bool> LootContainersEnabled;
        public static ConfigEntry<bool> LootLooseItemsEnabled;
        public static ConfigEntry<bool> LootCorpsesEnabled;
        public static ConfigEntry<bool> LootGearSwapEnabled;
        public static ConfigEntry<bool> LootDuringCombat;

        // ── 09. Vulture ─────────────────────────────────────────────
        public static ConfigEntry<bool> VultureEnabled;
        public static ConfigEntry<float> VultureDetectionRange;
        public static ConfigEntry<int> VultureCourageThreshold;
        public static ConfigEntry<float> VultureAmbushDuration;
        public static ConfigEntry<bool> VultureSilentApproach;
        public static ConfigEntry<bool> VultureBaiting;
        public static ConfigEntry<bool> VultureBossAvoidance;
        public static ConfigEntry<bool> VultureAirdropVulturing;

        // ── 10. Investigate ─────────────────────────────────────────
        public static ConfigEntry<bool> InvestigateEnabled;
        public static ConfigEntry<float> InvestigateDetectionRange;
        public static ConfigEntry<int> InvestigateIntensityThreshold;
        public static ConfigEntry<float> InvestigateMovementTimeout;

        // ── 11. Linger ──────────────────────────────────────────────
        public static ConfigEntry<bool> LingerEnabled;
        public static ConfigEntry<float> LingerDurationMin;
        public static ConfigEntry<float> LingerDurationMax;
        public static ConfigEntry<float> LingerPose;

        // ── 12. Spawn Entry ─────────────────────────────────────────
        public static ConfigEntry<bool> SpawnEntryEnabled;
        public static ConfigEntry<float> SpawnEntryDurationMin;
        public static ConfigEntry<float> SpawnEntryDurationMax;
        public static ConfigEntry<float> SpawnEntryPose;

        // ── 13. Room Clear ──────────────────────────────────────────
        public static ConfigEntry<bool> RoomClearEnabled;
        public static ConfigEntry<float> RoomClearDurationMin;
        public static ConfigEntry<float> RoomClearDurationMax;
        public static ConfigEntry<float> RoomClearCornerPauseDuration;
        public static ConfigEntry<float> RoomClearPose;

        // ── 14. Patrol ──────────────────────────────────────────────
        public static ConfigEntry<bool> PatrolEnabled;
        public static ConfigEntry<float> PatrolBaseScore;
        public static ConfigEntry<float> PatrolCooldownSec;
        public static ConfigEntry<float> PatrolPose;

        // ── 15. Dynamic Objectives ──────────────────────────────────
        public static ConfigEntry<bool> DynamicObjectivesEnabled;
        public static ConfigEntry<float> DynObjScanIntervalSec;
        public static ConfigEntry<int> DynObjMaxActiveQuests;
        public static ConfigEntry<bool> DynObjFirefightEnabled;
        public static ConfigEntry<bool> DynObjCorpseEnabled;
        public static ConfigEntry<bool> DynObjBuildingClearEnabled;

        // ── 16. Personality ─────────────────────────────────────────
        public static ConfigEntry<bool> PersonalityEnabled;
        public static ConfigEntry<bool> RaidTimeProgressionEnabled;

        // ── 17. Look Variance ───────────────────────────────────────
        public static ConfigEntry<bool> LookVarianceEnabled;

        // ── 18. Zone Movement ───────────────────────────────────────
        public static ConfigEntry<bool> ZoneMovementEnabled;
        public static ConfigEntry<float> ZoneConvergenceWeight;
        public static ConfigEntry<float> ZoneAdvectionWeight;
        public static ConfigEntry<float> ZoneMomentumWeight;
        public static ConfigEntry<float> ZoneNoiseWeight;
        public static ConfigEntry<int> ZoneTargetCellCount;
        public static ConfigEntry<bool> ZoneMovementDebugOverlay;
        public static ConfigEntry<bool> ZoneMovementDebugMinimap;

        // ── 19. AI Limiter ──────────────────────────────────────────
        public static ConfigEntry<bool> SleepingEnabled;
        public static ConfigEntry<bool> SleepingEnabledForQuestingBots;
        public static ConfigEntry<int> SleepingMinDistanceToHumansGlobal;
        public static ConfigEntry<int> SleepingMinDistanceToHumansCustoms;
        public static ConfigEntry<int> SleepingMinDistanceToHumansFactory;
        public static ConfigEntry<int> SleepingMinDistanceToHumansInterchange;
        public static ConfigEntry<int> SleepingMinDistanceToHumansLabs;
        public static ConfigEntry<int> SleepingMinDistanceToHumansLighthouse;
        public static ConfigEntry<int> SleepingMinDistanceToHumansReserve;
        public static ConfigEntry<int> SleepingMinDistanceToHumansShoreline;
        public static ConfigEntry<int> SleepingMinDistanceToHumansStreets;
        public static ConfigEntry<int> SleepingMinDistanceToHumansWoods;
        public static ConfigEntry<int> SleepingMinDistanceToHumansGroundZero;
        public static ConfigEntry<int> SleepingMinDistanceToQuestingBots;
        public static ConfigEntry<TarkovMaps> MapsToAllowSleepingForQuestingBots;
        public static ConfigEntry<BotTypeException> SleeplessBotTypes;
        public static ConfigEntry<int> MinBotsToEnableSleeping;

        // ── 20. Debug ───────────────────────────────────────────────
        public static ConfigEntry<bool> ShowSpawnDebugMessages;
        public static ConfigEntry<QuestingBotType> ShowBotInfoOverlays;
        public static ConfigEntry<QuestingBotType> ShowBotPathOverlays;
        public static ConfigEntry<QuestingBotType> ShowBotPathVisualizations;
        public static ConfigEntry<BotPathOverlayType> BotPathOverlayTypes;
        public static ConfigEntry<bool> ShowQuestInfoOverlays;
        public static ConfigEntry<bool> ShowQuestInfoForSpawnSearchQuests;
        public static ConfigEntry<int> QuestOverlayFontSize;
        public static ConfigEntry<int> QuestOverlayMaxDistance;
        public static ConfigEntry<string> BotFilter;

        // ── 21. Custom Quest Locations ──────────────────────────────
        public static ConfigEntry<bool> CreateQuestLocations;
        public static ConfigEntry<bool> ShowCurrentLocation;
        public static ConfigEntry<string> QuestLocationName;
        public static ConfigEntry<KeyboardShortcut> StoreQuestLocationKey;

        public static void BuildConfigOptions(ConfigFile Config)
        {
            indexMapIDs();
            indexWildSpawnTypeExceptions();

            var cfg = ConfigController.Config;
            var q = cfg.Questing;

            // ── 01. General ─────────────────────────────────────────
            QuestingEnabled = Config.Bind(ConfigSections.General, "Enable Questing", true, "Allow bots to quest");
            SprintingEnabled = Config.Bind(
                ConfigSections.General,
                "Allow Bots to Sprint while Questing",
                true,
                "Allow bots to sprint while questing. This does not affect their ability to sprint when they're not questing."
            );
            MinSprintingDistance = Config.Bind(
                ConfigSections.General,
                "Sprinting Distance Limit from Objectives (m)",
                3,
                new ConfigDescription(
                    "Bots will not be allowed to sprint if they are within this distance from their objective",
                    new AcceptableValueRange<int>(0, 75)
                )
            );
            UseUtilityAI = Config.Bind(
                ConfigSections.General,
                "Use Utility AI for Action Selection",
                true,
                new ConfigDescription(
                    "Use utility AI for action selection instead of switch-based dispatch",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            SquadStrategyEnabled = Config.Bind(
                ConfigSections.General,
                "Enable Squad Strategies",
                q.SquadStrategy.Enabled,
                new ConfigDescription(
                    "Enable Phobos-style squad tactical strategies for follower bots",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            MaxCalcTimePerFrame = Config.Bind(
                ConfigSections.General,
                "Max Calculation Time Per Frame (ms)",
                cfg.MaxCalcTimePerFrame,
                new ConfigDescription(
                    "Maximum milliseconds per frame allowed for bot pathing calculations. Lower = better FPS but slower bot reactions.",
                    new AcceptableValueRange<float>(1f, 20f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );

            // ── 02. Bot Spawns ──────────────────────────────────────
            SpawnsEnabled = Config.Bind(
                ConfigSections.BotSpawns,
                "Enable Custom Bot Spawns",
                cfg.BotSpawns.Enabled,
                "Enable the custom bot spawning system (PMCs, PScavs). Requires game restart to take effect."
            );
            SpawnInitialBossesFirst = Config.Bind(
                ConfigSections.BotSpawns,
                "Spawn Initial Bosses First",
                cfg.BotSpawns.SpawnInitialBossesFirst,
                new ConfigDescription(
                    "Prioritize spawning bosses before PMCs at raid start",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            SpawnRetryTime = Config.Bind(
                ConfigSections.BotSpawns,
                "Spawn Retry Time (s)",
                cfg.BotSpawns.SpawnRetryTime,
                new ConfigDescription(
                    "Seconds between spawn retry attempts when a spawn fails",
                    new AcceptableValueRange<float>(1f, 60f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            DelayGameStartUntilBotGenFinishes = Config.Bind(
                ConfigSections.BotSpawns,
                "Delay Game Start Until Bot Gen Finishes",
                cfg.BotSpawns.DelayGameStartUntilBotGenFinishes,
                new ConfigDescription(
                    "Hold the loading screen until all initial bots are generated",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );

            // ── 03. PMC Spawns ──────────────────────────────────────
            if (cfg.BotSpawns.Enabled)
            {
                PMCSpawnsEnabled = Config.Bind(
                    ConfigSections.PMCSpawns,
                    "Enable PMC Spawns",
                    cfg.BotSpawns.PMCs.Enabled,
                    "Enable custom PMC bot spawning"
                );
                PMCMinRaidTimeRemaining = Config.Bind(
                    ConfigSections.PMCSpawns,
                    "Min Raid Time Remaining (s)",
                    cfg.BotSpawns.PMCs.MinRaidTimeRemaining,
                    new ConfigDescription(
                        "PMCs will not spawn if less than this many seconds remain in the raid",
                        new AcceptableValueRange<float>(0f, 3600f)
                    )
                );
                PMCMinDistFromPlayersInitial = Config.Bind(
                    ConfigSections.PMCSpawns,
                    "Min Distance from Players at Start (m)",
                    cfg.BotSpawns.PMCs.MinDistanceFromPlayersInitial,
                    new ConfigDescription(
                        "Minimum distance from human players for initial PMC spawns",
                        new AcceptableValueRange<float>(0f, 500f)
                    )
                );
                PMCMinDistFromPlayersDuringRaid = Config.Bind(
                    ConfigSections.PMCSpawns,
                    "Min Distance from Players During Raid (m)",
                    cfg.BotSpawns.PMCs.MinDistanceFromPlayersDuringRaid,
                    new ConfigDescription(
                        "Minimum distance from human players for PMC spawns during the raid",
                        new AcceptableValueRange<float>(0f, 500f)
                    )
                );
                PMCFractionOfMaxPlayers = Config.Bind(
                    ConfigSections.PMCSpawns,
                    "Fraction of Max Players",
                    cfg.BotSpawns.PMCs.FractionOfMaxPlayers,
                    new ConfigDescription(
                        "Fraction of max player slots to fill with PMC bots (0.0-2.0)",
                        new AcceptableValueRange<float>(0f, 2f)
                    )
                );

                // ── 04. PScav Spawns ────────────────────────────────
                PScavSpawnsEnabled = Config.Bind(
                    ConfigSections.PScavSpawns,
                    "Enable PScav Spawns",
                    cfg.BotSpawns.PScavs.Enabled,
                    "Enable custom player-Scav bot spawning"
                );
                PScavMinRaidTimeRemaining = Config.Bind(
                    ConfigSections.PScavSpawns,
                    "Min Raid Time Remaining (s)",
                    cfg.BotSpawns.PScavs.MinRaidTimeRemaining,
                    new ConfigDescription(
                        "PScavs will not spawn if less than this many seconds remain in the raid",
                        new AcceptableValueRange<float>(0f, 3600f)
                    )
                );
                PScavMinDistFromPlayersInitial = Config.Bind(
                    ConfigSections.PScavSpawns,
                    "Min Distance from Players at Start (m)",
                    cfg.BotSpawns.PScavs.MinDistanceFromPlayersInitial,
                    new ConfigDescription(
                        "Minimum distance from human players for initial PScav spawns",
                        new AcceptableValueRange<float>(0f, 500f)
                    )
                );
                PScavMinDistFromPlayersDuringRaid = Config.Bind(
                    ConfigSections.PScavSpawns,
                    "Min Distance from Players During Raid (m)",
                    cfg.BotSpawns.PScavs.MinDistanceFromPlayersDuringRaid,
                    new ConfigDescription(
                        "Minimum distance from human players for PScav spawns during the raid",
                        new AcceptableValueRange<float>(0f, 500f)
                    )
                );
                PScavFractionOfMaxPlayers = Config.Bind(
                    ConfigSections.PScavSpawns,
                    "Fraction of Max Players",
                    cfg.BotSpawns.PScavs.FractionOfMaxPlayers,
                    new ConfigDescription(
                        "Fraction of max player slots to fill with PScav bots (0.0-2.0)",
                        new AcceptableValueRange<float>(0f, 2f)
                    )
                );

                // ── 05. Scav Limits ─────────────────────────────────
                ScavLimitsEnabled = Config.Bind(
                    ConfigSections.ScavLimits,
                    "Enable Scav Spawn Restrictions",
                    true,
                    "Restrict where and how frequently Scavs are allowed to spawn"
                );
                ScavSpawningExclusionRadiusMapFraction = Config.Bind(
                    ConfigSections.ScavLimits,
                    "Map Fraction for Scav Spawning Exclusion Radius",
                    0.1f,
                    new ConfigDescription(
                        "Adjusts the distance (relative to the map size) that Scavs are allowed to spawn near human players, PMC's, and player Scavs",
                        new AcceptableValueRange<float>(0.01f, 0.15f)
                    )
                );
                ScavSpawnRateLimit = Config.Bind(
                    ConfigSections.ScavLimits,
                    "Permitted Scav Spawn Rate",
                    2.5f,
                    new ConfigDescription(
                        "After the Scav spawn threshold is exceeded, only this number of Scavs will be allowed to spawn per minute (on average)",
                        new AcceptableValueRange<float>(0.5f, 6f)
                    )
                );
                ScavSpawnLimitThreshold = Config.Bind(
                    ConfigSections.ScavLimits,
                    "Threshold for Scav Spawn Rate Limit",
                    10,
                    new ConfigDescription(
                        "The Scav spawn rate limit will only be active after this many Scavs spawn in the raid",
                        new AcceptableValueRange<int>(1, 50)
                    )
                );
                ScavMaxAliveLimit = Config.Bind(
                    ConfigSections.ScavLimits,
                    "Max Alive Scavs",
                    15,
                    new ConfigDescription(
                        "The maximum number of Scavs that can be alive at the same time (including Sniper Scavs)",
                        new AcceptableValueRange<int>(5, 25)
                    )
                );
            }

            // ── 06. Bot Pathing ─────────────────────────────────────
            UseCustomMover = Config.Bind(
                ConfigSections.BotPathing,
                "Use Custom Mover",
                q.BotPathing.UseCustomMover,
                new ConfigDescription(
                    "Use Phobos-style Player.Move() replacement for smoother bot movement. Requires game restart.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            BypassDoorColliders = Config.Bind(
                ConfigSections.BotPathing,
                "Bypass Door Colliders",
                q.BotPathing.BypassDoorColliders,
                new ConfigDescription(
                    "Shrink door NavMesh carvers so bots can path through doorways. Requires game restart.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            IncompletePathRetryInterval = Config.Bind(
                ConfigSections.BotPathing,
                "Incomplete Path Retry Interval (s)",
                q.BotPathing.IncompletePathRetryInterval,
                new ConfigDescription(
                    "Seconds to wait before retrying an incomplete NavMesh path",
                    new AcceptableValueRange<float>(1f, 30f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );

            // ── 07. Bot LOD ─────────────────────────────────────────
            BotLodEnabled = Config.Bind(
                ConfigSections.BotLOD,
                "Enable Bot LOD",
                q.BotLod.Enabled,
                "Enable distance-based update throttling for bots. Reduces CPU usage for distant bots."
            );
            BotLodReducedDistance = Config.Bind(
                ConfigSections.BotLOD,
                "Reduced Update Distance (m)",
                q.BotLod.ReducedDistance,
                new ConfigDescription("Bots beyond this distance get reduced update frequency", new AcceptableValueRange<float>(50f, 500f))
            );
            BotLodMinimalDistance = Config.Bind(
                ConfigSections.BotLOD,
                "Minimal Update Distance (m)",
                q.BotLod.MinimalDistance,
                new ConfigDescription(
                    "Bots beyond this distance get minimal update frequency",
                    new AcceptableValueRange<float>(100f, 1000f)
                )
            );
            BotLodReducedFrameSkip = Config.Bind(
                ConfigSections.BotLOD,
                "Reduced Tier Frame Skip",
                q.BotLod.ReducedFrameSkip,
                new ConfigDescription(
                    "Number of frames to skip between updates for reduced-tier bots",
                    new AcceptableValueRange<int>(1, 10),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            BotLodMinimalFrameSkip = Config.Bind(
                ConfigSections.BotLOD,
                "Minimal Tier Frame Skip",
                q.BotLod.MinimalFrameSkip,
                new ConfigDescription(
                    "Number of frames to skip between updates for minimal-tier bots",
                    new AcceptableValueRange<int>(2, 20),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );

            // ── 08. Looting ─────────────────────────────────────────
            LootingEnabled = Config.Bind(
                ConfigSections.Looting,
                "Enable Looting",
                q.Looting.Enabled,
                "Enable the hybrid looting system. Bots will search containers, pick up items, and loot corpses."
            );
            LootDetectContainerDistance = Config.Bind(
                ConfigSections.Looting,
                "Container Detection Distance (m)",
                q.Looting.DetectContainerDistance,
                new ConfigDescription(
                    "Maximum distance at which bots can detect lootable containers",
                    new AcceptableValueRange<float>(10f, 200f)
                )
            );
            LootDetectItemDistance = Config.Bind(
                ConfigSections.Looting,
                "Item Detection Distance (m)",
                q.Looting.DetectItemDistance,
                new ConfigDescription(
                    "Maximum distance at which bots can detect loose items on the ground",
                    new AcceptableValueRange<float>(10f, 200f)
                )
            );
            LootDetectCorpseDistance = Config.Bind(
                ConfigSections.Looting,
                "Corpse Detection Distance (m)",
                q.Looting.DetectCorpseDistance,
                new ConfigDescription(
                    "Maximum distance at which bots can detect lootable corpses",
                    new AcceptableValueRange<float>(10f, 200f)
                )
            );
            LootMinItemValue = Config.Bind(
                ConfigSections.Looting,
                "Min Item Value (Roubles)",
                q.Looting.MinItemValue,
                new ConfigDescription(
                    "Minimum flea-market value for an item to be considered worth looting",
                    new AcceptableValueRange<int>(0, 100000)
                )
            );
            LootMaxConcurrentLooters = Config.Bind(
                ConfigSections.Looting,
                "Max Concurrent Looters",
                q.Looting.MaxConcurrentLooters,
                new ConfigDescription(
                    "Maximum number of bots that can be actively looting at the same time",
                    new AcceptableValueRange<int>(1, 20)
                )
            );
            LootContainersEnabled = Config.Bind(
                ConfigSections.Looting,
                "Enable Container Looting",
                q.Looting.ContainerLootingEnabled,
                "Allow bots to open and search lootable containers"
            );
            LootLooseItemsEnabled = Config.Bind(
                ConfigSections.Looting,
                "Enable Loose Item Looting",
                q.Looting.LooseItemLootingEnabled,
                "Allow bots to pick up loose items found on the ground"
            );
            LootCorpsesEnabled = Config.Bind(
                ConfigSections.Looting,
                "Enable Corpse Looting",
                q.Looting.CorpseLootingEnabled,
                "Allow bots to search corpses for loot"
            );
            LootGearSwapEnabled = Config.Bind(
                ConfigSections.Looting,
                "Enable Gear Swapping",
                q.Looting.GearSwapEnabled,
                "Allow bots to swap their gear for better items found as loot"
            );
            LootDuringCombat = Config.Bind(
                ConfigSections.Looting,
                "Allow Looting During Combat",
                q.Looting.LootDuringCombat,
                new ConfigDescription(
                    "Allow bots to loot while engaged in combat (not recommended for realism)",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );

            // ── 09. Vulture ─────────────────────────────────────────
            VultureEnabled = Config.Bind(
                ConfigSections.Vulture,
                "Enable Vulture Behavior",
                q.Vulture.Enabled,
                "Bots hear gunfire and move to ambush weakened survivors"
            );
            VultureDetectionRange = Config.Bind(
                ConfigSections.Vulture,
                "Detection Range (m)",
                q.Vulture.BaseDetectionRange,
                new ConfigDescription(
                    "Base detection range in meters for hearing combat events",
                    new AcceptableValueRange<float>(50f, 500f)
                )
            );
            VultureCourageThreshold = Config.Bind(
                ConfigSections.Vulture,
                "Courage Threshold",
                q.Vulture.CourageThreshold,
                new ConfigDescription(
                    "Minimum combat intensity required for a bot to trigger vulture behavior",
                    new AcceptableValueRange<int>(1, 50)
                )
            );
            VultureAmbushDuration = Config.Bind(
                ConfigSections.Vulture,
                "Ambush Duration (s)",
                q.Vulture.AmbushDuration,
                new ConfigDescription(
                    "Maximum time in seconds to hold at ambush position before rushing",
                    new AcceptableValueRange<float>(10f, 300f)
                )
            );
            VultureSilentApproach = Config.Bind(
                ConfigSections.Vulture,
                "Enable Silent Approach",
                q.Vulture.EnableSilentApproach,
                "Bots slow down and crouch when close to the target"
            );
            VultureBaiting = Config.Bind(
                ConfigSections.Vulture,
                "Enable Baiting",
                q.Vulture.EnableBaiting,
                new ConfigDescription(
                    "Bots may fire shots to bait enemies during ambush",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            VultureBossAvoidance = Config.Bind(
                ConfigSections.Vulture,
                "Enable Boss Avoidance",
                q.Vulture.EnableBossAvoidance,
                "Bots avoid areas with recent boss activity"
            );
            VultureAirdropVulturing = Config.Bind(
                ConfigSections.Vulture,
                "Enable Airdrop Vulturing",
                q.Vulture.EnableAirdropVulturing,
                "Bots vulture toward airdrop landing positions"
            );

            // ── 10. Investigate ─────────────────────────────────────
            InvestigateEnabled = Config.Bind(
                ConfigSections.Investigate,
                "Enable Investigate Behavior",
                q.Investigate.Enabled,
                "Bots hear nearby gunfire and cautiously approach to check it out"
            );
            InvestigateDetectionRange = Config.Bind(
                ConfigSections.Investigate,
                "Detection Range (m)",
                q.Investigate.DetectionRange,
                new ConfigDescription("Detection range in meters for hearing combat events", new AcceptableValueRange<float>(30f, 300f))
            );
            InvestigateIntensityThreshold = Config.Bind(
                ConfigSections.Investigate,
                "Intensity Threshold",
                q.Investigate.IntensityThreshold,
                new ConfigDescription(
                    "Minimum combat intensity to trigger investigation (lower than vulture threshold)",
                    new AcceptableValueRange<int>(1, 30)
                )
            );
            InvestigateMovementTimeout = Config.Bind(
                ConfigSections.Investigate,
                "Movement Timeout (s)",
                q.Investigate.MovementTimeout,
                new ConfigDescription(
                    "Maximum time in seconds for the entire investigation before timeout",
                    new AcceptableValueRange<float>(10f, 120f)
                )
            );

            // ── 11. Linger ──────────────────────────────────────────
            LingerEnabled = Config.Bind(
                ConfigSections.Linger,
                "Enable Linger Behavior",
                q.Linger.Enabled,
                "Bots pause briefly after completing an objective, looking around before moving on"
            );
            LingerDurationMin = Config.Bind(
                ConfigSections.Linger,
                "Min Duration (s)",
                q.Linger.DurationMin,
                new ConfigDescription("Minimum linger duration in seconds", new AcceptableValueRange<float>(1f, 60f))
            );
            LingerDurationMax = Config.Bind(
                ConfigSections.Linger,
                "Max Duration (s)",
                q.Linger.DurationMax,
                new ConfigDescription("Maximum linger duration in seconds", new AcceptableValueRange<float>(5f, 120f))
            );
            LingerPose = Config.Bind(
                ConfigSections.Linger,
                "Pose",
                q.Linger.Pose,
                new ConfigDescription(
                    "Bot pose while lingering (0=crouch, 1=standing)",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );

            // ── 12. Spawn Entry ─────────────────────────────────────
            SpawnEntryEnabled = Config.Bind(
                ConfigSections.SpawnEntry,
                "Enable Spawn Entry Behavior",
                q.SpawnEntry.Enabled,
                "Bots pause briefly after spawning to check surroundings before rushing to objectives"
            );
            SpawnEntryDurationMin = Config.Bind(
                ConfigSections.SpawnEntry,
                "Min Duration (s)",
                q.SpawnEntry.BaseDurationMin,
                new ConfigDescription("Minimum spawn entry pause duration in seconds", new AcceptableValueRange<float>(0f, 15f))
            );
            SpawnEntryDurationMax = Config.Bind(
                ConfigSections.SpawnEntry,
                "Max Duration (s)",
                q.SpawnEntry.BaseDurationMax,
                new ConfigDescription("Maximum spawn entry pause duration in seconds", new AcceptableValueRange<float>(1f, 30f))
            );
            SpawnEntryPose = Config.Bind(
                ConfigSections.SpawnEntry,
                "Pose",
                q.SpawnEntry.Pose,
                new ConfigDescription(
                    "Bot pose during spawn entry (0=crouch, 1=standing)",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );

            // ── 13. Room Clear ──────────────────────────────────────
            RoomClearEnabled = Config.Bind(
                ConfigSections.RoomClear,
                "Enable Room Clearing",
                q.RoomClear.Enabled,
                "Bots slow down and adopt a cautious posture when transitioning from outdoor to indoor"
            );
            RoomClearDurationMin = Config.Bind(
                ConfigSections.RoomClear,
                "Min Duration (s)",
                q.RoomClear.DurationMin,
                new ConfigDescription(
                    "Minimum room clear duration in seconds after entering indoors",
                    new AcceptableValueRange<float>(5f, 60f)
                )
            );
            RoomClearDurationMax = Config.Bind(
                ConfigSections.RoomClear,
                "Max Duration (s)",
                q.RoomClear.DurationMax,
                new ConfigDescription(
                    "Maximum room clear duration in seconds after entering indoors",
                    new AcceptableValueRange<float>(10f, 120f)
                )
            );
            RoomClearCornerPauseDuration = Config.Bind(
                ConfigSections.RoomClear,
                "Corner Pause Duration (s)",
                q.RoomClear.CornerPauseDuration,
                new ConfigDescription(
                    "Duration of brief pause at sharp corners during room clearing",
                    new AcceptableValueRange<float>(0f, 5f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            RoomClearPose = Config.Bind(
                ConfigSections.RoomClear,
                "Pose",
                q.RoomClear.Pose,
                new ConfigDescription(
                    "Bot pose during room clearing (0=crouch, 1=standing)",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );

            // ── 14. Patrol ──────────────────────────────────────────
            PatrolEnabled = Config.Bind(
                ConfigSections.Patrol,
                "Enable Patrol Routes",
                q.Patrol.Enabled,
                "Bots follow structured patrol paths between quest objectives"
            );
            PatrolBaseScore = Config.Bind(
                ConfigSections.Patrol,
                "Base Utility Score",
                q.Patrol.BaseScore,
                new ConfigDescription(
                    "Base utility score for patrol behavior in the utility AI",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            PatrolCooldownSec = Config.Bind(
                ConfigSections.Patrol,
                "Cooldown (s)",
                q.Patrol.CooldownSec,
                new ConfigDescription("Cooldown in seconds after completing a patrol route", new AcceptableValueRange<float>(10f, 600f))
            );
            PatrolPose = Config.Bind(
                ConfigSections.Patrol,
                "Pose",
                q.Patrol.Pose,
                new ConfigDescription(
                    "Bot pose while patrolling (0=crouch, 1=standing)",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );

            // ── 15. Dynamic Objectives ──────────────────────────────
            DynamicObjectivesEnabled = Config.Bind(
                ConfigSections.DynamicObjectives,
                "Enable Dynamic Objectives",
                q.DynamicObjectives.Enabled,
                "Generate quests from live game state: firefight clusters, corpse scavenging, building clears"
            );
            DynObjScanIntervalSec = Config.Bind(
                ConfigSections.DynamicObjectives,
                "Scan Interval (s)",
                q.DynamicObjectives.ScanIntervalSec,
                new ConfigDescription(
                    "Interval in seconds between scans for new dynamic objectives",
                    new AcceptableValueRange<float>(5f, 120f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            DynObjMaxActiveQuests = Config.Bind(
                ConfigSections.DynamicObjectives,
                "Max Active Quests",
                q.DynamicObjectives.MaxActiveQuests,
                new ConfigDescription("Maximum number of active dynamic quests at any time", new AcceptableValueRange<int>(1, 50))
            );
            DynObjFirefightEnabled = Config.Bind(
                ConfigSections.DynamicObjectives,
                "Enable Firefight Objectives",
                q.DynamicObjectives.FirefightEnabled,
                "Generate investigation objectives from clustered combat events"
            );
            DynObjCorpseEnabled = Config.Bind(
                ConfigSections.DynamicObjectives,
                "Enable Corpse Scavenging Objectives",
                q.DynamicObjectives.CorpseEnabled,
                "Generate scavenging objectives around fresh corpse locations"
            );
            DynObjBuildingClearEnabled = Config.Bind(
                ConfigSections.DynamicObjectives,
                "Enable Building Clear Objectives",
                q.DynamicObjectives.BuildingClearEnabled,
                "Generate building-clear objectives for indoor areas with activity"
            );

            // ── 16. Personality ─────────────────────────────────────
            PersonalityEnabled = Config.Bind(
                ConfigSections.Personality,
                "Enable Personality Modifiers",
                q.Personality.Enabled,
                "Bot difficulty influences utility AI scoring via aggression-based multipliers"
            );
            RaidTimeProgressionEnabled = Config.Bind(
                ConfigSections.Personality,
                "Enable Raid Time Progression",
                q.Personality.RaidTimeEnabled,
                "Shift bot behavior from early rush to late-raid caution based on elapsed raid time"
            );

            // ── 17. Look Variance ───────────────────────────────────
            LookVarianceEnabled = Config.Bind(
                ConfigSections.LookVariance,
                "Enable Look Variance",
                q.LookVariance.Enabled,
                "Bots periodically glance to the side, toward combat events, or at squad members while moving"
            );

            // ── 18. Zone Movement ───────────────────────────────────
            ZoneMovementEnabled = Config.Bind(
                ConfigSections.ZoneMovement,
                "Enable Zone Movement",
                q.ZoneMovement.Enabled,
                "Enable zone-based movement for bots without quests. Bots will move toward interesting areas using physics-inspired vector fields."
            );
            ZoneConvergenceWeight = Config.Bind(
                ConfigSections.ZoneMovement,
                "Convergence Weight",
                q.ZoneMovement.ConvergenceWeight,
                new ConfigDescription(
                    "Weight for the convergence (player attraction) field component",
                    new AcceptableValueRange<float>(0f, 5f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            ZoneAdvectionWeight = Config.Bind(
                ConfigSections.ZoneMovement,
                "Advection Weight",
                q.ZoneMovement.AdvectionWeight,
                new ConfigDescription(
                    "Weight for the advection (zone attraction + crowd repulsion) field component",
                    new AcceptableValueRange<float>(0f, 5f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            ZoneMomentumWeight = Config.Bind(
                ConfigSections.ZoneMovement,
                "Momentum Weight",
                q.ZoneMovement.MomentumWeight,
                new ConfigDescription(
                    "Weight for the momentum (travel direction smoothing) field component",
                    new AcceptableValueRange<float>(0f, 5f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            ZoneNoiseWeight = Config.Bind(
                ConfigSections.ZoneMovement,
                "Noise Weight",
                q.ZoneMovement.NoiseWeight,
                new ConfigDescription(
                    "Weight for the noise (random rotation) field component",
                    new AcceptableValueRange<float>(0f, 5f),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            ZoneTargetCellCount = Config.Bind(
                ConfigSections.ZoneMovement,
                "Target Cell Count",
                q.ZoneMovement.TargetCellCount,
                new ConfigDescription(
                    "Target number of grid cells for map partitioning. More cells = finer movement resolution.",
                    new AcceptableValueRange<int>(50, 500),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            ZoneMovementDebugOverlay = Config.Bind(
                ConfigSections.ZoneMovement,
                "Show Debug Overlay",
                false,
                new ConfigDescription(
                    "Show zone movement debug overlay with grid stats, player cell info, and field data",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            ZoneMovementDebugMinimap = Config.Bind(
                ConfigSections.ZoneMovement,
                "Show Debug Minimap",
                false,
                new ConfigDescription(
                    "Show a 2D minimap overlay visualizing grid cells, field vectors, bot positions, and zone sources",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );

            // ── 19. AI Limiter ──────────────────────────────────────
            int minDistanceAILimitNormal = cfg.Debug.Enabled && cfg.Debug.AllowZeroDistanceSleeping ? 0 : 50;
            int minDistanceAILimitQuesting = cfg.Debug.Enabled && cfg.Debug.AllowZeroDistanceSleeping ? 0 : 25;

            SleepingEnabled = Config.Bind(
                ConfigSections.AILimiter,
                "Enable AI Limiting",
                false,
                "Improve FPS by minimizing CPU load for AI out of certain ranges"
            );
            SleepingEnabledForQuestingBots = Config.Bind(
                ConfigSections.AILimiter,
                "Enable AI Limiting for Bots That Are Questing",
                true,
                "Allow AI to be disabled for bots that are questing"
            );
            MapsToAllowSleepingForQuestingBots = Config.Bind(
                ConfigSections.AILimiter,
                "Maps to Allow AI Limiting for Bots That Are Questing",
                TarkovMaps.Streets,
                "Only allow AI to be disabled for bots that are questing on the selected maps"
            );
            SleeplessBotTypes = Config.Bind(
                ConfigSections.AILimiter,
                "Bot Types that Cannot be Disabled",
                BotTypeException.SniperScavs | BotTypeException.Rogues,
                "These bot types will never be disabled by the AI limiter"
            );
            MinBotsToEnableSleeping = Config.Bind(
                ConfigSections.AILimiter,
                "Min Bots to Enable AI Limiting",
                15,
                new ConfigDescription(
                    "AI will only be disabled if there are at least this number of bots on the map",
                    new AcceptableValueRange<int>(1, 30)
                )
            );
            SleepingMinDistanceToQuestingBots = Config.Bind(
                ConfigSections.AILimiter,
                "Distance from Bots That Are Questing (m)",
                75,
                new ConfigDescription(
                    "AI will only be disabled if it's more than this distance from other questing bots (typically PMC's and player Scavs)",
                    new AcceptableValueRange<int>(minDistanceAILimitQuesting, 1000)
                )
            );
            SleepingMinDistanceToHumansGlobal = Config.Bind(
                ConfigSections.AILimiter,
                "Distance from Human Players (m)",
                200,
                new ConfigDescription(
                    "AI will only be disabled if it's more than this distance from a human player. This takes priority over the map-specific advanced settings.",
                    new AcceptableValueRange<int>(minDistanceAILimitNormal, 1000)
                )
            );
            SleepingMinDistanceToHumansCustoms = Config.Bind(
                ConfigSections.AILimiter,
                "Distance from Human Players on Customs (m)",
                1000,
                new ConfigDescription(
                    "AI will only be disabled if it's more than this distance from a human player on Customs",
                    new AcceptableValueRange<int>(minDistanceAILimitNormal, 1000),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            SleepingMinDistanceToHumansFactory = Config.Bind(
                ConfigSections.AILimiter,
                "Distance from Human Players on Factory (m)",
                1000,
                new ConfigDescription(
                    "AI will only be disabled if it's more than this distance from a human player on Factory",
                    new AcceptableValueRange<int>(minDistanceAILimitNormal, 1000),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            SleepingMinDistanceToHumansInterchange = Config.Bind(
                ConfigSections.AILimiter,
                "Distance from Human Players on Interchange (m)",
                1000,
                new ConfigDescription(
                    "AI will only be disabled if it's more than this distance from a human player on Interchange",
                    new AcceptableValueRange<int>(minDistanceAILimitNormal, 1000),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            SleepingMinDistanceToHumansLabs = Config.Bind(
                ConfigSections.AILimiter,
                "Distance from Human Players on Labs (m)",
                1000,
                new ConfigDescription(
                    "AI will only be disabled if it's more than this distance from a human player on Labs",
                    new AcceptableValueRange<int>(minDistanceAILimitNormal, 1000),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            SleepingMinDistanceToHumansLighthouse = Config.Bind(
                ConfigSections.AILimiter,
                "Distance from Human Players on Lighthouse (m)",
                1000,
                new ConfigDescription(
                    "AI will only be disabled if it's more than this distance from a human player on Lighthouse",
                    new AcceptableValueRange<int>(minDistanceAILimitNormal, 1000),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            SleepingMinDistanceToHumansReserve = Config.Bind(
                ConfigSections.AILimiter,
                "Distance from Human Players on Reserve (m)",
                1000,
                new ConfigDescription(
                    "AI will only be disabled if it's more than this distance from a human player on Reserve",
                    new AcceptableValueRange<int>(minDistanceAILimitNormal, 1000),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            SleepingMinDistanceToHumansShoreline = Config.Bind(
                ConfigSections.AILimiter,
                "Distance from Human Players on Shoreline (m)",
                1000,
                new ConfigDescription(
                    "AI will only be disabled if it's more than this distance from a human player on Shoreline",
                    new AcceptableValueRange<int>(minDistanceAILimitNormal, 1000),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            SleepingMinDistanceToHumansStreets = Config.Bind(
                ConfigSections.AILimiter,
                "Distance from Human Players on Streets (m)",
                1000,
                new ConfigDescription(
                    "AI will only be disabled if it's more than this distance from a human player on Streets",
                    new AcceptableValueRange<int>(minDistanceAILimitNormal, 1000),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            SleepingMinDistanceToHumansWoods = Config.Bind(
                ConfigSections.AILimiter,
                "Distance from Human Players on Woods (m)",
                1000,
                new ConfigDescription(
                    "AI will only be disabled if it's more than this distance from a human player on Woods",
                    new AcceptableValueRange<int>(minDistanceAILimitNormal, 1000),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            SleepingMinDistanceToHumansGroundZero = Config.Bind(
                ConfigSections.AILimiter,
                "Distance from Human Players on GroundZero (m)",
                1000,
                new ConfigDescription(
                    "AI will only be disabled if it's more than this distance from a human player on GroundZero",
                    new AcceptableValueRange<int>(minDistanceAILimitNormal, 1000),
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );

            // ── 20. Debug ───────────────────────────────────────────
            ShowSpawnDebugMessages = Config.Bind(
                ConfigSections.Debug,
                "Show Debug Messages for Spawning",
                false,
                new ConfigDescription(
                    "Show additional debug messages to troubleshoot spawning issues",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            ShowBotInfoOverlays = Config.Bind(
                ConfigSections.Debug,
                "Show Bot Info Overlays",
                (QuestingBotType)0,
                "Show information about what each bot is doing"
            );
            ShowBotPathOverlays = Config.Bind(
                ConfigSections.Debug,
                "Show Bot Path Overlays",
                (QuestingBotType)0,
                new ConfigDescription(
                    "Create markers for Bot Path Overlay Types that bots of each selected type are following",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            ShowBotPathVisualizations = Config.Bind(
                ConfigSections.Debug,
                "Show Bot Path Visualizations",
                (QuestingBotType)0,
                new ConfigDescription(
                    "Draw the path that bots of each selected type are following",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            BotPathOverlayTypes = Config.Bind(
                ConfigSections.Debug,
                "Bot Path Overlay Types",
                BotPathOverlayType.QuestTarget,
                new ConfigDescription(
                    "The types of positions that will be shown for each bot that has path overlays enabled",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            ShowQuestInfoOverlays = Config.Bind(
                ConfigSections.Debug,
                "Show Quest Info Overlays",
                false,
                "Show information about every nearby quest objective location"
            );
            ShowQuestInfoForSpawnSearchQuests = Config.Bind(
                ConfigSections.Debug,
                "Show Quest Info for Spawn-Search Quests",
                false,
                new ConfigDescription(
                    "Include quest markers and information for spawn-search quests like 'Spawn Point Wander' and 'Boss Hunter' quests",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );
            QuestOverlayMaxDistance = Config.Bind(
                ConfigSections.Debug,
                "Max Distance (m) to Show Quest Info",
                100,
                new ConfigDescription(
                    "Quest markers and info overlays will only be shown if the objective location is within this distance from you",
                    new AcceptableValueRange<int>(10, 300)
                )
            );
            QuestOverlayFontSize = Config.Bind(
                ConfigSections.Debug,
                "Font Size for Quest Info",
                16,
                new ConfigDescription("Font Size for Quest Overlays", new AcceptableValueRange<int>(12, 36))
            );
            BotFilter = Config.Bind(
                ConfigSections.Debug,
                "Bot Filter",
                "",
                new ConfigDescription(
                    "Show debug info only for bots listed e.g 2,4",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true }
                )
            );

            // ── 21. Custom Quest Locations ──────────────────────────
            CreateQuestLocations = Config.Bind(
                ConfigSections.CustomQuestLocations,
                "Enable Quest Location Saving",
                false,
                new ConfigDescription(
                    "Allow custom quest locations to be saved",
                    null,
                    new ConfigurationManagerAttributes { Order = 4, IsAdvanced = true }
                )
            );
            ShowCurrentLocation = Config.Bind(
                ConfigSections.CustomQuestLocations,
                "Display Current Location",
                false,
                new ConfigDescription(
                    "Display your current (x,y,z) coordinates on the screen",
                    null,
                    new ConfigurationManagerAttributes { Order = 3, IsAdvanced = true }
                )
            );
            QuestLocationName = Config.Bind(
                ConfigSections.CustomQuestLocations,
                "Quest Location Name",
                "Custom Quest Location",
                new ConfigDescription(
                    "Name of the next quest location that will be stored",
                    null,
                    new ConfigurationManagerAttributes { Order = 2, IsAdvanced = true }
                )
            );
            StoreQuestLocationKey = Config.Bind(
                ConfigSections.CustomQuestLocations,
                "Store New Quest Location",
                new KeyboardShortcut(KeyCode.KeypadEnter),
                new ConfigDescription(
                    "Store your current location as a quest location",
                    null,
                    new ConfigurationManagerAttributes { Order = 1, IsAdvanced = true }
                )
            );
        }

        private static void indexMapIDs()
        {
            TarkovMapIDToEnum.Add("bigmap", TarkovMaps.Customs);
            TarkovMapIDToEnum.Add("factory4_day", TarkovMaps.Factory);
            TarkovMapIDToEnum.Add("factory4_night", TarkovMaps.Factory);
            TarkovMapIDToEnum.Add("Interchange", TarkovMaps.Interchange);
            TarkovMapIDToEnum.Add("laboratory", TarkovMaps.Labs);
            TarkovMapIDToEnum.Add("Lighthouse", TarkovMaps.Lighthouse);
            TarkovMapIDToEnum.Add("RezervBase", TarkovMaps.Reserve);
            TarkovMapIDToEnum.Add("Shoreline", TarkovMaps.Shoreline);
            TarkovMapIDToEnum.Add("TarkovStreets", TarkovMaps.Streets);
            TarkovMapIDToEnum.Add("Woods", TarkovMaps.Woods);
            TarkovMapIDToEnum.Add("Sandbox", TarkovMaps.GroundZero);
            TarkovMapIDToEnum.Add("Sandbox_high", TarkovMaps.GroundZero);
        }

        private static void indexWildSpawnTypeExceptions()
        {
            IEnumerable<BotBrainType> sniperScavBrains = Enumerable.Empty<BotBrainType>().AddSniperScavBrain();
            IEnumerable<BotBrainType> rogueBrains = Enumerable.Empty<BotBrainType>().AddRogueBrain();
            IEnumerable<BotBrainType> raiderBrains = Enumerable.Empty<BotBrainType>().AddRaiderBrain();

            IEnumerable<BotBrainType> bossAndFollowerBrains = Enumerable
                .Empty<BotBrainType>()
                .AddAllNormalBossAndFollowerBrains()
                .AddZryachiyAndFollowerBrains();

            addBrainsToExceptions(sniperScavBrains, BotTypeException.SniperScavs);
            addBrainsToExceptions(rogueBrains, BotTypeException.Rogues);
            addBrainsToExceptions(raiderBrains, BotTypeException.Raiders);
            addBrainsToExceptions(bossAndFollowerBrains, BotTypeException.BossesAndFollowers);
        }

        private static void addBrainsToExceptions(IEnumerable<BotBrainType> brainTypes, BotTypeException botTypeException)
        {
            foreach (BotBrainType brainType in brainTypes)
            {
                if (!ExceptionFlagForWildSpawnType.ContainsKey(brainType.SpawnType))
                {
                    ExceptionFlagForWildSpawnType.Add(brainType.SpawnType, botTypeException);
                }
                else
                {
                    ExceptionFlagForWildSpawnType[brainType.SpawnType] |= botTypeException;
                }
            }
        }
    }
}
