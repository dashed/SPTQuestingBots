using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    public class VultureConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("base_detection_range")]
        public float BaseDetectionRange { get; set; } = 150.0f;

        [JsonProperty("night_range_multiplier")]
        public float NightRangeMultiplier { get; set; } = 0.65f;

        [JsonProperty("enable_time_of_day")]
        public bool EnableTimeOfDay { get; set; } = true;

        [JsonProperty("multi_shot_intensity_bonus")]
        public int MultiShotIntensityBonus { get; set; } = 5;

        [JsonProperty("intensity_window")]
        public float IntensityWindow { get; set; } = 15.0f;

        [JsonProperty("courage_threshold")]
        public int CourageThreshold { get; set; } = 15;

        [JsonProperty("ambush_duration")]
        public float AmbushDuration { get; set; } = 90.0f;

        [JsonProperty("ambush_distance_min")]
        public float AmbushDistanceMin { get; set; } = 25.0f;

        [JsonProperty("ambush_distance_max")]
        public float AmbushDistanceMax { get; set; } = 30.0f;

        [JsonProperty("silence_trigger_duration")]
        public float SilenceTriggerDuration { get; set; } = 45.0f;

        [JsonProperty("enable_greed")]
        public bool EnableGreed { get; set; } = true;

        [JsonProperty("enable_silent_approach")]
        public bool EnableSilentApproach { get; set; } = true;

        [JsonProperty("silent_approach_distance")]
        public float SilentApproachDistance { get; set; } = 35.0f;

        [JsonProperty("enable_flashlight_discipline")]
        public bool EnableFlashlightDiscipline { get; set; } = true;

        [JsonProperty("enable_paranoia")]
        public bool EnableParanoia { get; set; } = true;

        [JsonProperty("paranoia_interval_min")]
        public float ParanoiaIntervalMin { get; set; } = 3.0f;

        [JsonProperty("paranoia_interval_max")]
        public float ParanoiaIntervalMax { get; set; } = 6.0f;

        [JsonProperty("paranoia_angle_range")]
        public float ParanoiaAngleRange { get; set; } = 45.0f;

        [JsonProperty("enable_baiting")]
        public bool EnableBaiting { get; set; } = true;

        [JsonProperty("baiting_chance")]
        public int BaitingChance { get; set; } = 25;

        [JsonProperty("enable_boss_avoidance")]
        public bool EnableBossAvoidance { get; set; } = true;

        [JsonProperty("boss_avoidance_radius")]
        public float BossAvoidanceRadius { get; set; } = 75.0f;

        [JsonProperty("boss_zone_decay")]
        public float BossZoneDecay { get; set; } = 120.0f;

        [JsonProperty("enable_airdrop_vulturing")]
        public bool EnableAirdropVulturing { get; set; } = true;

        [JsonProperty("enable_squad_vulturing")]
        public bool EnableSquadVulturing { get; set; } = true;

        [JsonProperty("enable_for_pmcs")]
        public bool EnableForPmcs { get; set; } = true;

        [JsonProperty("enable_for_scavs")]
        public bool EnableForScavs { get; set; } = false;

        [JsonProperty("enable_for_pscavs")]
        public bool EnableForPscavs { get; set; } = false;

        [JsonProperty("enable_for_raiders")]
        public bool EnableForRaiders { get; set; } = false;

        [JsonProperty("max_event_age")]
        public float MaxEventAge { get; set; } = 300.0f;

        [JsonProperty("event_buffer_size")]
        public int EventBufferSize { get; set; } = 128;

        [JsonProperty("cooldown_on_reject")]
        public float CooldownOnReject { get; set; } = 180.0f;

        [JsonProperty("movement_timeout")]
        public float MovementTimeout { get; set; } = 90.0f;

        public VultureConfig() { }
    }
}
