using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    /// <summary>
    /// Configuration for the zone movement system — grid partitioning, vector field weights,
    /// and destination selection parameters that control how bots navigate between map zones.
    /// </summary>
    public class ZoneMovementConfig
    {
        /// <summary>Whether the zone movement system is enabled.</summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>Target number of grid cells. Actual count may differ due to rounding.</summary>
        [JsonProperty("target_cell_count")]
        public int TargetCellCount { get; set; } = 150;

        /// <summary>Minimum seconds between convergence field recomputations.</summary>
        [JsonProperty("convergence_update_interval_sec")]
        public float ConvergenceUpdateIntervalSec { get; set; } = 30f;

        /// <summary>Weight for the convergence (player attraction) field component.</summary>
        [JsonProperty("convergence_weight")]
        public float ConvergenceWeight { get; set; } = 1.0f;

        /// <summary>Weight for the advection (zone attraction + crowd repulsion) field component.</summary>
        [JsonProperty("advection_weight")]
        public float AdvectionWeight { get; set; } = 0.5f;

        /// <summary>Weight for the momentum (travel direction smoothing) field component.</summary>
        [JsonProperty("momentum_weight")]
        public float MomentumWeight { get; set; } = 0.5f;

        /// <summary>Weight for the noise (random rotation) field component.</summary>
        [JsonProperty("noise_weight")]
        public float NoiseWeight { get; set; } = 0.3f;

        /// <summary>Blend weight for POI density in cell scoring (0.0 = angle only, 1.0 = density only).</summary>
        [JsonProperty("poi_score_weight")]
        public float PoiScoreWeight { get; set; } = 0.3f;

        /// <summary>Multiplier for crowd repulsion force between bots.</summary>
        [JsonProperty("crowd_repulsion_strength")]
        public float CrowdRepulsionStrength { get; set; } = 2.0f;

        /// <summary>Padding in meters added to detected map bounds on each edge.</summary>
        [JsonProperty("bounds_padding")]
        public float BoundsPadding { get; set; } = 50f;

        /// <summary>Desirability score for the zone movement quest in quest selection.</summary>
        [JsonProperty("quest_desirability")]
        public float QuestDesirability { get; set; } = 5f;

        /// <summary>Display name for the zone movement quest.</summary>
        [JsonProperty("quest_name")]
        public string QuestName { get; set; } = "Zone Movement";

        public ZoneMovementConfig() { }
    }
}
