using Newtonsoft.Json;

namespace SPTQuestingBots.Server.Configuration;

/// <summary>
/// Root configuration model for the QuestingBots mod.
/// Deserialized from <c>config/config.json</c> at startup.
///
/// This class mirrors the full JSON structure consumed by both the server-side
/// plugin and the BepInEx client plugin. The client fetches it via the
/// <c>/QuestingBots/GetConfig</c> HTTP endpoint.
/// </summary>
public class QuestingBotsConfig
{
    /// <summary>
    /// Master switch for the entire mod. When <c>false</c>, most routes
    /// and services become no-ops.
    /// </summary>
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    /// <summary>Debug/development options (zone outlines, forced spawns, etc.).</summary>
    [JsonProperty("debug")]
    public DebugConfig Debug { get; set; } = new();

    /// <summary>
    /// Maximum wall-clock milliseconds the client plugin may spend
    /// on per-frame calculations before yielding.
    /// </summary>
    [JsonProperty("max_calc_time_per_frame_ms")]
    public int MaxCalcTimePerFrameMs { get; set; }

    /// <summary>
    /// Per-role percentage chance that a bot will be hostile toward bosses
    /// when it spawns (scav, pscav, pmc, boss).
    /// </summary>
    [JsonProperty("chance_of_being_hostile_toward_bosses")]
    public BotTypeChanceConfig ChanceOfBeingHostileTowardBosses { get; set; } = new();

    /// <summary>Questing behaviour settings (objectives, stuck detection, doors, etc.).</summary>
    [JsonProperty("questing")]
    public QuestingConfig Questing { get; set; } = new();

    /// <summary>Bot spawning system settings (PMC waves, PScav waves, caps, hostility).</summary>
    [JsonProperty("bot_spawns")]
    public BotSpawnsConfig BotSpawns { get; set; } = new();

    /// <summary>
    /// PScav conversion chance curve — adjusts the likelihood that a
    /// regular Scav spawn slot is converted into a Player Scav.
    /// </summary>
    [JsonProperty("adjust_pscav_chance")]
    public AdjustPScavChanceConfig AdjustPScavChance { get; set; } = new();

    /// <summary>
    /// Runtime-only field. Stores the original SPT PScav conversion chance
    /// before QuestingBots overrides it. Not read from <c>config.json</c>.
    /// </summary>
    [JsonIgnore]
    public int BasePScavConversionChance { get; set; }
}

// ────────────────────────────────────────────────────────────────────────
// Debug
// ────────────────────────────────────────────────────────────────────────

/// <summary>Development and debugging flags.</summary>
public class DebugConfig
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    /// <summary>Force PMC spawns regardless of other settings.</summary>
    [JsonProperty("always_spawn_pmcs")]
    public bool AlwaysSpawnPmcs { get; set; }

    /// <summary>Force PScav spawns regardless of other settings.</summary>
    [JsonProperty("always_spawn_pscavs")]
    public bool AlwaysSpawnPScavs { get; set; }

    /// <summary>Render quest-zone outlines in the game world.</summary>
    [JsonProperty("show_zone_outlines")]
    public bool ShowZoneOutlines { get; set; }

    /// <summary>Render failed pathfinding attempts as debug lines.</summary>
    [JsonProperty("show_failed_paths")]
    public bool ShowFailedPaths { get; set; }

    /// <summary>Show door-interaction test points in the game world.</summary>
    [JsonProperty("show_door_interaction_test_points")]
    public bool ShowDoorInteractionTestPoints { get; set; }

    /// <summary>
    /// Allow bots to enter the sleeping layer even when their
    /// objective distance is zero (useful for testing).
    /// </summary>
    [JsonProperty("allow_zero_distance_sleeping")]
    public bool AllowZeroDistanceSleeping { get; set; }
}

// ────────────────────────────────────────────────────────────────────────
// Shared primitives
// ────────────────────────────────────────────────────────────────────────

/// <summary>Integer values keyed by bot role (scav, pscav, pmc, boss).</summary>
public class BotTypeChanceConfig
{
    [JsonProperty("scav")]
    public int Scav { get; set; }

    [JsonProperty("pscav")]
    public int PScav { get; set; }

    [JsonProperty("pmc")]
    public int Pmc { get; set; }

    [JsonProperty("boss")]
    public int Boss { get; set; }
}

/// <summary>Boolean flags keyed by bot role (scav, pscav, pmc, boss).</summary>
public class BotTypeBoolConfig
{
    [JsonProperty("scav")]
    public bool Scav { get; set; }

    [JsonProperty("pscav")]
    public bool PScav { get; set; }

    [JsonProperty("pmc")]
    public bool Pmc { get; set; }

    [JsonProperty("boss")]
    public bool Boss { get; set; }
}

/// <summary>Generic min/max pair used throughout the config.</summary>
public class MinMaxConfig
{
    [JsonProperty("min")]
    public double Min { get; set; }

    [JsonProperty("max")]
    public double Max { get; set; }
}

/// <summary>Distance + angle pair for spatial checks (e.g. sprint corner detection).</summary>
public class DistanceAngleConfig
{
    [JsonProperty("distance")]
    public int Distance { get; set; }

    [JsonProperty("angle")]
    public int Angle { get; set; }
}

// ────────────────────────────────────────────────────────────────────────
// Questing
// ────────────────────────────────────────────────────────────────────────

/// <summary>Top-level questing behaviour settings.</summary>
public class QuestingConfig
{
    /// <summary>Enable/disable the entire questing system.</summary>
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    /// <summary>How often (ms) to recalculate bot navigation paths.</summary>
    [JsonProperty("bot_pathing_update_interval_ms")]
    public int BotPathingUpdateIntervalMs { get; set; }

    /// <summary>BigBrain layer priority settings (differs with/without SAIN).</summary>
    [JsonProperty("brain_layer_priorities")]
    public BrainLayerPrioritiesConfig BrainLayerPriorities { get; set; } = new();

    /// <summary>Maximum milliseconds to spend selecting a quest per frame.</summary>
    [JsonProperty("quest_selection_timeout")]
    public int QuestSelectionTimeout { get; set; }

    /// <summary>Distance (m) at which bots will run from the BTR.</summary>
    [JsonProperty("btr_run_distance")]
    public int BtrRunDistance { get; set; }

    /// <summary>Which bot roles are allowed to do quests.</summary>
    [JsonProperty("allowed_bot_types_for_questing")]
    public BotTypeBoolConfig AllowedBotTypesForQuesting { get; set; } = new();

    /// <summary>Parameters for detecting and resolving stuck bots.</summary>
    [JsonProperty("stuck_bot_detection")]
    public StuckBotDetectionConfig StuckBotDetection { get; set; } = new();

    /// <summary>Settings for bots unlocking doors during quests.</summary>
    [JsonProperty("unlocking_doors")]
    public UnlockingDoorsConfig UnlockingDoors { get; set; } = new();

    /// <summary>Minimum seconds between objective switches to prevent thrashing.</summary>
    [JsonProperty("min_time_between_switching_objectives")]
    public int MinTimeBetweenSwitchingObjectives { get; set; }

    /// <summary>Seconds to wait after completing an objective before selecting the next one.</summary>
    [JsonProperty("default_wait_time_after_objective_completion")]
    public int DefaultWaitTimeAfterObjectiveCompletion { get; set; }

    /// <summary>Whether to update a bot's zone assignment after it stops moving.</summary>
    [JsonProperty("update_bot_zone_after_stopping")]
    public bool UpdateBotZoneAfterStopping { get; set; }

    /// <summary>Seconds to wait before a bot starts planting an item.</summary>
    [JsonProperty("wait_time_before_planting")]
    public int WaitTimeBeforePlanting { get; set; }

    /// <summary>NavMesh search distances for quest generation.</summary>
    [JsonProperty("quest_generation")]
    public QuestGenerationConfig QuestGeneration { get; set; } = new();

    /// <summary>Thresholds for determining whether a bot has reached its objective.</summary>
    [JsonProperty("bot_search_distances")]
    public BotSearchDistancesConfig BotSearchDistances { get; set; } = new();

    /// <summary>Pathing error handling (start-position discrepancy, retry intervals).</summary>
    [JsonProperty("bot_pathing")]
    public BotPathingConfig BotPathing { get; set; } = new();

    /// <summary>Health, stamina, and timing requirements for bots to quest.</summary>
    [JsonProperty("bot_questing_requirements")]
    public BotQuestingRequirementsConfig BotQuestingRequirements { get; set; } = new();

    /// <summary>When and how bots decide to extract from the raid.</summary>
    [JsonProperty("extraction_requirements")]
    public ExtractionRequirementsConfig ExtractionRequirements { get; set; } = new();

    /// <summary>Stamina-based sprint rules and corner-detection settings.</summary>
    [JsonProperty("sprinting_limitations")]
    public SprintingLimitationsConfig SprintingLimitations { get; set; } = new();

    /// <summary>Quest selection weights, per-quest-type configs, and bot limits.</summary>
    [JsonProperty("bot_quests")]
    public BotQuestsConfig BotQuests { get; set; } = new();
}

/// <summary>BigBrain layer priority presets.</summary>
public class BrainLayerPrioritiesConfig
{
    /// <summary>Priorities to use when SAIN is installed.</summary>
    [JsonProperty("with_sain")]
    public BrainLayerPrioritySet WithSain { get; set; } = new();

    /// <summary>Priorities to use without SAIN.</summary>
    [JsonProperty("without_sain")]
    public BrainLayerPrioritySet WithoutSain { get; set; } = new();
}

/// <summary>Individual brain-layer priority values.</summary>
public class BrainLayerPrioritySet
{
    [JsonProperty("questing")]
    public int Questing { get; set; }

    [JsonProperty("following")]
    public int Following { get; set; }

    [JsonProperty("regrouping")]
    public int Regrouping { get; set; }

    [JsonProperty("sleeping")]
    public int Sleeping { get; set; }
}

/// <summary>Stuck-bot detection thresholds and remedies.</summary>
public class StuckBotDetectionConfig
{
    /// <summary>Movement distance (m) below which a bot is considered stuck.</summary>
    [JsonProperty("distance")]
    public int Distance { get; set; }

    /// <summary>Seconds a bot must be stuck before remedies kick in.</summary>
    [JsonProperty("time")]
    public int Time { get; set; }

    /// <summary>Maximum stuck detections before the bot gives up on the objective.</summary>
    [JsonProperty("max_count")]
    public int MaxCount { get; set; }

    /// <summary>Seconds a follower pauses when its leader is stuck.</summary>
    [JsonProperty("follower_break_time")]
    public int FollowerBreakTime { get; set; }

    /// <summary>Maximum seconds a bot can be injured/debilitated before aborting quests.</summary>
    [JsonProperty("max_not_able_bodied_time")]
    public int MaxNotAbleBodiedTime { get; set; }

    /// <summary>Jump and vault remedies for stuck bots.</summary>
    [JsonProperty("stuck_bot_remedies")]
    public StuckBotRemediesConfig StuckBotRemedies { get; set; } = new();
}

/// <summary>Automated jump/vault attempts to free stuck bots.</summary>
public class StuckBotRemediesConfig
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    [JsonProperty("min_time_before_jumping")]
    public int MinTimeBeforeJumping { get; set; }

    [JsonProperty("jump_debounce_time")]
    public int JumpDebounceTime { get; set; }

    [JsonProperty("min_time_before_vaulting")]
    public int MinTimeBeforeVaulting { get; set; }

    [JsonProperty("vault_debounce_time")]
    public int VaultDebounceTime { get; set; }
}

/// <summary>Settings for bots attempting to unlock doors.</summary>
public class UnlockingDoorsConfig
{
    /// <summary>Which bot roles can unlock doors.</summary>
    [JsonProperty("enabled")]
    public BotTypeBoolConfig Enabled { get; set; } = new();

    /// <summary>NavMesh search radius (m) when looking for doors.</summary>
    [JsonProperty("search_radius")]
    public int SearchRadius { get; set; }

    /// <summary>Maximum distance (m) from the door for the unlock interaction to trigger.</summary>
    [JsonProperty("max_distance_to_unlock")]
    public double MaxDistanceToUnlock { get; set; }

    [JsonProperty("door_approach_position_search_radius")]
    public double DoorApproachPositionSearchRadius { get; set; }

    [JsonProperty("door_approach_position_search_offset")]
    public double DoorApproachPositionSearchOffset { get; set; }

    /// <summary>Seconds the bot pauses after unlocking a door.</summary>
    [JsonProperty("pause_time_after_unlocking")]
    public int PauseTimeAfterUnlocking { get; set; }

    /// <summary>Minimum seconds between door-unlock attempts.</summary>
    [JsonProperty("debounce_time")]
    public int DebounceTime { get; set; }

    /// <summary>Base percentage chance that a bot has the correct key.</summary>
    [JsonProperty("default_chance_of_bots_having_keys")]
    public int DefaultChanceOfBotsHavingKeys { get; set; }
}

/// <summary>NavMesh search distances used during quest generation.</summary>
public class QuestGenerationConfig
{
    [JsonProperty("navmesh_search_distance_item")]
    public double NavmeshSearchDistanceItem { get; set; }

    [JsonProperty("navmesh_search_distance_zone")]
    public double NavmeshSearchDistanceZone { get; set; }

    [JsonProperty("navmesh_search_distance_spawn")]
    public double NavmeshSearchDistanceSpawn { get; set; }

    [JsonProperty("navmesh_search_distance_doors")]
    public double NavmeshSearchDistanceDoors { get; set; }
}

/// <summary>Thresholds for determining when a bot has reached its objective.</summary>
public class BotSearchDistancesConfig
{
    /// <summary>Ideal distance (m) at which the objective is considered reached.</summary>
    [JsonProperty("objective_reached_ideal")]
    public double ObjectiveReachedIdeal { get; set; }

    /// <summary>Acceptable distance (m) when there is a NavMesh path error.</summary>
    [JsonProperty("objective_reached_navmesh_path_error")]
    public double ObjectiveReachedNavmeshPathError { get; set; }

    /// <summary>Maximum tolerable NavMesh path error before giving up.</summary>
    [JsonProperty("max_navmesh_path_error")]
    public double MaxNavmeshPathError { get; set; }
}

/// <summary>Bot pathing error handling.</summary>
public class BotPathingConfig
{
    /// <summary>Maximum allowed discrepancy (m) between the bot's actual start and the path start.</summary>
    [JsonProperty("max_start_position_discrepancy")]
    public double MaxStartPositionDiscrepancy { get; set; }

    /// <summary>Seconds to wait before retrying an incomplete path.</summary>
    [JsonProperty("incomplete_path_retry_interval")]
    public int IncompletePathRetryInterval { get; set; }
}

/// <summary>Requirements a bot must meet before it can quest.</summary>
public class BotQuestingRequirementsConfig
{
    /// <summary>Exclude bots whose level falls outside the quest's level range.</summary>
    [JsonProperty("exclude_bots_by_level")]
    public bool ExcludeBotsByLevel { get; set; }

    /// <summary>Seconds before a bot can repeat the same quest.</summary>
    [JsonProperty("repeat_quest_delay")]
    public int RepeatQuestDelay { get; set; }

    /// <summary>Maximum seconds a bot will spend on a single quest.</summary>
    [JsonProperty("max_time_per_quest")]
    public int MaxTimePerQuest { get; set; }

    [JsonProperty("min_hydration")]
    public int MinHydration { get; set; }

    [JsonProperty("min_energy")]
    public int MinEnergy { get; set; }

    [JsonProperty("min_health_head")]
    public int MinHealthHead { get; set; }

    [JsonProperty("min_health_chest")]
    public int MinHealthChest { get; set; }

    [JsonProperty("min_health_stomach")]
    public int MinHealthStomach { get; set; }

    [JsonProperty("min_health_legs")]
    public int MinHealthLegs { get; set; }

    [JsonProperty("max_overweight_percentage")]
    public int MaxOverweightPercentage { get; set; }

    /// <summary>How long bots search the area after combat ends.</summary>
    [JsonProperty("search_time_after_combat")]
    public SearchTimeAfterCombatConfig SearchTimeAfterCombat { get; set; } = new();

    /// <summary>Settings for the hearing sensor that makes bots react to sounds.</summary>
    [JsonProperty("hearing_sensor")]
    public HearingSensorConfig HearingSensor { get; set; } = new();

    /// <summary>Settings for bots pausing to loot during quests.</summary>
    [JsonProperty("break_for_looting")]
    public BreakForLootingConfig BreakForLooting { get; set; } = new();

    /// <summary>Maximum distance followers can be from their boss.</summary>
    [JsonProperty("max_follower_distance")]
    public MaxFollowerDistanceConfig MaxFollowerDistance { get; set; } = new();
}

/// <summary>Post-combat search duration settings.</summary>
public class SearchTimeAfterCombatConfig
{
    /// <summary>Search time when SAIN combat is prioritized.</summary>
    [JsonProperty("prioritized_sain")]
    public MinMaxConfig PrioritizedSain { get; set; } = new();

    /// <summary>Search time when questing is prioritized.</summary>
    [JsonProperty("prioritized_questing")]
    public MinMaxConfig PrioritizedQuesting { get; set; } = new();
}

/// <summary>
/// Hearing sensor settings. Controls how bots react to footsteps and gunfire
/// while questing, causing them to become suspicious and potentially pause.
/// </summary>
public class HearingSensorConfig
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    /// <summary>Minimum corrected sound power required to trigger suspicion.</summary>
    [JsonProperty("min_corrected_sound_power")]
    public int MinCorrectedSoundPower { get; set; }

    [JsonProperty("max_distance_footsteps")]
    public int MaxDistanceFootsteps { get; set; }

    [JsonProperty("max_distance_gunfire")]
    public int MaxDistanceGunfire { get; set; }

    [JsonProperty("max_distance_gunfire_suppressed")]
    public int MaxDistanceGunfireSuppressed { get; set; }

    [JsonProperty("loudness_multiplier_footsteps")]
    public double LoudnessMultiplierFootsteps { get; set; }

    [JsonProperty("loudness_multiplier_headset")]
    public double LoudnessMultiplierHeadset { get; set; }

    [JsonProperty("loudness_multiplier_helmet_low_deaf")]
    public double LoudnessMultiplierHelmetLowDeaf { get; set; }

    [JsonProperty("loudness_multiplier_helmet_high_deaf")]
    public double LoudnessMultiplierHelmetHighDeaf { get; set; }

    /// <summary>Duration range for the suspicion state.</summary>
    [JsonProperty("suspicious_time")]
    public MinMaxConfig SuspiciousTime { get; set; } = new();

    /// <summary>Per-map maximum suspicion duration overrides.</summary>
    [JsonProperty("max_suspicious_time")]
    public Dictionary<string, int> MaxSuspiciousTime { get; set; } = new();

    /// <summary>Cooldown (s) after suspicion ends before it can trigger again.</summary>
    [JsonProperty("suspicion_cooldown_time")]
    public int SuspicionCooldownTime { get; set; }
}

/// <summary>Settings for bots pausing their quest to loot.</summary>
public class BreakForLootingConfig
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    [JsonProperty("min_time_between_looting_checks")]
    public int MinTimeBetweenLootingChecks { get; set; }

    [JsonProperty("min_time_between_follower_looting_checks")]
    public int MinTimeBetweenFollowerLootingChecks { get; set; }

    [JsonProperty("min_time_between_looting_events")]
    public int MinTimeBetweenLootingEvents { get; set; }

    [JsonProperty("max_time_to_start_looting")]
    public int MaxTimeToStartLooting { get; set; }

    [JsonProperty("max_loot_scan_time")]
    public int MaxLootScanTime { get; set; }

    /// <summary>Followers won't loot if they are farther than this from their boss.</summary>
    [JsonProperty("max_distance_from_boss")]
    public int MaxDistanceFromBoss { get; set; }
}

/// <summary>Follower regrouping distance thresholds.</summary>
public class MaxFollowerDistanceConfig
{
    [JsonProperty("max_wait_time")]
    public int MaxWaitTime { get; set; }

    [JsonProperty("min_regroup_time")]
    public int MinRegroupTime { get; set; }

    [JsonProperty("regroup_pause_time")]
    public int RegroupPauseTime { get; set; }

    [JsonProperty("target_position_variation_allowed")]
    public int TargetPositionVariationAllowed { get; set; }

    /// <summary>Acceptable distance range from boss while questing.</summary>
    [JsonProperty("target_range_questing")]
    public MinMaxConfig TargetRangeQuesting { get; set; } = new();

    /// <summary>Acceptable distance range from boss while in combat.</summary>
    [JsonProperty("target_range_combat")]
    public MinMaxConfig TargetRangeCombat { get; set; } = new();

    [JsonProperty("nearest")]
    public int Nearest { get; set; }

    [JsonProperty("furthest")]
    public int Furthest { get; set; }
}

/// <summary>Rules for when bots decide to extract from the raid.</summary>
public class ExtractionRequirementsConfig
{
    /// <summary>Use SAIN's extraction logic instead of the built-in one.</summary>
    [JsonProperty("use_sain_for_extracting")]
    public bool UseSainForExtracting { get; set; }

    /// <summary>Minimum seconds the bot must have been alive before extracting.</summary>
    [JsonProperty("min_alive_time")]
    public int MinAliveTime { get; set; }

    /// <summary>Seconds of raid time remaining at which the bot must extract.</summary>
    [JsonProperty("must_extract_time_remaining")]
    public int MustExtractTimeRemaining { get; set; }

    /// <summary>Total quest completions required before extraction is considered.</summary>
    [JsonProperty("total_quests")]
    public MinMaxConfig TotalQuests { get; set; } = new();

    /// <summary>EFT quest completions required before extraction is considered.</summary>
    [JsonProperty("EFT_quests")]
    public MinMaxConfig EftQuests { get; set; } = new();
}

/// <summary>Sprint limitations based on stamina and path geometry.</summary>
public class SprintingLimitationsConfig
{
    /// <summary>Seconds of debounce before sprint can be re-enabled.</summary>
    [JsonProperty("enable_debounce_time")]
    public int EnableDebounceTime { get; set; }

    /// <summary>Stamina thresholds for sprinting.</summary>
    [JsonProperty("stamina")]
    public MinMaxConfig Stamina { get; set; } = new();

    /// <summary>Corner detection: stop sprinting near sharp turns.</summary>
    [JsonProperty("sharp_path_corners")]
    public DistanceAngleConfig SharpPathCorners { get; set; } = new();

    /// <summary>Stop sprinting when approaching a closed door.</summary>
    [JsonProperty("approaching_closed_doors")]
    public DistanceAngleConfig ApproachingClosedDoors { get; set; } = new();
}

// ────────────────────────────────────────────────────────────────────────
// Bot quests
// ────────────────────────────────────────────────────────────────────────

/// <summary>
/// Quest selection weights and per-quest-type configuration.
/// Controls how bots choose which quest to pursue.
/// </summary>
public class BotQuestsConfig
{
    /// <summary>Random offset added to distance calculations for variety.</summary>
    [JsonProperty("distance_randomness")]
    public int DistanceRandomness { get; set; }

    /// <summary>Random offset added to desirability scores for variety.</summary>
    [JsonProperty("desirability_randomness")]
    public int DesirabilityRandomness { get; set; }

    /// <summary>Weight multiplier for distance in quest scoring.</summary>
    [JsonProperty("distance_weighting")]
    public double DistanceWeighting { get; set; }

    /// <summary>Weight multiplier for desirability in quest scoring.</summary>
    [JsonProperty("desirability_weighting")]
    public double DesirabilityWeighting { get; set; }

    [JsonProperty("desirability_camping_multiplier")]
    public double DesirabilityCampingMultiplier { get; set; }

    [JsonProperty("desirability_sniping_multiplier")]
    public double DesirabilitySnipingMultiplier { get; set; }

    [JsonProperty("desirability_active_quest_multiplier")]
    public double DesirabilityActiveQuestMultiplier { get; set; }

    /// <summary>Per-map weighting for directing bots toward their extraction point.</summary>
    [JsonProperty("exfil_direction_weighting")]
    public Dictionary<string, double> ExfilDirectionWeighting { get; set; } = new();

    [JsonProperty("exfil_direction_max_angle")]
    public int ExfilDirectionMaxAngle { get; set; }

    [JsonProperty("exfil_reached_min_fraction")]
    public double ExfilReachedMinFraction { get; set; }

    /// <summary>Boss roles that should not be hunted by the boss-hunter quest type.</summary>
    [JsonProperty("blacklisted_boss_hunter_bosses")]
    public List<string> BlacklistedBossHunterBosses { get; set; } = [];

    /// <summary>Seconds after an airdrop that bots remain interested in it.</summary>
    [JsonProperty("airdrop_bot_interest_time")]
    public int AirdropBotInterestTime { get; set; }

    /// <summary>Seconds a bot will search for elimination targets.</summary>
    [JsonProperty("elimination_quest_search_time")]
    public int EliminationQuestSearchTime { get; set; }

    /// <summary>EFT quest-specific settings (level ranges, key chances, etc.).</summary>
    [JsonProperty("eft_quests")]
    public EftQuestsConfig EftQuests { get; set; } = new();

    /// <summary>Lightkeeper Island quest toggle.</summary>
    [JsonProperty("lightkeeper_island_quests")]
    public LightkeeperIslandQuestsConfig LightkeeperIslandQuests { get; set; } = new();

    /// <summary>Spawn-rush quest settings (run to player spawns early in raid).</summary>
    [JsonProperty("spawn_rush")]
    public SpawnRushConfig SpawnRush { get; set; } = new();

    /// <summary>Spawn-point wander quest settings.</summary>
    [JsonProperty("spawn_point_wander")]
    public SpawnPointWanderConfig SpawnPointWander { get; set; } = new();

    /// <summary>Boss-hunter quest settings (PMCs actively seek bosses).</summary>
    [JsonProperty("boss_hunter")]
    public BossHunterConfig BossHunter { get; set; } = new();

    /// <summary>Airdrop-chaser quest settings.</summary>
    [JsonProperty("airdrop_chaser")]
    public AirdropChaserConfig AirdropChaser { get; set; } = new();
}

/// <summary>Settings for quests derived from EFT's built-in quest database.</summary>
public class EftQuestsConfig
{
    /// <summary>Base desirability score for EFT quests.</summary>
    [JsonProperty("desirability")]
    public int Desirability { get; set; }

    /// <summary>Maximum simultaneous bots pursuing the same EFT quest.</summary>
    [JsonProperty("max_bots_per_quest")]
    public int MaxBotsPerQuest { get; set; }

    /// <summary>Percentage chance a bot has the key needed for a quest.</summary>
    [JsonProperty("chance_of_having_keys")]
    public int ChanceOfHavingKeys { get; set; }

    /// <summary>Distance (m) at which looting behaviour matches the quest type.</summary>
    [JsonProperty("match_looting_behavior_distance")]
    public int MatchLootingBehaviorDistance { get; set; }

    /// <summary>
    /// 2D array mapping bot level thresholds to quest level ranges.
    /// Each row is <c>[bot_level_threshold, quest_level_range]</c>.
    /// </summary>
    [JsonProperty("level_range")]
    public double[][] LevelRange { get; set; } = [];
}

/// <summary>Toggle for Lightkeeper Island quests on Lighthouse.</summary>
public class LightkeeperIslandQuestsConfig
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }
}

/// <summary>
/// Spawn-rush quest: bots run to player spawn locations early in the raid.
/// </summary>
public class SpawnRushConfig
{
    [JsonProperty("desirability")]
    public int Desirability { get; set; }

    /// <summary>Only PMCs can spawn-rush (not scavs).</summary>
    [JsonProperty("pmcsOnly")]
    public bool PmcsOnly { get; set; }

    [JsonProperty("max_bots_per_quest")]
    public int MaxBotsPerQuest { get; set; }

    /// <summary>Maximum distance (m) from spawn to consider rushing.</summary>
    [JsonProperty("max_distance")]
    public int MaxDistance { get; set; }

    /// <summary>Maximum elapsed raid time (s) before spawn-rush quests expire.</summary>
    [JsonProperty("max_raid_ET")]
    public int MaxRaidET { get; set; }
}

/// <summary>Spawn-point wander: bots wander near map spawn locations.</summary>
public class SpawnPointWanderConfig
{
    [JsonProperty("desirability")]
    public int Desirability { get; set; }

    /// <summary>Minimum distance (m) from the bot's own spawn to wander targets.</summary>
    [JsonProperty("min_distance")]
    public int MinDistance { get; set; }

    [JsonProperty("max_bots_per_quest")]
    public int MaxBotsPerQuest { get; set; }
}

/// <summary>Boss-hunter quest: PMCs actively seek out and hunt bosses.</summary>
public class BossHunterConfig
{
    [JsonProperty("desirability")]
    public int Desirability { get; set; }

    [JsonProperty("pmcsOnly")]
    public bool PmcsOnly { get; set; }

    /// <summary>Minimum PMC level to unlock boss-hunting.</summary>
    [JsonProperty("min_level")]
    public int MinLevel { get; set; }

    /// <summary>Maximum elapsed raid time (s) before boss-hunter quests expire.</summary>
    [JsonProperty("max_raid_ET")]
    public int MaxRaidET { get; set; }

    /// <summary>Minimum distance (m) from the boss to activate the quest.</summary>
    [JsonProperty("min_distance")]
    public int MinDistance { get; set; }

    [JsonProperty("max_bots_per_quest")]
    public int MaxBotsPerQuest { get; set; }
}

/// <summary>Airdrop-chaser quest: bots run toward airdrop landing zones.</summary>
public class AirdropChaserConfig
{
    [JsonProperty("desirability")]
    public int Desirability { get; set; }

    [JsonProperty("max_bots_per_quest")]
    public int MaxBotsPerQuest { get; set; }

    /// <summary>Maximum distance (m) from the airdrop to consider chasing.</summary>
    [JsonProperty("max_distance")]
    public int MaxDistance { get; set; }
}

// ────────────────────────────────────────────────────────────────────────
// Bot spawns
// ────────────────────────────────────────────────────────────────────────

/// <summary>
/// Bot spawning system settings. Controls PMC/PScav wave generation,
/// bot caps, hostility, and integration with other spawning mods.
/// </summary>
public class BotSpawnsConfig
{
    /// <summary>Enable/disable the QuestingBots spawning system entirely.</summary>
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// Brain types that should never be used for PMCs or Player Scavs.
    /// Prevents bots from receiving boss-like AI when converted.
    /// </summary>
    [JsonProperty("blacklisted_pmc_bot_brains")]
    public List<string> BlacklistedPmcBotBrains { get; set; } = [];

    /// <summary>Seconds between spawn retry attempts when a spawn fails.</summary>
    [JsonProperty("spawn_retry_time")]
    public int SpawnRetryTime { get; set; }

    /// <summary>Hold the game-start countdown until initial bot generation completes.</summary>
    [JsonProperty("delay_game_start_until_bot_gen_finishes")]
    public bool DelayGameStartUntilBotGenFinishes { get; set; }

    /// <summary>Spawn boss bots before PMCs (affects spawn ordering).</summary>
    [JsonProperty("spawn_initial_bosses_first")]
    public bool SpawnInitialBossesFirst { get; set; }

    /// <summary>Adjustments to EFT's new wave-based spawn system.</summary>
    [JsonProperty("eft_new_spawn_system_adjustments")]
    public EftNewSpawnSystemAdjustmentsConfig EftNewSpawnSystemAdjustments { get; set; } = new();

    /// <summary>Bot population cap adjustments (EFT caps vs SPT caps).</summary>
    [JsonProperty("bot_cap_adjustments")]
    public BotCapAdjustmentsConfig BotCapAdjustments { get; set; } = new();

    /// <summary>Limits on how many bosses/rogues spawn at raid start.</summary>
    [JsonProperty("limit_initial_boss_spawns")]
    public LimitInitialBossSpawnsConfig LimitInitialBossSpawns { get; set; } = new();

    /// <summary>Per-map maximum alive-bot overrides.</summary>
    [JsonProperty("max_alive_bots")]
    public Dictionary<string, int> MaxAliveBots { get; set; } = new();

    /// <summary>PMC-vs-Scav and PMC-vs-PMC hostility overrides.</summary>
    [JsonProperty("pmc_hostility_adjustments")]
    public PmcHostilityAdjustmentsConfig PmcHostilityAdjustments { get; set; } = new();

    /// <summary>PMC spawn wave configuration.</summary>
    [JsonProperty("pmcs")]
    public PmcSpawnConfig Pmcs { get; set; } = new();

    /// <summary>Player Scav spawn wave configuration.</summary>
    [JsonProperty("player_scavs")]
    public PlayerScavSpawnConfig PlayerScavs { get; set; } = new();
}

/// <summary>Tweaks to EFT's wave-based spawn system timing.</summary>
public class EftNewSpawnSystemAdjustmentsConfig
{
    /// <summary>Seconds to delay non-wave spawns after being blocked.</summary>
    [JsonProperty("non_wave_retry_delay_after_blocked")]
    public int NonWaveRetryDelayAfterBlocked { get; set; }

    /// <summary>Time window (s) used for scav spawn rate calculations.</summary>
    [JsonProperty("scav_spawn_rate_time_window")]
    public int ScavSpawnRateTimeWindow { get; set; }
}

/// <summary>Bot population cap adjustments.</summary>
public class BotCapAdjustmentsConfig
{
    /// <summary>Replace SPT's bot caps with EFT's built-in values.</summary>
    [JsonProperty("use_EFT_bot_caps")]
    public bool UseEftBotCaps { get; set; }

    /// <summary>Only apply EFT caps when they are lower than SPT caps.</summary>
    [JsonProperty("only_decrease_bot_caps")]
    public bool OnlyDecreaseBotCaps { get; set; }

    /// <summary>Fixed integer adjustments applied per-map after cap resolution.</summary>
    [JsonProperty("map_specific_adjustments")]
    public Dictionary<string, int> MapSpecificAdjustments { get; set; } = new();
}

/// <summary>Limits on initial boss and rogue spawns at raid start.</summary>
public class LimitInitialBossSpawnsConfig
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    /// <summary>Remove the artificial delay before Rogues spawn on Lighthouse.</summary>
    [JsonProperty("disable_rogue_delay")]
    public bool DisableRogueDelay { get; set; }

    [JsonProperty("max_initial_bosses")]
    public int MaxInitialBosses { get; set; }

    [JsonProperty("max_initial_rogues")]
    public int MaxInitialRogues { get; set; }
}

/// <summary>PMC hostility behaviour overrides.</summary>
public class PmcHostilityAdjustmentsConfig
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    /// <summary>Force PMCs to always be hostile toward other PMCs.</summary>
    [JsonProperty("pmcs_always_hostile_against_pmcs")]
    public bool PmcsAlwaysHostileAgainstPmcs { get; set; }

    /// <summary>Force PMCs to always be hostile toward Scavs.</summary>
    [JsonProperty("pmcs_always_hostile_against_scavs")]
    public bool PmcsAlwaysHostileAgainstScavs { get; set; }

    /// <summary>Global chance (0-100) that Scavs treat PMCs as enemies.</summary>
    [JsonProperty("global_scav_enemy_chance")]
    public int GlobalScavEnemyChance { get; set; }

    /// <summary>Bot roles that PMCs should always treat as enemies.</summary>
    [JsonProperty("pmc_enemy_roles")]
    public List<string> PmcEnemyRoles { get; set; } = [];
}

/// <summary>PMC spawn wave configuration.</summary>
public class PmcSpawnConfig
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    /// <summary>Minimum remaining raid time (s) for PMC spawns to continue.</summary>
    [JsonProperty("min_raid_time_remaining")]
    public int MinRaidTimeRemaining { get; set; }

    /// <summary>Minimum spawn distance (m) from players at raid start.</summary>
    [JsonProperty("min_distance_from_players_initial")]
    public int MinDistanceFromPlayersInitial { get; set; }

    /// <summary>Minimum spawn distance (m) from players during the raid.</summary>
    [JsonProperty("min_distance_from_players_during_raid")]
    public int MinDistanceFromPlayersDuringRaid { get; set; }

    /// <summary>Factory-specific override for minimum spawn distance during raid.</summary>
    [JsonProperty("min_distance_from_players_during_raid_factory")]
    public int MinDistanceFromPlayersDuringRaidFactory { get; set; }

    /// <summary>
    /// 2D curve: <c>[[elapsed_fraction, player_fraction], ...]</c>.
    /// Maps raid elapsed-time fraction to fraction of max players that should be PMCs.
    /// </summary>
    [JsonProperty("fraction_of_max_players_vs_raidET")]
    public double[][] FractionOfMaxPlayersVsRaidET { get; set; } = [];

    /// <summary>
    /// 2D distribution: <c>[[group_size, weight], ...]</c>.
    /// Weights for how many bots spawn together in a PMC group.
    /// </summary>
    [JsonProperty("bots_per_group_distribution")]
    public double[][] BotsPerGroupDistribution { get; set; } = [];

    /// <summary>
    /// 2D distribution: <c>[[difficulty_level, weight], ...]</c>.
    /// Weights for PMC difficulty (as-online style).
    /// </summary>
    [JsonProperty("bot_difficulty_as_online")]
    public double[][] BotDifficultyAsOnline { get; set; } = [];
}

/// <summary>Player Scav spawn wave configuration.</summary>
public class PlayerScavSpawnConfig
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    [JsonProperty("min_raid_time_remaining")]
    public int MinRaidTimeRemaining { get; set; }

    [JsonProperty("min_distance_from_players_initial")]
    public int MinDistanceFromPlayersInitial { get; set; }

    [JsonProperty("min_distance_from_players_during_raid")]
    public int MinDistanceFromPlayersDuringRaid { get; set; }

    [JsonProperty("min_distance_from_players_during_raid_factory")]
    public int MinDistanceFromPlayersDuringRaidFactory { get; set; }

    /// <summary>Multiplier applied to max players to determine PScav count.</summary>
    [JsonProperty("fraction_of_max_players")]
    public double FractionOfMaxPlayers { get; set; }

    /// <summary>Seconds of randomness added to PScav spawn timing.</summary>
    [JsonProperty("time_randomness")]
    public int TimeRandomness { get; set; }

    /// <inheritdoc cref="PmcSpawnConfig.BotsPerGroupDistribution"/>
    [JsonProperty("bots_per_group_distribution")]
    public double[][] BotsPerGroupDistribution { get; set; } = [];

    /// <inheritdoc cref="PmcSpawnConfig.BotDifficultyAsOnline"/>
    [JsonProperty("bot_difficulty_as_online")]
    public double[][] BotDifficultyAsOnline { get; set; } = [];
}

// ────────────────────────────────────────────────────────────────────────
// PScav chance
// ────────────────────────────────────────────────────────────────────────

/// <summary>
/// Controls dynamic PScav conversion probability based on remaining raid time.
/// </summary>
public class AdjustPScavChanceConfig
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// 2D curve: <c>[[time_remaining_fraction, chance], ...]</c>.
    /// Maps remaining raid-time fraction to PScav conversion chance (0-100).
    /// </summary>
    [JsonProperty("chance_vs_time_remaining_fraction")]
    public double[][] ChanceVsTimeRemainingFraction { get; set; } = [];
}
