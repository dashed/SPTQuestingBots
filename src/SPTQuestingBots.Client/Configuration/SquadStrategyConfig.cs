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

        public SquadStrategyConfig() { }
    }
}
