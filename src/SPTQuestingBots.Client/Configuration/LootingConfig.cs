using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    public class LootingConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("detect_container_distance")]
        public float DetectContainerDistance { get; set; } = 60f;

        [JsonProperty("detect_item_distance")]
        public float DetectItemDistance { get; set; } = 40f;

        [JsonProperty("detect_corpse_distance")]
        public float DetectCorpseDistance { get; set; } = 50f;

        [JsonProperty("scan_interval_seconds")]
        public float ScanIntervalSeconds { get; set; } = 5f;

        [JsonProperty("min_item_value")]
        public int MinItemValue { get; set; } = 5000;

        [JsonProperty("max_concurrent_looters")]
        public int MaxConcurrentLooters { get; set; } = 5;

        [JsonProperty("loot_during_combat")]
        public bool LootDuringCombat { get; set; } = false;

        [JsonProperty("container_looting_enabled")]
        public bool ContainerLootingEnabled { get; set; } = true;

        [JsonProperty("loose_item_looting_enabled")]
        public bool LooseItemLootingEnabled { get; set; } = true;

        [JsonProperty("corpse_looting_enabled")]
        public bool CorpseLootingEnabled { get; set; } = true;

        [JsonProperty("gear_swap_enabled")]
        public bool GearSwapEnabled { get; set; } = true;

        [JsonProperty("squad_loot_coordination")]
        public bool SquadLootCoordination { get; set; } = true;

        [JsonProperty("disable_when_lootingbots_detected")]
        public bool DisableWhenLootingBotsDetected { get; set; } = true;

        [JsonProperty("approach_distance")]
        public float ApproachDistance { get; set; } = 0.85f;

        [JsonProperty("approach_y_tolerance")]
        public float ApproachYTolerance { get; set; } = 0.5f;

        [JsonProperty("max_looting_time_seconds")]
        public float MaxLootingTimeSeconds { get; set; } = 30f;

        [JsonProperty("loot_cooldown_seconds")]
        public float LootCooldownSeconds { get; set; } = 15f;

        [JsonProperty("value_score_cap")]
        public float ValueScoreCap { get; set; } = 50000f;

        [JsonProperty("distance_penalty_factor")]
        public float DistancePenaltyFactor { get; set; } = 0.001f;

        [JsonProperty("quest_proximity_bonus")]
        public float QuestProximityBonus { get; set; } = 0.15f;

        [JsonProperty("gear_upgrade_score_bonus")]
        public float GearUpgradeScoreBonus { get; set; } = 0.3f;

        public LootingConfig() { }
    }
}
