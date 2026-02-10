using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    /// <summary>
    /// Configuration for the head-look variance system — bots periodically glance
    /// to the side, toward combat events, or at squad members while moving.
    /// Makes movement look more natural and alert.
    /// </summary>
    public class LookVarianceConfig
    {
        /// <summary>Whether the look variance system is enabled.</summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>Minimum interval in seconds between flank checks (random head rotation).</summary>
        [JsonProperty("flank_check_interval_min")]
        public float FlankCheckIntervalMin { get; set; } = 5.0f;

        /// <summary>Maximum interval in seconds between flank checks.</summary>
        [JsonProperty("flank_check_interval_max")]
        public float FlankCheckIntervalMax { get; set; } = 15.0f;

        /// <summary>Minimum interval in seconds between POI/event glances.</summary>
        [JsonProperty("poi_glance_interval_min")]
        public float PoiGlanceIntervalMin { get; set; } = 8.0f;

        /// <summary>Maximum interval in seconds between POI/event glances.</summary>
        [JsonProperty("poi_glance_interval_max")]
        public float PoiGlanceIntervalMax { get; set; } = 20.0f;

        /// <summary>Maximum range in meters for detecting POIs to glance at.</summary>
        [JsonProperty("poi_detection_range")]
        public float PoiDetectionRange { get; set; } = 20.0f;

        /// <summary>Maximum range in meters for glancing at squad members.</summary>
        [JsonProperty("squad_glance_range")]
        public float SquadGlanceRange { get; set; } = 15.0f;

        /// <summary>Probability (0-1) of looking toward a nearby combat event when one exists.</summary>
        [JsonProperty("combat_event_look_chance")]
        public float CombatEventLookChance { get; set; } = 0.7f;

        public LookVarianceConfig() { }
    }
}
