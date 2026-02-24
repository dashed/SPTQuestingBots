using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration;

/// <summary>
/// Verifies that C# config class defaults match the shipped config.json values.
/// Any mismatch means that if config.json fails to load, the mod silently runs
/// with wrong parameters.
/// </summary>
[TestFixture]
public class ConfigConsistencyTests
{
    private JObject _configJson;

    [OneTimeSetUp]
    public void LoadConfigJson()
    {
        // Walk up from bin/Debug/net9.0 to repo root
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
        var json = File.ReadAllText(configPath);
        _configJson = JObject.Parse(json);
    }

    // ──────────────────────────────────────────────────────────────
    //  Helper: navigate JSON path and assert value
    // ──────────────────────────────────────────────────────────────

    private T JsonValue<T>(string path)
    {
        var token = _configJson.SelectToken(path);
        Assert.That(token, Is.Not.Null, $"JSON path '{path}' not found in config.json");
        return token.Value<T>();
    }

    private void AssertJsonHasPath(string path)
    {
        Assert.That(_configJson.SelectToken(path), Is.Not.Null, $"Expected JSON path '{path}' to exist in config.json");
    }

    private void AssertJsonMissingPath(string path)
    {
        Assert.That(_configJson.SelectToken(path), Is.Null, $"Expected JSON path '{path}' to NOT exist in config.json");
    }

    // ══════════════════════════════════════════════════════════════
    //  BUG: C# default != config.json value
    //  These tests document every known mismatch.
    //  Each test name encodes: Class_Property_CSharpDefault_vs_JsonValue
    // ══════════════════════════════════════════════════════════════

    // ── DebugConfig ──────────────────────────────────────────────

    [Test]
    public void DebugConfig_AllowZeroDistanceSleeping_MatchesJson()
    {
        var csharpDefault = new DebugConfig().AllowZeroDistanceSleeping;
        var jsonValue = JsonValue<bool>("debug.allow_zero_distance_sleeping");

        Assert.That(csharpDefault, Is.True, "C# default");
        Assert.That(jsonValue, Is.True, "config.json value");
        Assert.That(csharpDefault, Is.EqualTo(jsonValue), "C# default should match config.json");
    }

    // ── QuestingConfig (via JSON paths since class is not linked) ──

    [Test]
    public void QuestingConfig_QuestSelectionTimeout_Default2000_Json250()
    {
        var jsonValue = JsonValue<float>("questing.quest_selection_timeout");
        Assert.That(jsonValue, Is.EqualTo(250f), "config.json value");
        // C# default is 2000 — 8x too high
    }

    [Test]
    public void QuestingConfig_BTRRunDistance_Default40_Json10()
    {
        var jsonValue = JsonValue<float>("questing.btr_run_distance");
        Assert.That(jsonValue, Is.EqualTo(10f), "config.json value");
        // C# default is 40 — 4x too high
    }

    [Test]
    public void QuestingConfig_DefaultWaitTimeAfterObjectiveCompletion_Default10_Json3()
    {
        var jsonValue = JsonValue<float>("questing.default_wait_time_after_objective_completion");
        Assert.That(jsonValue, Is.EqualTo(3f), "config.json value");
        // C# default is 10 — 3.3x too high
    }

    // ── StuckBotDetectionConfig ──────────────────────────────────

    [Test]
    public void StuckBotDetection_MaxCount_Default10_Json8()
    {
        var jsonValue = JsonValue<int>("questing.stuck_bot_detection.max_count");
        Assert.That(jsonValue, Is.EqualTo(8), "config.json value");
        // C# default is 10
    }

    // ── StuckBotRemediesConfig ───────────────────────────────────

    [Test]
    public void StuckBotRemedies_MinTimeBeforeJumping_Default3_Json6()
    {
        var jsonValue = JsonValue<float>("questing.stuck_bot_detection.stuck_bot_remedies.min_time_before_jumping");
        Assert.That(jsonValue, Is.EqualTo(6f), "config.json value");
        // C# default is 3 — half the intended value
    }

    [Test]
    public void StuckBotRemedies_JumpDebounceTime_Default2_Json4()
    {
        var jsonValue = JsonValue<float>("questing.stuck_bot_detection.stuck_bot_remedies.jump_debounce_time");
        Assert.That(jsonValue, Is.EqualTo(4f), "config.json value");
        // C# default is 2 — half the intended value
    }

    [Test]
    public void StuckBotRemedies_MinTimeBeforeVaulting_Default1_5_Json8()
    {
        var jsonValue = JsonValue<float>("questing.stuck_bot_detection.stuck_bot_remedies.min_time_before_vaulting");
        Assert.That(jsonValue, Is.EqualTo(8f), "config.json value");
        // C# default is 1.5 — 5.3x too low
    }

    [Test]
    public void StuckBotRemedies_VaultDebounceTime_Default2_Json4()
    {
        var jsonValue = JsonValue<float>("questing.stuck_bot_detection.stuck_bot_remedies.vault_debounce_time");
        Assert.That(jsonValue, Is.EqualTo(4f), "config.json value");
        // C# default is 2 — half the intended value
    }

    // ── UnlockingDoorsConfig ─────────────────────────────────────

    [Test]
    public void UnlockingDoors_DoorApproachPositionSearchOffset_DefaultNeg0_5_JsonNeg0_75()
    {
        var jsonValue = JsonValue<float>("questing.unlocking_doors.door_approach_position_search_offset");
        Assert.That(jsonValue, Is.EqualTo(-0.75f), "config.json value");
        // C# default is -0.5 — offset too small
    }

    // ── QuestGenerationConfig ────────────────────────────────────

    [Test]
    public void QuestGeneration_NavMeshSearchDistanceItem_Default2_Json1_5()
    {
        var jsonValue = JsonValue<float>("questing.quest_generation.navmesh_search_distance_item");
        Assert.That(jsonValue, Is.EqualTo(1.5f), "config.json value");
        // C# default is 2
    }

    [Test]
    public void QuestGeneration_NavMeshSearchDistanceZone_Default2_Json1_5()
    {
        var jsonValue = JsonValue<float>("questing.quest_generation.navmesh_search_distance_zone");
        Assert.That(jsonValue, Is.EqualTo(1.5f), "config.json value");
        // C# default is 2
    }

    [Test]
    public void QuestGeneration_NavMeshSearchDistanceDoors_Default1_5_Json0_75()
    {
        var jsonValue = JsonValue<float>("questing.quest_generation.navmesh_search_distance_doors");
        Assert.That(jsonValue, Is.EqualTo(0.75f), "config.json value");
        // C# default is 1.5 — 2x too high
    }

    // ── BotSearchDistanceConfig ──────────────────────────────────

    [Test]
    public void BotSearchDistances_ObjectiveReachedIdeal_Default3_Json0_5()
    {
        var jsonValue = JsonValue<float>("questing.bot_search_distances.objective_reached_ideal");
        Assert.That(jsonValue, Is.EqualTo(0.5f), "config.json value");
        // C# default is 3 — 6x too high! Bots would consider objectives "reached"
        // from much too far away.
    }

    [Test]
    public void BotSearchDistances_ObjectiveReachedNavMeshPathError_Default20_Json2()
    {
        var jsonValue = JsonValue<float>("questing.bot_search_distances.objective_reached_navmesh_path_error");
        Assert.That(jsonValue, Is.EqualTo(2f), "config.json value");
        // C# default is 20 — 10x too high! Would accept massive path errors.
    }

    // ── BotPathingConfig ─────────────────────────────────────────

    [Test]
    public void BotPathing_UseCustomMover_NotInJson()
    {
        AssertJsonMissingPath("questing.bot_pathing.use_custom_mover");
        // C# has this property (default true) but JSON doesn't expose it
        var config = new BotPathingConfig();
        Assert.That(config.UseCustomMover, Is.True, "C# default used since missing from JSON");
    }

    [Test]
    public void BotPathing_BypassDoorColliders_NotInJson()
    {
        AssertJsonMissingPath("questing.bot_pathing.bypass_door_colliders");
        var config = new BotPathingConfig();
        Assert.That(config.BypassDoorColliders, Is.True, "C# default used since missing from JSON");
    }

    // ── BotQuestingRequirementsConfig ────────────────────────────

    [Test]
    public void BotQuestingRequirements_ExcludeBotsByLevel_DefaultFalse_JsonTrue()
    {
        var jsonValue = JsonValue<bool>("questing.bot_questing_requirements.exclude_bots_by_level");
        Assert.That(jsonValue, Is.True, "config.json value");
        // C# default is false — level filtering disabled by default
    }

    [Test]
    public void BotQuestingRequirements_RepeatQuestDelay_Default300_Json360()
    {
        var jsonValue = JsonValue<float>("questing.bot_questing_requirements.repeat_quest_delay");
        Assert.That(jsonValue, Is.EqualTo(360f), "config.json value");
        // C# default is 300 — 60s shorter
    }

    [Test]
    public void BotQuestingRequirements_MinHydration_Default50_Json20()
    {
        var jsonValue = JsonValue<float>("questing.bot_questing_requirements.min_hydration");
        Assert.That(jsonValue, Is.EqualTo(20f), "config.json value");
        // C# default is 50 — bots refuse quests at much higher hydration
    }

    [Test]
    public void BotQuestingRequirements_MinEnergy_Default50_Json20()
    {
        var jsonValue = JsonValue<float>("questing.bot_questing_requirements.min_energy");
        Assert.That(jsonValue, Is.EqualTo(20f), "config.json value");
        // C# default is 50 — bots refuse quests at much higher energy
    }

    // ── HearingSensorConfig ──────────────────────────────────────

    [Test]
    public void HearingSensor_Enabled_DefaultFalse_JsonTrue()
    {
        var jsonValue = JsonValue<bool>("questing.bot_questing_requirements.hearing_sensor.enabled");
        Assert.That(jsonValue, Is.True, "config.json value");
        // C# default is false — hearing sensor completely disabled
    }

    [Test]
    public void HearingSensor_MaxDistanceGunfire_Default75_Json50()
    {
        var jsonValue = JsonValue<float>("questing.bot_questing_requirements.hearing_sensor.max_distance_gunfire");
        Assert.That(jsonValue, Is.EqualTo(50f), "config.json value");
        // C# default is 75 — 50% too high
    }

    [Test]
    public void HearingSensor_MaxDistanceGunfireSuppressed_Default75_Json50()
    {
        var jsonValue = JsonValue<float>("questing.bot_questing_requirements.hearing_sensor.max_distance_gunfire_suppressed");
        Assert.That(jsonValue, Is.EqualTo(50f), "config.json value");
        // C# default is 75 — suppressed weapons heard same distance as unsuppressed
    }

    [Test]
    public void HearingSensor_SuspicionCooldownTime_Default30_Json7()
    {
        var jsonValue = JsonValue<float>("questing.bot_questing_requirements.hearing_sensor.suspicion_cooldown_time");
        Assert.That(jsonValue, Is.EqualTo(7f), "config.json value");
        // C# default is 30 — 4.3x too high, bots stay suspicious way too long
    }

    // ── ExtractionRequirementsConfig ─────────────────────────────

    [Test]
    public void ExtractionRequirements_UseSAINForExtracting_DefaultFalse_JsonTrue()
    {
        var jsonValue = JsonValue<bool>("questing.extraction_requirements.use_sain_for_extracting");
        Assert.That(jsonValue, Is.True, "config.json value");
        // C# default is false — SAIN extraction integration disabled
    }

    // ── SprintingLimitationsConfig ───────────────────────────────

    [Test]
    public void SprintingLimitations_EnableDebounceTime_MatchesJson()
    {
        var config = new SprintingLimitationsConfig();
        Assert.That(config.EnableDebounceTime, Is.EqualTo(3f), "C# default");

        var jsonValue = JsonValue<float>("questing.sprinting_limitations.enable_debounce_time");
        Assert.That(jsonValue, Is.EqualTo(3f), "config.json value");
        Assert.That(config.EnableDebounceTime, Is.EqualTo(jsonValue), "C# default should match config.json");
    }

    // ── BreakForLootingConfig ────────────────────────────────────

    [Test]
    public void BreakForLooting_MaxDistanceFromBoss_Default75_Json50()
    {
        var jsonValue = JsonValue<float>("questing.bot_questing_requirements.break_for_looting.max_distance_from_boss");
        Assert.That(jsonValue, Is.EqualTo(50f), "config.json value");
        // C# default is 75 — followers allowed to loot much further from boss
    }

    // ── MaxFollowerDistanceConfig ────────────────────────────────

    [Test]
    public void MaxFollowerDistance_TargetPositionVariationAllowed_Default1_Json3()
    {
        var jsonValue = JsonValue<float>("questing.bot_questing_requirements.max_follower_distance.target_position_variation_allowed");
        Assert.That(jsonValue, Is.EqualTo(3f), "config.json value");
        // C# default is 1 — followers would re-path too aggressively
    }

    [Test]
    public void MaxFollowerDistance_Nearest_Default35_Json15()
    {
        var jsonValue = JsonValue<float>("questing.bot_questing_requirements.max_follower_distance.nearest");
        Assert.That(jsonValue, Is.EqualTo(15f), "config.json value");
        // C# default is 35 — 2.3x too far, followers too spread out
    }

    [Test]
    public void MaxFollowerDistance_Furthest_Default50_Json25()
    {
        var jsonValue = JsonValue<float>("questing.bot_questing_requirements.max_follower_distance.furthest");
        Assert.That(jsonValue, Is.EqualTo(25f), "config.json value");
        // C# default is 50 — 2x too far
    }

    // ── BotQuestsConfig ──────────────────────────────────────────

    [Test]
    public void BotQuests_DistanceRandomness_Default50_Json30()
    {
        var jsonValue = JsonValue<int>("questing.bot_quests.distance_randomness");
        Assert.That(jsonValue, Is.EqualTo(30), "config.json value");
        // C# default is 50 — 67% too much randomness
    }

    [Test]
    public void BotQuests_DesirabilityRandomness_Default50_Json20()
    {
        var jsonValue = JsonValue<int>("questing.bot_quests.desirability_randomness");
        Assert.That(jsonValue, Is.EqualTo(20), "config.json value");
        // C# default is 50 — 2.5x too much randomness
    }

    [Test]
    public void BotQuests_DesirabilityActiveQuestMultiplier_Default1_Json1_2()
    {
        var jsonValue = JsonValue<float>("questing.bot_quests.desirability_active_quest_multiplier");
        Assert.That(jsonValue, Is.EqualTo(1.2f), "config.json value");
        // C# default is 1.0 — no bonus for active quests
    }

    [Test]
    public void BotQuests_AirdropBotInterestTime_Default1800_Json420()
    {
        var jsonValue = JsonValue<float>("questing.bot_quests.airdrop_bot_interest_time");
        Assert.That(jsonValue, Is.EqualTo(420f), "config.json value");
        // C# default is 1800 (30 min) — 4.3x too long, bots chase airdrops forever
    }

    [Test]
    public void BotQuests_LightkeeperIslandQuests_Enabled_DefaultFalse_JsonTrue()
    {
        var jsonValue = JsonValue<bool>("questing.bot_quests.lightkeeper_island_quests.enabled");
        Assert.That(jsonValue, Is.True, "config.json value");
        // C# default is false — lightkeeper quests disabled
    }

    // ── BotSpawnsConfig ──────────────────────────────────────────

    [Test]
    public void BotSpawns_DelayGameStartUntilBotGenFinishes_DefaultFalse_JsonTrue()
    {
        var jsonValue = JsonValue<bool>("bot_spawns.delay_game_start_until_bot_gen_finishes");
        Assert.That(jsonValue, Is.True, "config.json value");
        // C# default is false — game can start before bots are generated
    }

    [Test]
    public void BotSpawns_SpawnInitialBossesFirst_DefaultTrue_JsonFalse()
    {
        var jsonValue = JsonValue<bool>("bot_spawns.spawn_initial_bosses_first");
        Assert.That(jsonValue, Is.False, "config.json value");
        // C# default is true — would spawn bosses first despite JSON intent
    }

    // ── EftNewSpawnSystemAdjustmentsConfig ───────────────────────

    [Test]
    public void EftNewSpawnSystem_NonWaveRetryDelayAfterBlocked_Default20_Json180()
    {
        var jsonValue = JsonValue<float>("bot_spawns.eft_new_spawn_system_adjustments.non_wave_retry_delay_after_blocked");
        Assert.That(jsonValue, Is.EqualTo(180f), "config.json value");
        // C# default is 20 — 9x too low, spawns retry way too aggressively
    }

    // ── LimitInitialBossSpawnsConfig ─────────────────────────────

    [Test]
    public void LimitInitialBossSpawns_MaxInitialBosses_Default10_Json14()
    {
        var jsonValue = JsonValue<int>("bot_spawns.limit_initial_boss_spawns.max_initial_bosses");
        Assert.That(jsonValue, Is.EqualTo(14), "config.json value");
        // C# default is 10 — 4 fewer bosses allowed
    }

    [Test]
    public void LimitInitialBossSpawns_MaxInitialRogues_Default6_Json10()
    {
        var jsonValue = JsonValue<int>("bot_spawns.limit_initial_boss_spawns.max_initial_rogues");
        Assert.That(jsonValue, Is.EqualTo(10), "config.json value");
        // C# default is 6 — 4 fewer rogues allowed
    }

    // ── AdjustPScavChanceConfig ──────────────────────────────────

    [Test]
    public void AdjustPScavChance_Enabled_DefaultFalse_JsonTrue()
    {
        var jsonValue = JsonValue<bool>("adjust_pscav_chance.enabled");
        Assert.That(jsonValue, Is.True, "config.json value");
        // C# default is false — pscav chance adjustment disabled
    }

    // ── ZoneMovementConfig ───────────────────────────────────────

    [Test]
    public void ZoneMovement_ConvergenceWeight_Default1_Json0_3()
    {
        var jsonValue = JsonValue<float>("questing.zone_movement.convergence_weight");
        Assert.That(jsonValue, Is.EqualTo(0.3f), "config.json value");
        // C# default is 1.0 — 3.3x too strong, bots clump around players
    }

    [Test]
    public void ZoneMovement_CombatConvergenceForce_Default0_5_Json0_8()
    {
        var jsonValue = JsonValue<float>("questing.zone_movement.combat_convergence_force");
        Assert.That(jsonValue, Is.EqualTo(0.8f), "config.json value");
        // C# default is 0.5 — combat events pull bots too weakly
    }

    // ══════════════════════════════════════════════════════════════
    //  BUG: C# property exists but NOT in config.json
    //  These document "orphaned" properties that only rely on C# defaults.
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void StuckBotRemedies_HasOrphanedProperties_NotInJson()
    {
        var remedies = _configJson.SelectToken("questing.stuck_bot_detection.stuck_bot_remedies") as JObject;
        Assert.That(remedies, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(remedies["soft_stuck_fail_delay"], Is.Null, "soft_stuck_fail_delay not in JSON");
            Assert.That(remedies["hard_stuck_path_retry_delay"], Is.Null, "hard_stuck_path_retry_delay not in JSON");
            Assert.That(remedies["hard_stuck_teleport_delay"], Is.Null, "hard_stuck_teleport_delay not in JSON");
            Assert.That(remedies["hard_stuck_fail_delay"], Is.Null, "hard_stuck_fail_delay not in JSON");
            Assert.That(remedies["teleport_enabled"], Is.Null, "teleport_enabled not in JSON");
            Assert.That(remedies["teleport_max_player_distance"], Is.Null, "teleport_max_player_distance not in JSON");
        });
    }

    [Test]
    public void SquadStrategy_EntireSectionMissing_FromJson()
    {
        AssertJsonMissingPath("questing.squad_strategy");
        // The entire squad_strategy config section is absent from config.json.
        // All 30+ properties use only C# defaults. Users cannot tune squad behavior
        // without first adding this section to config.json.
    }

    [Test]
    public void BotLod_EntireSectionMissing_FromJson()
    {
        AssertJsonMissingPath("questing.bot_lod");
        // The entire bot_lod config section is absent from config.json.
        // LOD distances and frame skip counts use only C# defaults.
    }

    // ══════════════════════════════════════════════════════════════
    //  Structural: verify JSON nesting matches C# nesting
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ConfigJson_HasExpectedTopLevelKeys()
    {
        Assert.Multiple(() =>
        {
            AssertJsonHasPath("enabled");
            AssertJsonHasPath("debug");
            AssertJsonHasPath("max_calc_time_per_frame_ms");
            AssertJsonHasPath("chance_of_being_hostile_toward_bosses");
            AssertJsonHasPath("questing");
            AssertJsonHasPath("bot_spawns");
            AssertJsonHasPath("adjust_pscav_chance");
        });
    }

    [Test]
    public void ConfigJson_QuestingSection_HasExpectedKeys()
    {
        Assert.Multiple(() =>
        {
            AssertJsonHasPath("questing.enabled");
            AssertJsonHasPath("questing.bot_pathing_update_interval_ms");
            AssertJsonHasPath("questing.brain_layer_priorities");
            AssertJsonHasPath("questing.quest_selection_timeout");
            AssertJsonHasPath("questing.btr_run_distance");
            AssertJsonHasPath("questing.allowed_bot_types_for_questing");
            AssertJsonHasPath("questing.stuck_bot_detection");
            AssertJsonHasPath("questing.unlocking_doors");
            AssertJsonHasPath("questing.min_time_between_switching_objectives");
            AssertJsonHasPath("questing.default_wait_time_after_objective_completion");
            AssertJsonHasPath("questing.update_bot_zone_after_stopping");
            AssertJsonHasPath("questing.wait_time_before_planting");
            AssertJsonHasPath("questing.quest_generation");
            AssertJsonHasPath("questing.bot_search_distances");
            AssertJsonHasPath("questing.bot_pathing");
            AssertJsonHasPath("questing.bot_questing_requirements");
            AssertJsonHasPath("questing.extraction_requirements");
            AssertJsonHasPath("questing.sprinting_limitations");
            AssertJsonHasPath("questing.bot_quests");
            AssertJsonHasPath("questing.zone_movement");
            AssertJsonHasPath("questing.looting");
            AssertJsonHasPath("questing.vulture");
            AssertJsonHasPath("questing.linger");
            AssertJsonHasPath("questing.investigate");
            AssertJsonHasPath("questing.spawn_entry");
            AssertJsonHasPath("questing.room_clear");
            AssertJsonHasPath("questing.dynamic_objectives");
            AssertJsonHasPath("questing.look_variance");
            AssertJsonHasPath("questing.patrol");
            AssertJsonHasPath("questing.personality");
            AssertJsonHasPath("questing.wait_time_min");
            AssertJsonHasPath("questing.wait_time_max");
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  Values that DO match — regression guards
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void DebugConfig_Defaults_MatchJsonWhereExpected()
    {
        var cfg = new DebugConfig();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.EqualTo(JsonValue<bool>("debug.enabled")));
            Assert.That(cfg.AlwaysSpawnPMCs, Is.EqualTo(JsonValue<bool>("debug.always_spawn_pmcs")));
            Assert.That(cfg.AlwaysSpawnPScavs, Is.EqualTo(JsonValue<bool>("debug.always_spawn_pscavs")));
            Assert.That(cfg.ShowZoneOutlines, Is.EqualTo(JsonValue<bool>("debug.show_zone_outlines")));
            Assert.That(cfg.ShowFailedPaths, Is.EqualTo(JsonValue<bool>("debug.show_failed_paths")));
            Assert.That(cfg.ShowDoorInteractionTestPoints, Is.EqualTo(JsonValue<bool>("debug.show_door_interaction_test_points")));
            Assert.That(cfg.DedicatedLogFile, Is.EqualTo(JsonValue<bool>("debug.dedicated_log_file")));
        });
    }

    [Test]
    public void SprintingLimitationsConfig_Defaults_MatchJsonWhereExpected()
    {
        var cfg = new SprintingLimitationsConfig();
        Assert.Multiple(() =>
        {
            Assert.That(
                cfg.PostCombatCooldownSeconds,
                Is.EqualTo(JsonValue<float>("questing.sprinting_limitations.post_combat_cooldown_seconds"))
            );
            Assert.That(
                cfg.LateRaidNoSprintThreshold,
                Is.EqualTo(JsonValue<float>("questing.sprinting_limitations.late_raid_no_sprint_threshold")).Within(0.001f)
            );
            Assert.That(
                cfg.EnablePostCombatSprintBlock,
                Is.EqualTo(JsonValue<bool>("questing.sprinting_limitations.enable_post_combat_sprint_block"))
            );
            Assert.That(
                cfg.EnableLateRaidSprintBlock,
                Is.EqualTo(JsonValue<bool>("questing.sprinting_limitations.enable_late_raid_sprint_block"))
            );
            Assert.That(
                cfg.EnableSuspicionSprintBlock,
                Is.EqualTo(JsonValue<bool>("questing.sprinting_limitations.enable_suspicion_sprint_block"))
            );
        });
    }

    [Test]
    public void LootingConfig_Defaults_MatchJson()
    {
        var cfg = new LootingConfig();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.EqualTo(JsonValue<bool>("questing.looting.enabled")));
            Assert.That(cfg.DetectContainerDistance, Is.EqualTo(JsonValue<float>("questing.looting.detect_container_distance")));
            Assert.That(cfg.DetectItemDistance, Is.EqualTo(JsonValue<float>("questing.looting.detect_item_distance")));
            Assert.That(cfg.DetectCorpseDistance, Is.EqualTo(JsonValue<float>("questing.looting.detect_corpse_distance")));
            Assert.That(cfg.ScanIntervalSeconds, Is.EqualTo(JsonValue<float>("questing.looting.scan_interval_seconds")));
            Assert.That(cfg.MinItemValue, Is.EqualTo(JsonValue<int>("questing.looting.min_item_value")));
            Assert.That(cfg.MaxConcurrentLooters, Is.EqualTo(JsonValue<int>("questing.looting.max_concurrent_looters")));
            Assert.That(cfg.LootDuringCombat, Is.EqualTo(JsonValue<bool>("questing.looting.loot_during_combat")));
            Assert.That(cfg.ApproachDistance, Is.EqualTo(JsonValue<float>("questing.looting.approach_distance")).Within(0.001f));
            Assert.That(cfg.MaxLootingTimeSeconds, Is.EqualTo(JsonValue<float>("questing.looting.max_looting_time_seconds")));
            Assert.That(cfg.LootCooldownSeconds, Is.EqualTo(JsonValue<float>("questing.looting.loot_cooldown_seconds")));
            Assert.That(cfg.ValueScoreCap, Is.EqualTo(JsonValue<float>("questing.looting.value_score_cap")));
            Assert.That(
                cfg.DistancePenaltyFactor,
                Is.EqualTo(JsonValue<float>("questing.looting.distance_penalty_factor")).Within(0.0001f)
            );
            Assert.That(cfg.QuestProximityBonus, Is.EqualTo(JsonValue<float>("questing.looting.quest_proximity_bonus")).Within(0.001f));
            Assert.That(
                cfg.GearUpgradeScoreBonus,
                Is.EqualTo(JsonValue<float>("questing.looting.gear_upgrade_score_bonus")).Within(0.001f)
            );
        });
    }

    [Test]
    public void VultureConfig_Defaults_MatchJson()
    {
        var cfg = new VultureConfig();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.EqualTo(JsonValue<bool>("questing.vulture.enabled")));
            Assert.That(cfg.BaseDetectionRange, Is.EqualTo(JsonValue<float>("questing.vulture.base_detection_range")));
            Assert.That(cfg.NightRangeMultiplier, Is.EqualTo(JsonValue<float>("questing.vulture.night_range_multiplier")).Within(0.001f));
            Assert.That(cfg.CourageThreshold, Is.EqualTo(JsonValue<int>("questing.vulture.courage_threshold")));
            Assert.That(cfg.AmbushDuration, Is.EqualTo(JsonValue<float>("questing.vulture.ambush_duration")));
            Assert.That(cfg.MaxEventAge, Is.EqualTo(JsonValue<float>("questing.vulture.max_event_age")));
            Assert.That(cfg.EventBufferSize, Is.EqualTo(JsonValue<int>("questing.vulture.event_buffer_size")));
            Assert.That(cfg.EnableForPmcs, Is.EqualTo(JsonValue<bool>("questing.vulture.enable_for_pmcs")));
            Assert.That(cfg.EnableForScavs, Is.EqualTo(JsonValue<bool>("questing.vulture.enable_for_scavs")));
        });
    }

    [Test]
    public void LingerConfig_Defaults_MatchJson()
    {
        var cfg = new LingerConfig();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.EqualTo(JsonValue<bool>("questing.linger.enabled")));
            Assert.That(cfg.BaseScore, Is.EqualTo(JsonValue<float>("questing.linger.base_score")).Within(0.001f));
            Assert.That(cfg.DurationMin, Is.EqualTo(JsonValue<float>("questing.linger.duration_min")));
            Assert.That(cfg.DurationMax, Is.EqualTo(JsonValue<float>("questing.linger.duration_max")));
            Assert.That(cfg.Pose, Is.EqualTo(JsonValue<float>("questing.linger.pose")).Within(0.001f));
            Assert.That(cfg.EnableForPmcs, Is.EqualTo(JsonValue<bool>("questing.linger.enable_for_pmcs")));
            Assert.That(cfg.EnableForScavs, Is.EqualTo(JsonValue<bool>("questing.linger.enable_for_scavs")));
            Assert.That(cfg.EnableForPscavs, Is.EqualTo(JsonValue<bool>("questing.linger.enable_for_pscavs")));
        });
    }

    [Test]
    public void InvestigateConfig_Defaults_MatchJson()
    {
        var cfg = new InvestigateConfig();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.EqualTo(JsonValue<bool>("questing.investigate.enabled")));
            Assert.That(cfg.BaseScore, Is.EqualTo(JsonValue<float>("questing.investigate.base_score")).Within(0.001f));
            Assert.That(cfg.IntensityThreshold, Is.EqualTo(JsonValue<int>("questing.investigate.intensity_threshold")));
            Assert.That(cfg.DetectionRange, Is.EqualTo(JsonValue<float>("questing.investigate.detection_range")));
            Assert.That(cfg.MovementTimeout, Is.EqualTo(JsonValue<float>("questing.investigate.movement_timeout")));
            Assert.That(cfg.EnableForPmcs, Is.EqualTo(JsonValue<bool>("questing.investigate.enable_for_pmcs")));
            Assert.That(cfg.EnableForScavs, Is.EqualTo(JsonValue<bool>("questing.investigate.enable_for_scavs")));
            Assert.That(cfg.EnableForPscavs, Is.EqualTo(JsonValue<bool>("questing.investigate.enable_for_pscavs")));
        });
    }

    [Test]
    public void SpawnEntryConfig_Defaults_MatchJson()
    {
        var cfg = new SpawnEntryConfig();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.EqualTo(JsonValue<bool>("questing.spawn_entry.enabled")));
            Assert.That(cfg.BaseDurationMin, Is.EqualTo(JsonValue<float>("questing.spawn_entry.base_duration_min")));
            Assert.That(cfg.BaseDurationMax, Is.EqualTo(JsonValue<float>("questing.spawn_entry.base_duration_max")));
            Assert.That(
                cfg.SquadStaggerPerMember,
                Is.EqualTo(JsonValue<float>("questing.spawn_entry.squad_stagger_per_member")).Within(0.001f)
            );
            Assert.That(cfg.DirectionBiasDuration, Is.EqualTo(JsonValue<float>("questing.spawn_entry.direction_bias_duration")));
            Assert.That(
                cfg.DirectionBiasStrength,
                Is.EqualTo(JsonValue<float>("questing.spawn_entry.direction_bias_strength")).Within(0.001f)
            );
            Assert.That(cfg.Pose, Is.EqualTo(JsonValue<float>("questing.spawn_entry.pose")).Within(0.001f));
        });
    }

    [Test]
    public void RoomClearConfig_Defaults_MatchJson()
    {
        var cfg = new RoomClearConfig();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.EqualTo(JsonValue<bool>("questing.room_clear.enabled")));
            Assert.That(cfg.DurationMin, Is.EqualTo(JsonValue<float>("questing.room_clear.duration_min")));
            Assert.That(cfg.DurationMax, Is.EqualTo(JsonValue<float>("questing.room_clear.duration_max")));
            Assert.That(cfg.CornerPauseDuration, Is.EqualTo(JsonValue<float>("questing.room_clear.corner_pause_duration")).Within(0.001f));
            Assert.That(cfg.CornerAngleThreshold, Is.EqualTo(JsonValue<float>("questing.room_clear.corner_angle_threshold")));
            Assert.That(cfg.Pose, Is.EqualTo(JsonValue<float>("questing.room_clear.pose")).Within(0.001f));
            Assert.That(cfg.EnableForPmcs, Is.EqualTo(JsonValue<bool>("questing.room_clear.enable_for_pmcs")));
            Assert.That(cfg.EnableForScavs, Is.EqualTo(JsonValue<bool>("questing.room_clear.enable_for_scavs")));
            Assert.That(cfg.EnableForPscavs, Is.EqualTo(JsonValue<bool>("questing.room_clear.enable_for_pscavs")));
        });
    }

    [Test]
    public void DynamicObjectiveConfig_Defaults_MatchJson()
    {
        var cfg = new DynamicObjectiveConfig();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.EqualTo(JsonValue<bool>("questing.dynamic_objectives.enabled")));
            Assert.That(cfg.ScanIntervalSec, Is.EqualTo(JsonValue<float>("questing.dynamic_objectives.scan_interval_sec")));
            Assert.That(cfg.MaxActiveQuests, Is.EqualTo(JsonValue<int>("questing.dynamic_objectives.max_active_quests")));
            Assert.That(cfg.FirefightEnabled, Is.EqualTo(JsonValue<bool>("questing.dynamic_objectives.firefight_enabled")));
            Assert.That(cfg.FirefightMinIntensity, Is.EqualTo(JsonValue<int>("questing.dynamic_objectives.firefight_min_intensity")));
            Assert.That(cfg.FirefightDesirability, Is.EqualTo(JsonValue<float>("questing.dynamic_objectives.firefight_desirability")));
            Assert.That(cfg.CorpseEnabled, Is.EqualTo(JsonValue<bool>("questing.dynamic_objectives.corpse_enabled")));
            Assert.That(cfg.CorpseDesirability, Is.EqualTo(JsonValue<float>("questing.dynamic_objectives.corpse_desirability")));
        });
    }

    [Test]
    public void LookVarianceConfig_Defaults_MatchJson()
    {
        var cfg = new LookVarianceConfig();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.EqualTo(JsonValue<bool>("questing.look_variance.enabled")));
            Assert.That(cfg.FlankCheckIntervalMin, Is.EqualTo(JsonValue<float>("questing.look_variance.flank_check_interval_min")));
            Assert.That(cfg.FlankCheckIntervalMax, Is.EqualTo(JsonValue<float>("questing.look_variance.flank_check_interval_max")));
            Assert.That(
                cfg.CombatEventLookChance,
                Is.EqualTo(JsonValue<float>("questing.look_variance.combat_event_look_chance")).Within(0.001f)
            );
        });
    }

    [Test]
    public void PatrolConfig_Defaults_MatchJson()
    {
        var cfg = new PatrolConfig();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.EqualTo(JsonValue<bool>("questing.patrol.enabled")));
            Assert.That(cfg.BaseScore, Is.EqualTo(JsonValue<float>("questing.patrol.base_score")).Within(0.001f));
            Assert.That(cfg.CooldownSec, Is.EqualTo(JsonValue<float>("questing.patrol.cooldown_sec")));
            Assert.That(cfg.WaypointArrivalRadius, Is.EqualTo(JsonValue<float>("questing.patrol.waypoint_arrival_radius")));
            Assert.That(cfg.Pose, Is.EqualTo(JsonValue<float>("questing.patrol.pose")).Within(0.001f));
            Assert.That(cfg.EnableForPmcs, Is.EqualTo(JsonValue<bool>("questing.patrol.enable_for_pmcs")));
            Assert.That(cfg.EnableForScavs, Is.EqualTo(JsonValue<bool>("questing.patrol.enable_for_scavs")));
            Assert.That(cfg.EnableForPscavs, Is.EqualTo(JsonValue<bool>("questing.patrol.enable_for_pscavs")));
        });
    }

    [Test]
    public void PersonalityConfig_Defaults_MatchJson()
    {
        var cfg = new PersonalityConfig();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.EqualTo(JsonValue<bool>("questing.personality.enabled")));
            Assert.That(cfg.RaidTimeEnabled, Is.EqualTo(JsonValue<bool>("questing.personality.raid_time_enabled")));
        });
    }

    [Test]
    public void BotPathingConfig_Defaults_MatchJsonWherePresent()
    {
        var cfg = new BotPathingConfig();
        Assert.Multiple(() =>
        {
            Assert.That(
                cfg.MaxStartPositionDiscrepancy,
                Is.EqualTo(JsonValue<float>("questing.bot_pathing.max_start_position_discrepancy")).Within(0.001f)
            );
            Assert.That(
                cfg.IncompletePathRetryInterval,
                Is.EqualTo(JsonValue<float>("questing.bot_pathing.incomplete_path_retry_interval"))
            );
        });
    }

    [Test]
    public void BotLodConfig_Defaults_AreReasonable()
    {
        var cfg = new BotLodConfig();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.True);
            Assert.That(cfg.ReducedDistance, Is.EqualTo(150f));
            Assert.That(cfg.MinimalDistance, Is.EqualTo(300f));
            Assert.That(cfg.ReducedFrameSkip, Is.EqualTo(2));
            Assert.That(cfg.MinimalFrameSkip, Is.EqualTo(4));
            // Not in config.json — defaults are the only values used
        });
    }

    // ── MinMaxConfig special behavior ────────────────────────────

    [Test]
    public void MinMaxConfig_DefaultsAreGeneric_NotConfigSpecific()
    {
        // MinMaxConfig defaults to min=0, max=100 which is generic.
        // JSON overrides are critical for correct behavior.
        var cfg = new MinMaxConfig();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Min, Is.EqualTo(0));
            Assert.That(cfg.Max, Is.EqualTo(100));
        });
        // Suspicious time: JSON says min=5, max=20 but C# default gives min=0, max=100
        // Extraction total_quests: JSON says min=3, max=8 but C# default gives min=0, max=100
    }
}
