using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    /// <summary>
    /// Configuration for the squad strategy system: role distances, formation movement,
    /// communication range, combat positioning, objective sharing, and voice commands.
    /// Loaded from config.json under the "squad_strategy" key.
    /// </summary>
    public class SquadStrategyConfig
    {
        /// <summary>Whether the squad strategy system is enabled.</summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>Distance (meters) for Guard role positions around the objective.</summary>
        [JsonProperty("guard_distance")]
        public float GuardDistance { get; set; } = 8f;

        /// <summary>Distance (meters) for Flanker role positions perpendicular to approach.</summary>
        [JsonProperty("flank_distance")]
        public float FlankDistance { get; set; } = 15f;

        /// <summary>Distance (meters) for Overwatch role positions behind approach direction.</summary>
        [JsonProperty("overwatch_distance")]
        public float OverwatchDistance { get; set; } = 25f;

        /// <summary>Distance (meters) for Escort role trailing behind the boss.</summary>
        [JsonProperty("escort_distance")]
        public float EscortDistance { get; set; } = 5f;

        /// <summary>Radius (meters) within which a follower is considered to have arrived at its tactical position.</summary>
        [JsonProperty("arrival_radius")]
        public float ArrivalRadius { get; set; } = 3f;

        /// <summary>Maximum allowed distance (meters) a follower can be from the boss before regrouping.</summary>
        [JsonProperty("max_distance_from_boss")]
        public float MaxDistanceFromBoss { get; set; } = 75f;

        /// <summary>Minimum time (seconds) between strategy re-evaluations.</summary>
        [JsonProperty("strategy_pacing_seconds")]
        public float StrategyPacingSeconds { get; set; } = 0.5f;

        /// <summary>Whether to assign tactical roles based on the leader's current quest action type.</summary>
        [JsonProperty("use_quest_type_roles")]
        public bool UseQuestTypeRoles { get; set; } = true;

        /// <summary>Whether to enforce communication range limits between squad members.</summary>
        [JsonProperty("enable_communication_range")]
        public bool EnableCommunicationRange { get; set; } = true;

        /// <summary>Base communication range (meters) when neither bot has an earpiece.</summary>
        [JsonProperty("communication_range_no_earpiece")]
        public float CommunicationRangeNoEarpiece { get; set; } = 35f;

        /// <summary>Extended communication range (meters) when both bots have earpieces.</summary>
        [JsonProperty("communication_range_earpiece")]
        public float CommunicationRangeEarpiece { get; set; } = 200f;

        /// <summary>Whether to compute squad personality from member bot types.</summary>
        [JsonProperty("enable_squad_personality")]
        public bool EnableSquadPersonality { get; set; } = true;

        /// <summary>Whether to validate tactical positions against the NavMesh.</summary>
        [JsonProperty("enable_position_validation")]
        public bool EnablePositionValidation { get; set; } = true;

        /// <summary>Maximum distance (meters) from computed position to search for a valid NavMesh point.</summary>
        [JsonProperty("navmesh_sample_radius")]
        public float NavMeshSampleRadius { get; set; } = 2.0f;

        /// <summary>Number of sunflower spiral candidates to try when primary position validation fails.</summary>
        [JsonProperty("fallback_candidate_count")]
        public int FallbackCandidateCount { get; set; } = 16;

        /// <summary>Search radius (meters) for fallback sunflower spiral positions.</summary>
        [JsonProperty("fallback_search_radius")]
        public float FallbackSearchRadius { get; set; } = 15.0f;

        /// <summary>Whether to verify NavMesh path exists between objective and tactical position.</summary>
        [JsonProperty("enable_reachability_check")]
        public bool EnableReachabilityCheck { get; set; } = true;

        /// <summary>Multiplier on direct distance to compute maximum allowed NavMesh path length.</summary>
        [JsonProperty("max_path_length_multiplier")]
        public float MaxPathLengthMultiplier { get; set; } = 2.5f;

        /// <summary>Whether to verify line-of-sight for Overwatch positions.</summary>
        [JsonProperty("enable_los_check")]
        public bool EnableLosCheck { get; set; } = true;

        /// <summary>Whether to use BSG's pre-computed cover voxel grid for tactical positions.</summary>
        [JsonProperty("enable_cover_position_source")]
        public bool EnableCoverPositionSource { get; set; } = true;

        /// <summary>Search radius (meters) for BSG cover point collection around the objective.</summary>
        [JsonProperty("cover_search_radius")]
        public float CoverSearchRadius { get; set; } = 25f;

        // ── Formation Movement ───────────────────────────────────────

        /// <summary>Whether to use formation-based movement (Column/Spread) for followers.</summary>
        [JsonProperty("enable_formation_movement")]
        public bool EnableFormationMovement { get; set; } = true;

        /// <summary>Distance (meters) from boss beyond which followers sprint to catch up.</summary>
        [JsonProperty("catch_up_distance")]
        public float CatchUpDistance { get; set; } = 30f;

        /// <summary>Distance (meters) from boss within which followers match boss speed.</summary>
        [JsonProperty("match_speed_distance")]
        public float MatchSpeedDistance { get; set; } = 15f;

        /// <summary>Distance (meters) to tactical position within which followers slow down.</summary>
        [JsonProperty("slow_approach_distance")]
        public float SlowApproachDistance { get; set; } = 5f;

        /// <summary>Spacing (meters) between followers in Column formation.</summary>
        [JsonProperty("column_spacing")]
        public float ColumnSpacing { get; set; } = 4f;

        /// <summary>Spacing (meters) between followers in Spread formation.</summary>
        [JsonProperty("spread_spacing")]
        public float SpreadSpacing { get; set; } = 3f;

        /// <summary>Path width threshold (meters) to switch from Column to Spread formation.</summary>
        [JsonProperty("formation_switch_width")]
        public float FormationSwitchWidth { get; set; } = 8f;

        // ── Combat-Aware Positioning ──────────────────────────────

        /// <summary>Whether to recompute tactical positions when squad detects enemies.</summary>
        [JsonProperty("enable_combat_aware_positioning")]
        public bool EnableCombatAwarePositioning { get; set; } = true;

        // ── Zone Movement Integration ──────────────────────────────

        /// <summary>Whether to spread followers across neighboring grid cells during zone movement.</summary>
        [JsonProperty("enable_zone_follower_spread")]
        public bool EnableZoneFollowerSpread { get; set; } = true;

        /// <summary>Jitter radius (meters) for follower positions within assigned zone cells.</summary>
        [JsonProperty("zone_jitter_radius")]
        public float ZoneJitterRadius { get; set; } = 5f;

        // ── Multi-Level Objective Sharing ──────────────────────────

        /// <summary>Whether to use two-tier objective sharing (Direct + Relayed) instead of flat sharing.</summary>
        [JsonProperty("enable_objective_sharing")]
        public bool EnableObjectiveSharing { get; set; } = true;

        /// <summary>Maximum number of Tier 1 (direct) followers that receive exact positions.</summary>
        [JsonProperty("trusted_follower_count")]
        public int TrustedFollowerCount { get; set; } = 2;

        /// <summary>Base noise magnitude (meters) applied to Tier 2 (relayed) positions.</summary>
        [JsonProperty("sharing_noise_base")]
        public float SharingNoiseBase { get; set; } = 5f;

        // ── Voice Commands ─────────────────────────────────────────

        /// <summary>Whether to play voice lines for squad callouts (objective changes, arrivals, etc.).</summary>
        [JsonProperty("enable_voice_commands")]
        public bool EnableVoiceCommands { get; set; } = true;

        /// <summary>Minimum time (seconds) between voice callouts from the same bot.</summary>
        [JsonProperty("voice_command_cooldown")]
        public float VoiceCommandCooldown { get; set; } = 5.0f;

        /// <summary>Delay (seconds) before a follower responds to a boss callout.</summary>
        [JsonProperty("follower_response_delay")]
        public float FollowerResponseDelay { get; set; } = 0.8f;

        public SquadStrategyConfig() { }
    }
}
