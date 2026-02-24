using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    public class StuckBotRemediesConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("min_time_before_jumping")]
        public float MinTimeBeforeJumping { get; set; } = 6;

        [JsonProperty("jump_debounce_time")]
        public float JumpDebounceTime { get; set; } = 4;

        [JsonProperty("min_time_before_vaulting")]
        public float MinTimeBeforeVaulting { get; set; } = 8;

        [JsonProperty("vault_debounce_time")]
        public float VaultDebounceTime { get; set; } = 4;

        [JsonProperty("soft_stuck_fail_delay")]
        public float SoftStuckFailDelay { get; set; } = 6;

        [JsonProperty("hard_stuck_path_retry_delay")]
        public float HardStuckPathRetryDelay { get; set; } = 5;

        [JsonProperty("hard_stuck_teleport_delay")]
        public float HardStuckTeleportDelay { get; set; } = 10;

        [JsonProperty("hard_stuck_fail_delay")]
        public float HardStuckFailDelay { get; set; } = 15;

        [JsonProperty("teleport_enabled")]
        public bool TeleportEnabled { get; set; } = true;

        [JsonProperty("teleport_max_player_distance")]
        public float TeleportMaxPlayerDistance { get; set; } = 10;

        public StuckBotRemediesConfig() { }
    }
}
