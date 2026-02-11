using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration;

/// <summary>
/// Configuration for the hybrid looting system.
/// Controls detection ranges, scoring parameters, and squad coordination behavior.
/// </summary>
public class LootingConfig
{
    /// <summary>Master toggle for the entire looting system. When false, bots will not loot at all.</summary>
    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum distance (meters) at which bots can detect lootable containers.</summary>
    [JsonProperty("detect_container_distance")]
    public float DetectContainerDistance { get; set; } = 60f;

    /// <summary>Maximum distance (meters) at which bots can detect loose items on the ground.</summary>
    [JsonProperty("detect_item_distance")]
    public float DetectItemDistance { get; set; } = 40f;

    /// <summary>Maximum distance (meters) at which bots can detect lootable corpses.</summary>
    [JsonProperty("detect_corpse_distance")]
    public float DetectCorpseDistance { get; set; } = 50f;

    /// <summary>How often (seconds) bots perform a Physics.OverlapSphere scan for nearby loot.</summary>
    [JsonProperty("scan_interval_seconds")]
    public float ScanIntervalSeconds { get; set; } = 5f;

    /// <summary>Minimum flea-market value (roubles) for a non-gear item to be considered worth looting.</summary>
    [JsonProperty("min_item_value")]
    public int MinItemValue { get; set; } = 5000;

    /// <summary>Maximum number of bots that can be actively looting at the same time.</summary>
    [JsonProperty("max_concurrent_looters")]
    public int MaxConcurrentLooters { get; set; } = 5;

    /// <summary>Whether bots are allowed to loot while engaged in combat. Usually false for realism.</summary>
    [JsonProperty("loot_during_combat")]
    public bool LootDuringCombat { get; set; } = false;

    /// <summary>Whether bots will open and search lootable containers (weapon boxes, crates, etc.).</summary>
    [JsonProperty("container_looting_enabled")]
    public bool ContainerLootingEnabled { get; set; } = true;

    /// <summary>Whether bots will pick up loose items found on the ground.</summary>
    [JsonProperty("loose_item_looting_enabled")]
    public bool LooseItemLootingEnabled { get; set; } = true;

    /// <summary>Whether bots will search corpses for loot.</summary>
    [JsonProperty("corpse_looting_enabled")]
    public bool CorpseLootingEnabled { get; set; } = true;

    /// <summary>Whether bots can swap their current gear for better gear found as loot (armor, weapons, rigs, backpacks).</summary>
    [JsonProperty("gear_swap_enabled")]
    public bool GearSwapEnabled { get; set; } = true;

    /// <summary>Whether squad members coordinate looting through boss priority claims and shared scan results.</summary>
    [JsonProperty("squad_loot_coordination")]
    public bool SquadLootCoordination { get; set; } = true;

    /// <summary>When true, disables this looting system if the LootingBots mod is detected to avoid conflicts.</summary>
    [JsonProperty("disable_when_lootingbots_detected")]
    public bool DisableWhenLootingBotsDetected { get; set; } = true;

    /// <summary>How close (meters, XZ plane) a bot must get to a loot target before starting interaction.</summary>
    [JsonProperty("approach_distance")]
    public float ApproachDistance { get; set; } = 0.85f;

    /// <summary>Vertical tolerance (meters) for arrival check when approaching a loot target.</summary>
    [JsonProperty("approach_y_tolerance")]
    public float ApproachYTolerance { get; set; } = 0.5f;

    /// <summary>Maximum time (seconds) a bot will spend on a single looting action before timing out.</summary>
    [JsonProperty("max_looting_time_seconds")]
    public float MaxLootingTimeSeconds { get; set; } = 30f;

    /// <summary>Cooldown (seconds) after finishing one loot action before the bot will consider looting again.</summary>
    [JsonProperty("loot_cooldown_seconds")]
    public float LootCooldownSeconds { get; set; } = 15f;

    /// <summary>Value cap (roubles) for normalizing item value into 0-1 score range. Items at or above this value get max score.</summary>
    [JsonProperty("value_score_cap")]
    public float ValueScoreCap { get; set; } = 50000f;

    /// <summary>Multiplier applied to squared distance to penalize farther loot targets in scoring. Higher = stronger penalty.</summary>
    [JsonProperty("distance_penalty_factor")]
    public float DistancePenaltyFactor { get; set; } = 0.001f;

    /// <summary>Score bonus added when loot is near the bot's current quest objective (within 20m).</summary>
    [JsonProperty("quest_proximity_bonus")]
    public float QuestProximityBonus { get; set; } = 0.15f;

    /// <summary>Score bonus added when loot represents a gear upgrade (better armor, weapon, rig, or backpack).</summary>
    [JsonProperty("gear_upgrade_score_bonus")]
    public float GearUpgradeScoreBonus { get; set; } = 0.3f;

    public LootingConfig() { }
}
