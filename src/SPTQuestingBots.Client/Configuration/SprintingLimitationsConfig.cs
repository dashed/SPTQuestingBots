using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    public class SprintingLimitationsConfig
    {
        [JsonProperty("enable_debounce_time")]
        public float EnableDebounceTime { get; set; } = 3;

        [JsonProperty("stamina")]
        public MinMaxConfig Stamina { get; set; } = new MinMaxConfig();

        [JsonProperty("sharp_path_corners")]
        public DistanceAngleConfig SharpPathCorners { get; set; } = new DistanceAngleConfig();

        [JsonProperty("approaching_closed_doors")]
        public DistanceAngleConfig ApproachingClosedDoors { get; set; } = new DistanceAngleConfig();

        [JsonProperty("post_combat_cooldown_seconds")]
        public float PostCombatCooldownSeconds { get; set; } = 20f;

        [JsonProperty("late_raid_no_sprint_threshold")]
        public float LateRaidNoSprintThreshold { get; set; } = 0.15f;

        [JsonProperty("enable_post_combat_sprint_block")]
        public bool EnablePostCombatSprintBlock { get; set; } = true;

        [JsonProperty("enable_late_raid_sprint_block")]
        public bool EnableLateRaidSprintBlock { get; set; } = true;

        [JsonProperty("enable_suspicion_sprint_block")]
        public bool EnableSuspicionSprintBlock { get; set; } = true;

        [JsonProperty("enable_door_sprint_pause")]
        public bool EnableDoorSprintPause { get; set; } = true;

        [JsonProperty("enable_stamina_exhaustion_sprint_block")]
        public bool EnableStaminaExhaustionSprintBlock { get; set; } = true;

        [JsonProperty("enable_physical_condition_sprint_block")]
        public bool EnablePhysicalConditionSprintBlock { get; set; } = true;

        [JsonProperty("enable_overweight_sprint_block")]
        public bool EnableOverweightSprintBlock { get; set; } = true;

        public SprintingLimitationsConfig() { }
    }
}
