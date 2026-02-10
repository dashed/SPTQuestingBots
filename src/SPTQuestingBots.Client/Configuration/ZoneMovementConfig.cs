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

        /// <summary>
        /// Per-map convergence overrides. Keys are BSG location IDs (case-insensitive).
        /// Each entry specifies radius, force, and enabled flag.
        /// Maps not listed here use built-in defaults from <see cref="ZoneMovement.Core.ConvergenceMapConfig"/>.
        /// </summary>
        [JsonProperty("convergence_per_map")]
        public Dictionary<string, ConvergenceMapEntry> ConvergencePerMap { get; set; } = new Dictionary<string, ConvergenceMapEntry>();

        /// <summary>Time (seconds) over which combat event convergence pull linearly decays to zero.</summary>
        [JsonProperty("combat_convergence_decay_sec")]
        public float CombatConvergenceDecaySec { get; set; } = 30f;

        /// <summary>Maximum range (meters) for combat events to contribute to convergence pull.</summary>
        [JsonProperty("combat_convergence_radius")]
        public float CombatConvergenceRadius { get; set; } = 300f;

        /// <summary>Force multiplier for combat event convergence pull (relative to player pull).</summary>
        [JsonProperty("combat_convergence_force")]
        public float CombatConvergenceForce { get; set; } = 0.5f;

        /// <summary>
        /// Per-map advection zone overrides. Keys are BSG location IDs (case-insensitive).
        /// Maps not listed here use built-in defaults from <see cref="ZoneMovement.Core.AdvectionZoneConfig"/>.
        /// </summary>
        [JsonProperty("advection_zones_per_map")]
        public Dictionary<string, AdvectionMapZonesEntry> AdvectionZonesPerMap { get; set; } =
            new Dictionary<string, AdvectionMapZonesEntry>();

        public ZoneMovementConfig() { }
    }

    /// <summary>
    /// JSON-serializable per-map convergence entry for config.json.
    /// </summary>
    public class ConvergenceMapEntry
    {
        [JsonProperty("radius")]
        public float Radius { get; set; }

        [JsonProperty("force")]
        public float Force { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        public ConvergenceMapEntry() { }
    }

    /// <summary>
    /// JSON-serializable per-map advection zone overrides for config.json.
    /// </summary>
    public class AdvectionMapZonesEntry
    {
        [JsonProperty("builtin_zones")]
        public Dictionary<string, AdvectionZoneEntryConfig> BuiltinZones { get; set; } = new Dictionary<string, AdvectionZoneEntryConfig>();

        [JsonProperty("custom_zones")]
        public List<CustomZoneEntryConfig> CustomZones { get; set; } = new List<CustomZoneEntryConfig>();

        public AdvectionMapZonesEntry() { }
    }

    /// <summary>
    /// JSON-serializable zone entry with force range, radius, decay, and time/boss multipliers.
    /// </summary>
    public class AdvectionZoneEntryConfig
    {
        [JsonProperty("force_min")]
        public float ForceMin { get; set; }

        [JsonProperty("force_max")]
        public float ForceMax { get; set; }

        [JsonProperty("radius")]
        public float Radius { get; set; }

        [JsonProperty("decay")]
        public float Decay { get; set; } = 1.0f;

        [JsonProperty("early_multiplier")]
        public float EarlyMultiplier { get; set; } = 1.0f;

        [JsonProperty("late_multiplier")]
        public float LateMultiplier { get; set; } = 1.0f;

        [JsonProperty("boss_alive_multiplier")]
        public float BossAliveMultiplier { get; set; } = 1.0f;

        public AdvectionZoneEntryConfig() { }
    }

    /// <summary>
    /// JSON-serializable custom zone entry with position.
    /// </summary>
    public class CustomZoneEntryConfig : AdvectionZoneEntryConfig
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("z")]
        public float Z { get; set; }

        public CustomZoneEntryConfig() { }
    }
}
