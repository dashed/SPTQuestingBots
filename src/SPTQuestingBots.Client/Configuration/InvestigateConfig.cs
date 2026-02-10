using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    /// <summary>
    /// Configuration for the investigate system — bots hear nearby gunfire and
    /// cautiously approach the location to check it out. Lighter-weight than
    /// vulture behavior: lower intensity threshold, shorter duration, no ambush phases.
    /// </summary>
    public class InvestigateConfig
    {
        /// <summary>Whether the investigate system is enabled.</summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>Base utility score for investigate behavior.</summary>
        [JsonProperty("base_score")]
        public float BaseScore { get; set; } = 0.40f;

        /// <summary>Minimum combat intensity to trigger investigation (lower than vulture).</summary>
        [JsonProperty("intensity_threshold")]
        public int IntensityThreshold { get; set; } = 5;

        /// <summary>Detection range in meters for hearing combat events.</summary>
        [JsonProperty("detection_range")]
        public float DetectionRange { get; set; } = 120.0f;

        /// <summary>Maximum time in seconds for the entire investigation before timeout.</summary>
        [JsonProperty("movement_timeout")]
        public float MovementTimeout { get; set; } = 45.0f;

        /// <summary>Movement speed during approach (0-1, where 1 is full speed).</summary>
        [JsonProperty("approach_speed")]
        public float ApproachSpeed { get; set; } = 0.5f;

        /// <summary>Bot pose during approach (0=crouch, 1=standing). 0.6 = cautious crouch.</summary>
        [JsonProperty("approach_pose")]
        public float ApproachPose { get; set; } = 0.6f;

        /// <summary>Distance in meters at which the bot considers itself arrived at the event location.</summary>
        [JsonProperty("arrival_distance")]
        public float ArrivalDistance { get; set; } = 15.0f;

        /// <summary>How long in seconds to look around after arriving at the event location.</summary>
        [JsonProperty("look_around_duration")]
        public float LookAroundDuration { get; set; } = 8.0f;

        /// <summary>Minimum interval in seconds between random head scans while looking around.</summary>
        [JsonProperty("head_scan_interval_min")]
        public float HeadScanIntervalMin { get; set; } = 2.0f;

        /// <summary>Maximum interval in seconds between random head scans while looking around.</summary>
        [JsonProperty("head_scan_interval_max")]
        public float HeadScanIntervalMax { get; set; } = 5.0f;

        /// <summary>Whether PMC bots can investigate gunfire.</summary>
        [JsonProperty("enable_for_pmcs")]
        public bool EnableForPmcs { get; set; } = true;

        /// <summary>Whether regular scav bots can investigate gunfire.</summary>
        [JsonProperty("enable_for_scavs")]
        public bool EnableForScavs { get; set; } = true;

        /// <summary>Whether player scav bots can investigate gunfire.</summary>
        [JsonProperty("enable_for_pscavs")]
        public bool EnableForPscavs { get; set; } = false;

        public InvestigateConfig() { }
    }
}
