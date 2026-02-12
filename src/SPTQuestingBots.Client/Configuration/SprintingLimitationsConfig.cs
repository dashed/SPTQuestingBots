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
        public float EnableDebounceTime { get; set; } = 1;

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

        public SprintingLimitationsConfig() { }
    }
}
