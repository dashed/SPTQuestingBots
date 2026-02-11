using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration;

/// <summary>
/// Configuration for the room clearing system — bots slow down and adopt a
/// cautious posture when transitioning from outdoor to indoor environments.
/// Controls duration, pose, corner pause behavior, and per-role enablement.
/// </summary>
public class RoomClearConfig
{
    /// <summary>Whether the room clearing system is enabled.</summary>
    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Minimum room clear duration in seconds after entering indoors.</summary>
    [JsonProperty("duration_min")]
    public float DurationMin { get; set; } = 15.0f;

    /// <summary>Maximum room clear duration in seconds after entering indoors.</summary>
    [JsonProperty("duration_max")]
    public float DurationMax { get; set; } = 30.0f;

    /// <summary>Duration in seconds for the brief pause at sharp corners.</summary>
    [JsonProperty("corner_pause_duration")]
    public float CornerPauseDuration { get; set; } = 1.5f;

    /// <summary>Angle threshold in degrees for detecting sharp corners in the path.</summary>
    [JsonProperty("corner_angle_threshold")]
    public float CornerAngleThreshold { get; set; } = 60.0f;

    /// <summary>Bot pose during room clearing (0=crouch, 1=standing). 0.7 = slight crouch.</summary>
    [JsonProperty("pose")]
    public float Pose { get; set; } = 0.7f;

    /// <summary>Whether PMC bots use room clearing behavior.</summary>
    [JsonProperty("enable_for_pmcs")]
    public bool EnableForPmcs { get; set; } = true;

    /// <summary>Whether regular scav bots use room clearing behavior.</summary>
    [JsonProperty("enable_for_scavs")]
    public bool EnableForScavs { get; set; } = true;

    /// <summary>Whether player scav bots use room clearing behavior.</summary>
    [JsonProperty("enable_for_pscavs")]
    public bool EnableForPscavs { get; set; } = true;

    public RoomClearConfig() { }
}
