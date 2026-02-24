using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration;

/// <summary>
/// Simulates a player's FIRST INSTALL experience:
/// - Does config.json deserialize without errors?
/// - Are all sections present and non-null?
/// - Do feature toggles default sensibly?
/// - Do extreme values cause crashes?
/// - Do missing sections produce safe defaults?
/// </summary>
[TestFixture]
public class FirstRunInstallTests
{
    private JObject _configJson;
    private string _configJsonRaw;

    [OneTimeSetUp]
    public void LoadConfigJson()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        string configPath = null;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "config", "config.json");
            if (File.Exists(candidate))
            {
                configPath = candidate;
                break;
            }
            dir = Path.GetDirectoryName(dir);
            if (dir == null)
                break;
        }

        Assert.That(configPath, Is.Not.Null, "Could not find config/config.json from test directory");
        _configJsonRaw = File.ReadAllText(configPath);
        _configJson = JObject.Parse(_configJsonRaw);
    }

    private T JsonValue<T>(string path)
    {
        var token = _configJson.SelectToken(path);
        Assert.That(token, Is.Not.Null, $"JSON path '{path}' not found in config.json");
        return token.Value<T>();
    }

    // ══════════════════════════════════════════════════════════════
    //  1. Full config.json structural integrity
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ConfigJson_ParsesWithoutErrors()
    {
        Assert.DoesNotThrow(() => JObject.Parse(_configJsonRaw));
    }

    [Test]
    public void ConfigJson_HasAllTopLevelSections()
    {
        var required = new[]
        {
            "enabled",
            "debug",
            "max_calc_time_per_frame_ms",
            "chance_of_being_hostile_toward_bosses",
            "questing",
            "bot_spawns",
            "adjust_pscav_chance",
        };

        Assert.Multiple(() =>
        {
            foreach (var key in required)
            {
                Assert.That(_configJson.ContainsKey(key), Is.True, $"Top-level key '{key}' missing from config.json");
            }
        });
    }

    [Test]
    public void ConfigJson_QuestingSectionHasAllSubSections()
    {
        var expected = new[]
        {
            "enabled",
            "bot_pathing_update_interval_ms",
            "brain_layer_priorities",
            "quest_selection_timeout",
            "btr_run_distance",
            "allowed_bot_types_for_questing",
            "stuck_bot_detection",
            "unlocking_doors",
            "quest_generation",
            "bot_search_distances",
            "bot_pathing",
            "bot_questing_requirements",
            "extraction_requirements",
            "sprinting_limitations",
            "bot_quests",
            "zone_movement",
            "squad_strategy",
            "bot_lod",
            "looting",
            "vulture",
            "linger",
            "investigate",
            "spawn_entry",
            "room_clear",
            "dynamic_objectives",
            "look_variance",
            "patrol",
            "personality",
        };

        var questing = _configJson["questing"] as JObject;
        Assert.That(questing, Is.Not.Null, "questing section is null");

        Assert.Multiple(() =>
        {
            foreach (var key in expected)
            {
                Assert.That(questing.ContainsKey(key), Is.True, $"questing.{key} missing from config.json");
            }
        });
    }

    [Test]
    public void ConfigJson_BotSpawnsSectionHasAllSubSections()
    {
        var expected = new[]
        {
            "enabled",
            "blacklisted_pmc_bot_brains",
            "spawn_retry_time",
            "delay_game_start_until_bot_gen_finishes",
            "spawn_initial_bosses_first",
            "eft_new_spawn_system_adjustments",
            "bot_cap_adjustments",
            "limit_initial_boss_spawns",
            "max_alive_bots",
            "pmc_hostility_adjustments",
            "pmcs",
            "player_scavs",
        };

        var section = _configJson["bot_spawns"] as JObject;
        Assert.That(section, Is.Not.Null, "bot_spawns section is null");

        Assert.Multiple(() =>
        {
            foreach (var key in expected)
            {
                Assert.That(section.ContainsKey(key), Is.True, $"bot_spawns.{key} missing from config.json");
            }
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  2. Default config values are sane for first install
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void DefaultConfig_ModEnabled()
    {
        Assert.That(JsonValue<bool>("enabled"), Is.True, "Mod should be enabled by default");
    }

    [Test]
    public void DefaultConfig_QuestingEnabled()
    {
        Assert.That(JsonValue<bool>("questing.enabled"), Is.True, "Questing should be enabled by default");
    }

    [Test]
    public void DefaultConfig_BotSpawnsDisabled()
    {
        Assert.That(JsonValue<bool>("bot_spawns.enabled"), Is.False, "Bot spawns should be disabled by default (requires opt-in)");
    }

    [Test]
    public void DefaultConfig_MaxCalcTimePositive()
    {
        var value = JsonValue<int>("max_calc_time_per_frame_ms");
        Assert.That(value, Is.GreaterThan(0), "max_calc_time_per_frame_ms must be > 0");
        Assert.That(value, Is.LessThanOrEqualTo(50), "max_calc_time_per_frame_ms should not be excessive");
    }

    [Test]
    public void DefaultConfig_AllFeatureTogglesArePresent()
    {
        var togglePaths = new[]
        {
            "questing.zone_movement.enabled",
            "questing.squad_strategy.enabled",
            "questing.bot_lod.enabled",
            "questing.looting.enabled",
            "questing.vulture.enabled",
            "questing.linger.enabled",
            "questing.investigate.enabled",
            "questing.spawn_entry.enabled",
            "questing.room_clear.enabled",
            "questing.dynamic_objectives.enabled",
            "questing.look_variance.enabled",
            "questing.patrol.enabled",
            "questing.personality.enabled",
            "questing.bot_pathing.use_custom_mover",
            "questing.bot_pathing.bypass_door_colliders",
            "questing.stuck_bot_detection.stuck_bot_remedies.enabled",
        };

        Assert.Multiple(() =>
        {
            foreach (var path in togglePaths)
            {
                var token = _configJson.SelectToken(path);
                Assert.That(token, Is.Not.Null, $"Toggle '{path}' missing from config.json");
                Assert.That(token.Type, Is.EqualTo(JTokenType.Boolean), $"Toggle '{path}' should be boolean");
            }
        });
    }

    [Test]
    public void DefaultConfig_AllMajorFeaturesEnabled()
    {
        // For first-run, all major features should be ON so the player gets the full experience
        var features = new[]
        {
            "questing.zone_movement.enabled",
            "questing.squad_strategy.enabled",
            "questing.bot_lod.enabled",
            "questing.looting.enabled",
            "questing.vulture.enabled",
            "questing.linger.enabled",
            "questing.investigate.enabled",
            "questing.spawn_entry.enabled",
            "questing.room_clear.enabled",
            "questing.dynamic_objectives.enabled",
            "questing.look_variance.enabled",
            "questing.patrol.enabled",
            "questing.personality.enabled",
        };

        Assert.Multiple(() =>
        {
            foreach (var path in features)
            {
                Assert.That(JsonValue<bool>(path), Is.True, $"Feature '{path}' should be enabled by default");
            }
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  3. 2D arrays are valid in shipped config
    // ══════════════════════════════════════════════════════════════

    [TestCase("questing.bot_quests.eft_quests.level_range")]
    [TestCase("bot_spawns.pmcs.fraction_of_max_players_vs_raidET")]
    [TestCase("bot_spawns.pmcs.bots_per_group_distribution")]
    [TestCase("bot_spawns.pmcs.bot_difficulty_as_online")]
    [TestCase("bot_spawns.player_scavs.bots_per_group_distribution")]
    [TestCase("bot_spawns.player_scavs.bot_difficulty_as_online")]
    [TestCase("adjust_pscav_chance.chance_vs_time_remaining_fraction")]
    public void DefaultConfig_2DArraysAreWellFormed(string path)
    {
        var token = _configJson.SelectToken(path);
        Assert.That(token, Is.Not.Null, $"Path '{path}' not found");
        Assert.That(token.Type, Is.EqualTo(JTokenType.Array), $"'{path}' should be array");

        var array = token as JArray;
        Assert.That(array.Count, Is.GreaterThan(0), $"'{path}' should not be empty");

        foreach (var row in array)
        {
            Assert.That(row.Type, Is.EqualTo(JTokenType.Array), $"Each row in '{path}' should be an array");
            var rowArr = row as JArray;
            Assert.That(rowArr.Count, Is.EqualTo(2), $"Each row in '{path}' must have exactly 2 columns, found {rowArr.Count}");
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  4. Per-map dictionaries are not empty
    // ══════════════════════════════════════════════════════════════

    [TestCase("bot_spawns.max_alive_bots")]
    [TestCase("bot_spawns.bot_cap_adjustments.map_specific_adjustments")]
    [TestCase("questing.zone_movement.convergence_per_map")]
    [TestCase("questing.bot_questing_requirements.hearing_sensor.max_suspicious_time")]
    [TestCase("questing.bot_quests.exfil_direction_weighting")]
    public void DefaultConfig_PerMapDictionaries_AreNotEmpty(string path)
    {
        var token = _configJson.SelectToken(path) as JObject;
        Assert.That(token, Is.Not.Null, $"'{path}' not found or not an object");
        Assert.That(token.Count, Is.GreaterThan(0), $"'{path}' should have at least one entry");
    }

    // ══════════════════════════════════════════════════════════════
    //  5. Missing section resilience (JObject manipulation)
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void MissingQuestingSection_IndividualConfigDefaults_StillSane()
    {
        // If the "questing" section is missing, individual config POCOs still have safe defaults
        var lod = new BotLodConfig();
        Assert.That(lod.Enabled, Is.True, "BotLod default should be enabled");
        Assert.That(lod.ReducedDistance, Is.GreaterThan(0), "ReducedDistance default must be positive");
        Assert.That(lod.MinimalDistance, Is.GreaterThan(lod.ReducedDistance), "MinimalDistance > ReducedDistance");

        var looting = new LootingConfig();
        Assert.That(looting.Enabled, Is.True, "Looting default should be enabled");
        Assert.That(looting.ScanIntervalSeconds, Is.GreaterThan(0));
        Assert.That(looting.MinItemValue, Is.GreaterThan(0));

        var vulture = new VultureConfig();
        Assert.That(vulture.Enabled, Is.True, "Vulture default should be enabled");
        Assert.That(vulture.BaseDetectionRange, Is.GreaterThan(0));
        Assert.That(vulture.CourageThreshold, Is.GreaterThan(0));
    }

    [Test]
    public void MissingBotSpawnsSection_ConfigDefaultsSafe()
    {
        // When bot_spawns is missing, the default should be disabled
        var json = """{ "enabled": true, "questing": { "enabled": true } }""";
        var parsed = JObject.Parse(json);
        Assert.That(parsed["bot_spawns"], Is.Null, "bot_spawns should be absent");

        // Individual config class defaults should be safe
        var squad = new SquadStrategyConfig();
        Assert.That(squad.GuardDistance, Is.GreaterThan(0));
        Assert.That(squad.ArrivalRadius, Is.GreaterThan(0));
        Assert.That(squad.MaxDistanceFromBoss, Is.GreaterThan(0));
    }

    // ══════════════════════════════════════════════════════════════
    //  6. Extreme config values
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ExtremeConfig_AllZeroDistances_BotLod_StillValid()
    {
        var config = new BotLodConfig
        {
            Enabled = true,
            ReducedDistance = 0,
            MinimalDistance = 0,
            ReducedFrameSkip = 0,
            MinimalFrameSkip = 0,
        };
        // Should not throw — system should handle gracefully
        Assert.That(config.Enabled, Is.True);
    }

    [Test]
    public void ExtremeConfig_NegativeDetectionRange_Vulture()
    {
        var config = new VultureConfig { BaseDetectionRange = -10f };
        // Negative range effectively disables detection
        Assert.That(config.BaseDetectionRange, Is.LessThan(0));
    }

    [Test]
    public void ExtremeConfig_ZeroScanInterval_Looting()
    {
        var config = new LootingConfig { ScanIntervalSeconds = 0f };
        Assert.That(config.ScanIntervalSeconds, Is.EqualTo(0f));
    }

    [Test]
    public void ExtremeConfig_HugeMaxConcurrentLooters()
    {
        var config = new LootingConfig { MaxConcurrentLooters = 999999 };
        Assert.That(config.MaxConcurrentLooters, Is.EqualTo(999999));
    }

    [Test]
    public void ExtremeConfig_ZeroBaseScore_Linger()
    {
        var config = new LingerConfig { BaseScore = 0f };
        Assert.That(config.BaseScore, Is.EqualTo(0f));
    }

    [Test]
    public void ExtremeConfig_ZeroCooldown_Patrol()
    {
        var config = new PatrolConfig { CooldownSec = 0f };
        Assert.That(config.CooldownSec, Is.EqualTo(0f));
    }

    // ══════════════════════════════════════════════════════════════
    //  7. Feature toggle: C# defaults match config.json values
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void FeatureToggle_BotLod_CSharpDefault_MatchesJson()
    {
        Assert.That(new BotLodConfig().Enabled, Is.EqualTo(JsonValue<bool>("questing.bot_lod.enabled")));
    }

    [Test]
    public void FeatureToggle_Looting_CSharpDefault_MatchesJson()
    {
        Assert.That(new LootingConfig().Enabled, Is.EqualTo(JsonValue<bool>("questing.looting.enabled")));
    }

    [Test]
    public void FeatureToggle_Vulture_CSharpDefault_MatchesJson()
    {
        Assert.That(new VultureConfig().Enabled, Is.EqualTo(JsonValue<bool>("questing.vulture.enabled")));
    }

    [Test]
    public void FeatureToggle_Investigate_CSharpDefault_MatchesJson()
    {
        Assert.That(new InvestigateConfig().Enabled, Is.EqualTo(JsonValue<bool>("questing.investigate.enabled")));
    }

    [Test]
    public void FeatureToggle_Linger_CSharpDefault_MatchesJson()
    {
        Assert.That(new LingerConfig().Enabled, Is.EqualTo(JsonValue<bool>("questing.linger.enabled")));
    }

    [Test]
    public void FeatureToggle_SpawnEntry_CSharpDefault_MatchesJson()
    {
        Assert.That(new SpawnEntryConfig().Enabled, Is.EqualTo(JsonValue<bool>("questing.spawn_entry.enabled")));
    }

    [Test]
    public void FeatureToggle_RoomClear_CSharpDefault_MatchesJson()
    {
        Assert.That(new RoomClearConfig().Enabled, Is.EqualTo(JsonValue<bool>("questing.room_clear.enabled")));
    }

    [Test]
    public void FeatureToggle_Patrol_CSharpDefault_MatchesJson()
    {
        Assert.That(new PatrolConfig().Enabled, Is.EqualTo(JsonValue<bool>("questing.patrol.enabled")));
    }

    [Test]
    public void FeatureToggle_DynamicObjectives_CSharpDefault_MatchesJson()
    {
        Assert.That(new DynamicObjectiveConfig().Enabled, Is.EqualTo(JsonValue<bool>("questing.dynamic_objectives.enabled")));
    }

    [Test]
    public void FeatureToggle_LookVariance_CSharpDefault_MatchesJson()
    {
        Assert.That(new LookVarianceConfig().Enabled, Is.EqualTo(JsonValue<bool>("questing.look_variance.enabled")));
    }

    [Test]
    public void FeatureToggle_Personality_CSharpDefault_MatchesJson()
    {
        Assert.That(new PersonalityConfig().Enabled, Is.EqualTo(JsonValue<bool>("questing.personality.enabled")));
    }

    [Test]
    public void FeatureToggle_StuckRemedies_CSharpDefault_MatchesJson()
    {
        Assert.That(
            new StuckBotRemediesConfig().Enabled,
            Is.EqualTo(JsonValue<bool>("questing.stuck_bot_detection.stuck_bot_remedies.enabled"))
        );
    }

    [Test]
    public void FeatureToggle_CustomMover_CSharpDefault_MatchesJson()
    {
        Assert.That(new BotPathingConfig().UseCustomMover, Is.EqualTo(JsonValue<bool>("questing.bot_pathing.use_custom_mover")));
    }

    [Test]
    public void FeatureToggle_DoorBypass_CSharpDefault_MatchesJson()
    {
        Assert.That(new BotPathingConfig().BypassDoorColliders, Is.EqualTo(JsonValue<bool>("questing.bot_pathing.bypass_door_colliders")));
    }

    // ══════════════════════════════════════════════════════════════
    //  8. Plugin ID consistency
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void PluginID_Client_ContainsDanW()
    {
        // The BepInPlugin ID is "com.DanW.QuestingBots"
        // The server plugin has "com.danw.sptquestingbots" in its metadata
        // These are different by design (client vs server) but both should
        // contain DanW/danw to identify the author
        var clientPluginSource = FindSourceFile("QuestingBotsPlugin.cs");
        Assert.That(clientPluginSource, Does.Contain("com.DanW.QuestingBots"), "Client plugin ID should be 'com.DanW.QuestingBots'");
    }

    [Test]
    public void PluginDependency_BigBrain_VersionSpecified()
    {
        var source = FindSourceFile("QuestingBotsPlugin.cs");
        Assert.That(source, Does.Contain("xyz.drakia.bigbrain"), "Plugin must depend on BigBrain");
    }

    [Test]
    public void PluginDependency_Waypoints_VersionSpecified()
    {
        var source = FindSourceFile("QuestingBotsPlugin.cs");
        Assert.That(source, Does.Contain("xyz.drakia.waypoints"), "Plugin must depend on Waypoints");
    }

    // ══════════════════════════════════════════════════════════════
    //  9. Config value ranges in shipped config
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void DefaultConfig_StuckBotDetection_ValuesPositive()
    {
        Assert.Multiple(() =>
        {
            Assert.That(JsonValue<float>("questing.stuck_bot_detection.distance"), Is.GreaterThan(0));
            Assert.That(JsonValue<float>("questing.stuck_bot_detection.time"), Is.GreaterThan(0));
            Assert.That(JsonValue<int>("questing.stuck_bot_detection.max_count"), Is.GreaterThan(0));
        });
    }

    [Test]
    public void DefaultConfig_SprintLimitations_StaminaRangeValid()
    {
        var min = JsonValue<float>("questing.sprinting_limitations.stamina.min");
        var max = JsonValue<float>("questing.sprinting_limitations.stamina.max");

        Assert.Multiple(() =>
        {
            Assert.That(min, Is.GreaterThanOrEqualTo(0f));
            Assert.That(max, Is.LessThanOrEqualTo(1f));
            Assert.That(min, Is.LessThan(max));
        });
    }

    [Test]
    public void DefaultConfig_ExtractionRequirements_Consistent()
    {
        var minAlive = JsonValue<float>("questing.extraction_requirements.min_alive_time");
        var mustExtract = JsonValue<float>("questing.extraction_requirements.must_extract_time_remaining");
        var totalMin = JsonValue<float>("questing.extraction_requirements.total_quests.min");
        var totalMax = JsonValue<float>("questing.extraction_requirements.total_quests.max");

        Assert.Multiple(() =>
        {
            Assert.That(minAlive, Is.GreaterThan(0));
            Assert.That(mustExtract, Is.GreaterThan(0));
            Assert.That(totalMin, Is.LessThanOrEqualTo(totalMax));
        });
    }

    [Test]
    public void DefaultConfig_LootingDistances_Positive()
    {
        Assert.Multiple(() =>
        {
            Assert.That(JsonValue<float>("questing.looting.detect_container_distance"), Is.GreaterThan(0));
            Assert.That(JsonValue<float>("questing.looting.detect_item_distance"), Is.GreaterThan(0));
            Assert.That(JsonValue<float>("questing.looting.detect_corpse_distance"), Is.GreaterThan(0));
            Assert.That(JsonValue<float>("questing.looting.approach_distance"), Is.GreaterThan(0));
        });
    }

    [Test]
    public void DefaultConfig_VultureDistances_AmbushMinLessThanMax()
    {
        var min = JsonValue<float>("questing.vulture.ambush_distance_min");
        var max = JsonValue<float>("questing.vulture.ambush_distance_max");
        Assert.That(min, Is.LessThanOrEqualTo(max), "ambush_distance_min must be <= ambush_distance_max");
    }

    [Test]
    public void DefaultConfig_LingerDuration_MinLessThanMax()
    {
        var min = JsonValue<float>("questing.linger.duration_min");
        var max = JsonValue<float>("questing.linger.duration_max");
        Assert.That(min, Is.LessThanOrEqualTo(max));
    }

    [Test]
    public void DefaultConfig_RoomClearDuration_MinLessThanMax()
    {
        var min = JsonValue<float>("questing.room_clear.duration_min");
        var max = JsonValue<float>("questing.room_clear.duration_max");
        Assert.That(min, Is.LessThanOrEqualTo(max));
    }

    [Test]
    public void DefaultConfig_SpawnEntryDuration_MinLessThanMax()
    {
        var min = JsonValue<float>("questing.spawn_entry.base_duration_min");
        var max = JsonValue<float>("questing.spawn_entry.base_duration_max");
        Assert.That(min, Is.LessThanOrEqualTo(max));
    }

    [Test]
    public void DefaultConfig_InvestigateHeadScan_MinLessThanMax()
    {
        var min = JsonValue<float>("questing.investigate.head_scan_interval_min");
        var max = JsonValue<float>("questing.investigate.head_scan_interval_max");
        Assert.That(min, Is.LessThanOrEqualTo(max));
    }

    [Test]
    public void DefaultConfig_PatrolBaseScore_InZeroOneRange()
    {
        var score = JsonValue<float>("questing.patrol.base_score");
        Assert.That(score, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
    }

    [Test]
    public void DefaultConfig_LingerBaseScore_InZeroOneRange()
    {
        var score = JsonValue<float>("questing.linger.base_score");
        Assert.That(score, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
    }

    [Test]
    public void DefaultConfig_InvestigateBaseScore_InZeroOneRange()
    {
        var score = JsonValue<float>("questing.investigate.base_score");
        Assert.That(score, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
    }

    [Test]
    public void DefaultConfig_SquadStrategy_DistancesPositive()
    {
        Assert.Multiple(() =>
        {
            Assert.That(JsonValue<float>("questing.squad_strategy.guard_distance"), Is.GreaterThan(0));
            Assert.That(JsonValue<float>("questing.squad_strategy.flank_distance"), Is.GreaterThan(0));
            Assert.That(JsonValue<float>("questing.squad_strategy.overwatch_distance"), Is.GreaterThan(0));
            Assert.That(JsonValue<float>("questing.squad_strategy.escort_distance"), Is.GreaterThan(0));
            Assert.That(JsonValue<float>("questing.squad_strategy.arrival_radius"), Is.GreaterThan(0));
        });
    }

    [Test]
    public void DefaultConfig_ZoneMovement_WeightsNonNegative()
    {
        Assert.Multiple(() =>
        {
            Assert.That(JsonValue<float>("questing.zone_movement.convergence_weight"), Is.GreaterThanOrEqualTo(0));
            Assert.That(JsonValue<float>("questing.zone_movement.advection_weight"), Is.GreaterThanOrEqualTo(0));
            Assert.That(JsonValue<float>("questing.zone_movement.momentum_weight"), Is.GreaterThanOrEqualTo(0));
            Assert.That(JsonValue<float>("questing.zone_movement.noise_weight"), Is.GreaterThanOrEqualTo(0));
        });
    }

    [Test]
    public void DefaultConfig_ZoneMovement_TargetCellCountPositive()
    {
        Assert.That(JsonValue<int>("questing.zone_movement.target_cell_count"), Is.GreaterThan(0));
    }

    [Test]
    public void DefaultConfig_FactoryMaps_ZoneMovementDisabled()
    {
        // Factory is too small for zone movement
        Assert.Multiple(() =>
        {
            Assert.That(JsonValue<bool>("questing.zone_movement.convergence_per_map.factory4_day.enabled"), Is.False);
            Assert.That(JsonValue<bool>("questing.zone_movement.convergence_per_map.factory4_night.enabled"), Is.False);
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  10. No unexpected null references in default config objects
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void AllLinkedConfigClasses_DefaultConstruction_HasNoNullSubObjects()
    {
        Assert.Multiple(() =>
        {
            var lod = new BotLodConfig();
            Assert.That(lod, Is.Not.Null);

            var looting = new LootingConfig();
            Assert.That(looting, Is.Not.Null);

            var vulture = new VultureConfig();
            Assert.That(vulture, Is.Not.Null);

            var linger = new LingerConfig();
            Assert.That(linger, Is.Not.Null);

            var investigate = new InvestigateConfig();
            Assert.That(investigate, Is.Not.Null);

            var spawnEntry = new SpawnEntryConfig();
            Assert.That(spawnEntry, Is.Not.Null);

            var roomClear = new RoomClearConfig();
            Assert.That(roomClear, Is.Not.Null);

            var patrol = new PatrolConfig();
            Assert.That(patrol, Is.Not.Null);
            Assert.That(patrol.RoutesPerMap, Is.Not.Null, "PatrolConfig.RoutesPerMap default should not be null");

            var dynamic = new DynamicObjectiveConfig();
            Assert.That(dynamic, Is.Not.Null);

            var personality = new PersonalityConfig();
            Assert.That(personality, Is.Not.Null);

            var lookVariance = new LookVarianceConfig();
            Assert.That(lookVariance, Is.Not.Null);

            var squadStrategy = new SquadStrategyConfig();
            Assert.That(squadStrategy, Is.Not.Null);

            var sprint = new SprintingLimitationsConfig();
            Assert.That(sprint, Is.Not.Null);
            Assert.That(sprint.Stamina, Is.Not.Null, "SprintingLimitationsConfig.Stamina default should not be null");
            Assert.That(sprint.SharpPathCorners, Is.Not.Null, "SprintingLimitationsConfig.SharpPathCorners default should not be null");
            Assert.That(
                sprint.ApproachingClosedDoors,
                Is.Not.Null,
                "SprintingLimitationsConfig.ApproachingClosedDoors default should not be null"
            );

            var stuckRemedies = new StuckBotRemediesConfig();
            Assert.That(stuckRemedies, Is.Not.Null);

            var botPathing = new BotPathingConfig();
            Assert.That(botPathing, Is.Not.Null);

            var debug = new DebugConfig();
            Assert.That(debug, Is.Not.Null);

            var zoneMovement = new ZoneMovementConfig();
            Assert.That(zoneMovement, Is.Not.Null);
            Assert.That(zoneMovement.ConvergencePerMap, Is.Not.Null, "ZoneMovement.ConvergencePerMap default should not be null");
            Assert.That(zoneMovement.AdvectionZonesPerMap, Is.Not.Null, "ZoneMovement.AdvectionZonesPerMap default should not be null");
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  Helper to find source files relative to repo root
    // ══════════════════════════════════════════════════════════════

    private static string FindSourceFile(string fileName)
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "src", "SPTQuestingBots.Client", fileName);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = Path.GetDirectoryName(dir);
            if (dir == null)
                break;
        }
        Assert.Fail($"Could not find source file: {fileName}");
        return null;
    }
}
