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

    /// <summary>Duration in seconds for the brief pause at sharp corners. BSG default: 1.2s.</summary>
    [JsonProperty("corner_pause_duration")]
    public float CornerPauseDuration { get; set; } = 1.2f;

    /// <summary>Angle threshold in degrees for detecting sharp corners in the path.</summary>
    [JsonProperty("corner_angle_threshold")]
    public float CornerAngleThreshold { get; set; } = 60.0f;

    /// <summary>Bot pose during room clearing (0=crouch, 1=standing). 0.7 = slight crouch.</summary>
    [JsonProperty("pose")]
    public float Pose { get; set; } = 0.7f;

    /// <summary>
    /// Distance in meters the bot walks through a doorway before resuming normal speed.
    /// BSG default: 0.75m. Controls how far into a room the bot moves at walking pace.
    /// </summary>
    [JsonProperty("walk_through_distance")]
    public float WalkThroughDistance { get; set; } = 0.75f;

    /// <summary>
    /// Raycast distance in meters for left/right checks when entering a room.
    /// BSG default: 30m. Used to detect room boundaries and threats during entry.
    /// </summary>
    [JsonProperty("look_raycast_distance")]
    public float LookRaycastDistance { get; set; } = 30.0f;

    /// <summary>
    /// Duration in seconds the bot looks in each direction (left/right) when clearing a room.
    /// BSG default: 1.2s per direction.
    /// </summary>
    [JsonProperty("look_duration")]
    public float LookDuration { get; set; } = 1.2f;

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
