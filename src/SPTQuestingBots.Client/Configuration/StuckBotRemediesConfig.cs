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
        public float MinTimeBeforeJumping { get; set; } = 3;

        [JsonProperty("jump_debounce_time")]
        public float JumpDebounceTime { get; set; } = 2;

        [JsonProperty("min_time_before_vaulting")]
        public float MinTimeBeforeVaulting { get; set; } = 1.5f;

        [JsonProperty("vault_debounce_time")]
        public float VaultDebounceTime { get; set; } = 2;

        public StuckBotRemediesConfig() { }
    }
}
