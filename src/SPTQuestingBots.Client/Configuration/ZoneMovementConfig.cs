using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    public class ZoneMovementConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("target_cell_count")]
        public int TargetCellCount { get; set; } = 150;

        [JsonProperty("convergence_update_interval_sec")]
        public float ConvergenceUpdateIntervalSec { get; set; } = 30f;

        [JsonProperty("convergence_weight")]
        public float ConvergenceWeight { get; set; } = 1.0f;

        [JsonProperty("advection_weight")]
        public float AdvectionWeight { get; set; } = 0.5f;

        [JsonProperty("momentum_weight")]
        public float MomentumWeight { get; set; } = 0.5f;

        [JsonProperty("noise_weight")]
        public float NoiseWeight { get; set; } = 0.3f;

        [JsonProperty("poi_score_weight")]
        public float PoiScoreWeight { get; set; } = 0.3f;

        [JsonProperty("crowd_repulsion_strength")]
        public float CrowdRepulsionStrength { get; set; } = 2.0f;

        [JsonProperty("bounds_padding")]
        public float BoundsPadding { get; set; } = 50f;

        [JsonProperty("quest_desirability")]
        public float QuestDesirability { get; set; } = 5f;

        [JsonProperty("quest_name")]
        public string QuestName { get; set; } = "Zone Movement";

        public ZoneMovementConfig() { }
    }
}
