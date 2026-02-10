using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    /// <summary>
    /// Configuration for the vulture system — bots that hear gunfire and move to
    /// ambush weakened survivors. Controls detection, approach behavior, ambush timing,
    /// boss avoidance, and per-role enablement.
    /// </summary>
    public class VultureConfig
    {
        /// <summary>Whether the vulture system is enabled.</summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>Base detection range in meters for hearing combat events.</summary>
        [JsonProperty("base_detection_range")]
        public float BaseDetectionRange { get; set; } = 150.0f;

        /// <summary>Detection range multiplier during nighttime (0.0-1.0).</summary>
        [JsonProperty("night_range_multiplier")]
        public float NightRangeMultiplier { get; set; } = 0.65f;

        /// <summary>Whether to apply time-of-day modifiers to detection range.</summary>
        [JsonProperty("enable_time_of_day")]
        public bool EnableTimeOfDay { get; set; } = true;

        /// <summary>Bonus intensity added per multi-shot burst.</summary>
        [JsonProperty("multi_shot_intensity_bonus")]
        public int MultiShotIntensityBonus { get; set; } = 5;

        /// <summary>Time window in seconds for counting combat intensity around an event.</summary>
        [JsonProperty("intensity_window")]
        public float IntensityWindow { get; set; } = 15.0f;

        /// <summary>Minimum combat intensity required for a bot to trigger vulture behavior.</summary>
        [JsonProperty("courage_threshold")]
        public int CourageThreshold { get; set; } = 15;

        /// <summary>Maximum time in seconds to hold at ambush position before rushing.</summary>
        [JsonProperty("ambush_duration")]
        public float AmbushDuration { get; set; } = 90.0f;

        /// <summary>Minimum distance in meters to stop during rush phase (arrival threshold).</summary>
        [JsonProperty("ambush_distance_min")]
        public float AmbushDistanceMin { get; set; } = 25.0f;

        /// <summary>Distance in meters to transition from silent approach to hold ambush.</summary>
        [JsonProperty("ambush_distance_max")]
        public float AmbushDistanceMax { get; set; } = 30.0f;

        /// <summary>Seconds of silence (zero intensity) before triggering early rush.</summary>
        [JsonProperty("silence_trigger_duration")]
        public float SilenceTriggerDuration { get; set; } = 45.0f;

        /// <summary>Whether bots will loot bodies after completing vulture behavior.</summary>
        [JsonProperty("enable_greed")]
        public bool EnableGreed { get; set; } = true;

        /// <summary>Whether bots slow down and crouch when close to the target.</summary>
        [JsonProperty("enable_silent_approach")]
        public bool EnableSilentApproach { get; set; } = true;

        /// <summary>Distance in meters at which the bot transitions to silent approach.</summary>
        [JsonProperty("silent_approach_distance")]
        public float SilentApproachDistance { get; set; } = 35.0f;

        /// <summary>Whether bots turn off flashlights during silent approach.</summary>
        [JsonProperty("enable_flashlight_discipline")]
        public bool EnableFlashlightDiscipline { get; set; } = true;

        /// <summary>Whether bots periodically look around during ambush (paranoia behavior).</summary>
        [JsonProperty("enable_paranoia")]
        public bool EnableParanoia { get; set; } = true;

        /// <summary>Minimum interval in seconds between paranoia look-arounds.</summary>
        [JsonProperty("paranoia_interval_min")]
        public float ParanoiaIntervalMin { get; set; } = 3.0f;

        /// <summary>Maximum interval in seconds between paranoia look-arounds.</summary>
        [JsonProperty("paranoia_interval_max")]
        public float ParanoiaIntervalMax { get; set; } = 6.0f;

        /// <summary>Maximum angle in degrees for paranoia look direction from forward.</summary>
        [JsonProperty("paranoia_angle_range")]
        public float ParanoiaAngleRange { get; set; } = 45.0f;

        /// <summary>Whether bots may fire shots to bait enemies during ambush.</summary>
        [JsonProperty("enable_baiting")]
        public bool EnableBaiting { get; set; } = true;

        /// <summary>Percentage chance (0-100) of baiting behavior per ambush.</summary>
        [JsonProperty("baiting_chance")]
        public int BaitingChance { get; set; } = 25;

        /// <summary>Whether bots avoid areas with recent boss activity.</summary>
        [JsonProperty("enable_boss_avoidance")]
        public bool EnableBossAvoidance { get; set; } = true;

        /// <summary>Radius in meters around boss events to avoid.</summary>
        [JsonProperty("boss_avoidance_radius")]
        public float BossAvoidanceRadius { get; set; } = 75.0f;

        /// <summary>Time in seconds before boss zone markers expire.</summary>
        [JsonProperty("boss_zone_decay")]
        public float BossZoneDecay { get; set; } = 120.0f;

        /// <summary>Whether bots vulture toward airdrop landing positions.</summary>
        [JsonProperty("enable_airdrop_vulturing")]
        public bool EnableAirdropVulturing { get; set; } = true;

        /// <summary>Whether squad members coordinate during vulture behavior.</summary>
        [JsonProperty("enable_squad_vulturing")]
        public bool EnableSquadVulturing { get; set; } = true;

        /// <summary>Whether PMC bots can use vulture behavior.</summary>
        [JsonProperty("enable_for_pmcs")]
        public bool EnableForPmcs { get; set; } = true;

        /// <summary>Whether regular scav bots can use vulture behavior.</summary>
        [JsonProperty("enable_for_scavs")]
        public bool EnableForScavs { get; set; } = false;

        /// <summary>Whether player scav bots can use vulture behavior.</summary>
        [JsonProperty("enable_for_pscavs")]
        public bool EnableForPscavs { get; set; } = false;

        /// <summary>Whether raider bots can use vulture behavior.</summary>
        [JsonProperty("enable_for_raiders")]
        public bool EnableForRaiders { get; set; } = false;

        /// <summary>Maximum age in seconds for combat events to be considered.</summary>
        [JsonProperty("max_event_age")]
        public float MaxEventAge { get; set; } = 300.0f;

        /// <summary>Ring buffer size for storing combat events.</summary>
        [JsonProperty("event_buffer_size")]
        public int EventBufferSize { get; set; } = 128;

        /// <summary>Cooldown in seconds after rejecting vulture behavior before re-evaluation.</summary>
        [JsonProperty("cooldown_on_reject")]
        public float CooldownOnReject { get; set; } = 180.0f;

        /// <summary>Maximum time in seconds for vulture movement before timeout.</summary>
        [JsonProperty("movement_timeout")]
        public float MovementTimeout { get; set; } = 90.0f;

        public VultureConfig() { }
    }
}
