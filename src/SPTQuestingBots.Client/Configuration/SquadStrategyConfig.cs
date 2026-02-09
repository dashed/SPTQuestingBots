using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    public class SquadStrategyConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("guard_distance")]
        public float GuardDistance { get; set; } = 8f;

        [JsonProperty("flank_distance")]
        public float FlankDistance { get; set; } = 15f;

        [JsonProperty("overwatch_distance")]
        public float OverwatchDistance { get; set; } = 25f;

        [JsonProperty("escort_distance")]
        public float EscortDistance { get; set; } = 5f;

        [JsonProperty("arrival_radius")]
        public float ArrivalRadius { get; set; } = 3f;

        [JsonProperty("max_distance_from_boss")]
        public float MaxDistanceFromBoss { get; set; } = 75f;

        [JsonProperty("strategy_pacing_seconds")]
        public float StrategyPacingSeconds { get; set; } = 0.5f;

        [JsonProperty("use_quest_type_roles")]
        public bool UseQuestTypeRoles { get; set; } = true;

        [JsonProperty("enable_communication_range")]
        public bool EnableCommunicationRange { get; set; } = true;

        [JsonProperty("communication_range_no_earpiece")]
        public float CommunicationRangeNoEarpiece { get; set; } = 35f;

        [JsonProperty("communication_range_earpiece")]
        public float CommunicationRangeEarpiece { get; set; } = 200f;

        [JsonProperty("enable_squad_personality")]
        public bool EnableSquadPersonality { get; set; } = true;

        [JsonProperty("enable_position_validation")]
        public bool EnablePositionValidation { get; set; } = true;

        [JsonProperty("navmesh_sample_radius")]
        public float NavMeshSampleRadius { get; set; } = 2.0f;

        [JsonProperty("fallback_candidate_count")]
        public int FallbackCandidateCount { get; set; } = 16;

        [JsonProperty("fallback_search_radius")]
        public float FallbackSearchRadius { get; set; } = 15.0f;

        [JsonProperty("enable_reachability_check")]
        public bool EnableReachabilityCheck { get; set; } = true;

        [JsonProperty("max_path_length_multiplier")]
        public float MaxPathLengthMultiplier { get; set; } = 2.5f;

        [JsonProperty("enable_los_check")]
        public bool EnableLosCheck { get; set; } = true;

        public SquadStrategyConfig() { }
    }
}
