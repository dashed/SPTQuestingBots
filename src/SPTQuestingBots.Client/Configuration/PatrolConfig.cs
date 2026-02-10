using System.Collections.Generic;
using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    /// <summary>
    /// JSON POCO for a single route entry in the per-map config override.
    /// Used for deserialization of routes_per_map values.
    /// </summary>
    public class PatrolRouteEntry
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("type")]
        public string Type { get; set; } = "Perimeter";

        [JsonProperty("waypoints")]
        public PatrolWaypointEntry[] Waypoints { get; set; } = System.Array.Empty<PatrolWaypointEntry>();

        [JsonProperty("min_aggression")]
        public float MinAggression { get; set; } = 0f;

        [JsonProperty("max_aggression")]
        public float MaxAggression { get; set; } = 1f;

        [JsonProperty("min_raid_time")]
        public float MinRaidTime { get; set; } = 0f;

        [JsonProperty("max_raid_time")]
        public float MaxRaidTime { get; set; } = 1f;

        [JsonProperty("is_loop")]
        public bool IsLoop { get; set; } = true;

        public PatrolRouteEntry() { }
    }

    /// <summary>
    /// JSON POCO for a single waypoint entry within a route override.
    /// </summary>
    public class PatrolWaypointEntry
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("z")]
        public float Z { get; set; }

        [JsonProperty("pause_min")]
        public float PauseMin { get; set; } = 2f;

        [JsonProperty("pause_max")]
        public float PauseMax { get; set; } = 5f;

        public PatrolWaypointEntry() { }
    }

    /// <summary>
    /// Configuration for the patrol route system — bots follow structured patrol
    /// paths between quest objectives. Controls scoring, cooldown, movement params,
    /// per-role enablement, and per-map route overrides.
    /// </summary>
    public class PatrolConfig
    {
        /// <summary>Whether the patrol system is enabled.</summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>Base utility score for patrol behavior.</summary>
        [JsonProperty("base_score")]
        public float BaseScore { get; set; } = 0.50f;

        /// <summary>Cooldown in seconds after completing a patrol route.</summary>
        [JsonProperty("cooldown_sec")]
        public float CooldownSec { get; set; } = 120f;

        /// <summary>Distance in meters at which a waypoint is considered reached.</summary>
        [JsonProperty("waypoint_arrival_radius")]
        public float WaypointArrivalRadius { get; set; } = 3f;

        /// <summary>Bot pose while patrolling (0=crouch, 1=standing).</summary>
        [JsonProperty("pose")]
        public float Pose { get; set; } = 0.85f;

        /// <summary>Whether PMC bots can patrol.</summary>
        [JsonProperty("enable_for_pmcs")]
        public bool EnableForPmcs { get; set; } = true;

        /// <summary>Whether regular scav bots can patrol.</summary>
        [JsonProperty("enable_for_scavs")]
        public bool EnableForScavs { get; set; } = true;

        /// <summary>Whether player scav bots can patrol.</summary>
        [JsonProperty("enable_for_pscavs")]
        public bool EnableForPscavs { get; set; } = true;

        /// <summary>Per-map route overrides. Keys are BSG map IDs (e.g. "bigmap").</summary>
        [JsonProperty("routes_per_map")]
        public Dictionary<string, PatrolRouteEntry[]> RoutesPerMap { get; set; } = new Dictionary<string, PatrolRouteEntry[]>();

        public PatrolConfig() { }
    }
}
