using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration;

public class BotPathingConfig
{
    [JsonProperty("max_start_position_discrepancy")]
    public float MaxStartPositionDiscrepancy { get; set; } = 0.5f;

    [JsonProperty("incomplete_path_retry_interval")]
    public float IncompletePathRetryInterval { get; set; } = 5;

    [JsonProperty("use_custom_mover")]
    public bool UseCustomMover { get; set; } = true;

    [JsonProperty("bypass_door_colliders")]
    public bool BypassDoorColliders { get; set; } = true;

    [JsonProperty("threat_avoidance_enabled")]
    public bool ThreatAvoidanceEnabled { get; set; } = true;

    [JsonProperty("threat_avoidance_cooldown")]
    public float ThreatAvoidanceCooldown { get; set; } = 30f;

    [JsonProperty("danger_place_avoidance_enabled")]
    public bool DangerPlaceAvoidanceEnabled { get; set; } = true;

    [JsonProperty("mine_avoidance_enabled")]
    public bool MineAvoidanceEnabled { get; set; } = true;

    [JsonProperty("mine_avoidance_radius")]
    public float MineAvoidanceRadius { get; set; } = 50f;

    public BotPathingConfig() { }
}
