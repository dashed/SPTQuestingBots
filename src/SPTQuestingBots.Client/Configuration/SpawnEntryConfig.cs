using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    /// <summary>
    /// Configuration for the spawn entry system — bots pause briefly after spawning
    /// to check their surroundings before rushing to objectives. Controls duration,
    /// pose, squad stagger, and direction bias.
    /// </summary>
    public class SpawnEntryConfig
    {
        /// <summary>Whether the spawn entry system is enabled.</summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>Minimum base duration in seconds for the spawn entry pause.</summary>
        [JsonProperty("base_duration_min")]
        public float BaseDurationMin { get; set; } = 3.0f;

        /// <summary>Maximum base duration in seconds for the spawn entry pause.</summary>
        [JsonProperty("base_duration_max")]
        public float BaseDurationMax { get; set; } = 5.0f;

        /// <summary>Extra seconds added per squad follower index for staggered departure.</summary>
        [JsonProperty("squad_stagger_per_member")]
        public float SquadStaggerPerMember { get; set; } = 1.5f;

        /// <summary>Duration in seconds for the spawn facing direction bias to decay from 1.0 to 0.</summary>
        [JsonProperty("direction_bias_duration")]
        public float DirectionBiasDuration { get; set; } = 30.0f;

        /// <summary>Maximum bonus applied to objectives in the spawn facing direction.</summary>
        [JsonProperty("direction_bias_strength")]
        public float DirectionBiasStrength { get; set; } = 0.05f;

        /// <summary>Bot pose during spawn entry (0=crouch, 1=standing). 0.85 = alert but not crouched.</summary>
        [JsonProperty("pose")]
        public float Pose { get; set; } = 0.85f;

        public SpawnEntryConfig() { }
    }
}
