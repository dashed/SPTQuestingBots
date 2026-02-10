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

        [JsonProperty("enable_cover_position_source")]
        public bool EnableCoverPositionSource { get; set; } = true;

        [JsonProperty("cover_search_radius")]
        public float CoverSearchRadius { get; set; } = 25f;

        // ── Formation Movement ───────────────────────────────────────

        [JsonProperty("enable_formation_movement")]
        public bool EnableFormationMovement { get; set; } = true;

        [JsonProperty("catch_up_distance")]
        public float CatchUpDistance { get; set; } = 30f;

        [JsonProperty("match_speed_distance")]
        public float MatchSpeedDistance { get; set; } = 15f;

        [JsonProperty("slow_approach_distance")]
        public float SlowApproachDistance { get; set; } = 5f;

        [JsonProperty("column_spacing")]
        public float ColumnSpacing { get; set; } = 4f;

        [JsonProperty("spread_spacing")]
        public float SpreadSpacing { get; set; } = 3f;

        [JsonProperty("formation_switch_width")]
        public float FormationSwitchWidth { get; set; } = 8f;

        // ── Voice Commands ─────────────────────────────────────────

        [JsonProperty("enable_voice_commands")]
        public bool EnableVoiceCommands { get; set; } = true;

        [JsonProperty("voice_command_cooldown")]
        public float VoiceCommandCooldown { get; set; } = 5.0f;

        [JsonProperty("follower_response_delay")]
        public float FollowerResponseDelay { get; set; } = 0.8f;

        public SquadStrategyConfig() { }
    }
}
