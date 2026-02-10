using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration
{
    public class BotLodConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("reduced_distance")]
        public float ReducedDistance { get; set; } = 150f;

        [JsonProperty("minimal_distance")]
        public float MinimalDistance { get; set; } = 300f;

        [JsonProperty("reduced_frame_skip")]
        public int ReducedFrameSkip { get; set; } = 2;

        [JsonProperty("minimal_frame_skip")]
        public int MinimalFrameSkip { get; set; } = 4;

        public BotLodConfig() { }
    }
}
