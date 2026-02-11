using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration;

/// <summary>
/// Configuration for the dynamic objective system — generates quests from live
/// game state including firefight clusters, corpse locations, and building clears.
/// </summary>
public class DynamicObjectiveConfig
{
    /// <summary>Whether the dynamic objective system is enabled.</summary>
    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Interval in seconds between scans for new dynamic objectives.</summary>
    [JsonProperty("scan_interval_sec")]
    public float ScanIntervalSec { get; set; } = 30f;

    /// <summary>Maximum number of active dynamic quests at any time.</summary>
    [JsonProperty("max_active_quests")]
    public int MaxActiveQuests { get; set; } = 10;

    /// <summary>Whether firefight investigation objectives are enabled.</summary>
    [JsonProperty("firefight_enabled")]
    public bool FirefightEnabled { get; set; } = true;

    /// <summary>Minimum combat intensity to trigger a firefight objective.</summary>
    [JsonProperty("firefight_min_intensity")]
    public int FirefightMinIntensity { get; set; } = 20;

    /// <summary>Desirability score for firefight investigation quests.</summary>
    [JsonProperty("firefight_desirability")]
    public float FirefightDesirability { get; set; } = 8f;

    /// <summary>Maximum age in seconds for combat events to form firefight objectives.</summary>
    [JsonProperty("firefight_max_age_sec")]
    public float FirefightMaxAgeSec { get; set; } = 120f;

    /// <summary>Radius in meters for clustering nearby combat events.</summary>
    [JsonProperty("firefight_cluster_radius")]
    public float FirefightClusterRadius { get; set; } = 50f;

    /// <summary>Whether corpse scavenging objectives are enabled.</summary>
    [JsonProperty("corpse_enabled")]
    public bool CorpseEnabled { get; set; } = true;

    /// <summary>Desirability score for corpse scavenging quests.</summary>
    [JsonProperty("corpse_desirability")]
    public float CorpseDesirability { get; set; } = 6f;

    /// <summary>Maximum age in seconds for death events to form corpse objectives.</summary>
    [JsonProperty("corpse_max_age_sec")]
    public float CorpseMaxAgeSec { get; set; } = 180f;

    /// <summary>Whether building clear objectives are enabled.</summary>
    [JsonProperty("building_clear_enabled")]
    public bool BuildingClearEnabled { get; set; } = true;

    /// <summary>Desirability score for building clear quests.</summary>
    [JsonProperty("building_clear_desirability")]
    public float BuildingClearDesirability { get; set; } = 4f;

    /// <summary>Minimum hold time in seconds for building clear objectives.</summary>
    [JsonProperty("building_clear_hold_min")]
    public float BuildingClearHoldMin { get; set; } = 15f;

    /// <summary>Maximum hold time in seconds for building clear objectives.</summary>
    [JsonProperty("building_clear_hold_max")]
    public float BuildingClearHoldMax { get; set; } = 30f;

    public DynamicObjectiveConfig() { }
}
