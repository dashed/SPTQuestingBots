using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    public class WeaponReadinessConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("skip_questing_on_malfunction")]
        public bool SkipQuestingOnMalfunction { get; set; } = true;

        [JsonProperty("switch_to_single_fire_for_ambush")]
        public bool SwitchToSingleFireForAmbush { get; set; } = true;

        public WeaponReadinessConfig() { }
    }
}
