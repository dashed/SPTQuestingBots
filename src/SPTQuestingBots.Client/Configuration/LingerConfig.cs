using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration;

/// <summary>
/// Configuration for the linger system — bots pause briefly after completing
/// an objective, looking around before moving on. Controls duration, pose,
/// head scanning behavior, and per-role enablement.
/// </summary>
public class LingerConfig
{
    /// <summary>Whether the linger system is enabled.</summary>
    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Base utility score for linger behavior (decays linearly to zero).</summary>
    [JsonProperty("base_score")]
    public float BaseScore { get; set; } = 0.45f;

    /// <summary>Minimum linger duration in seconds.</summary>
    [JsonProperty("duration_min")]
    public float DurationMin { get; set; } = 10.0f;

    /// <summary>Maximum linger duration in seconds.</summary>
    [JsonProperty("duration_max")]
    public float DurationMax { get; set; } = 30.0f;

    /// <summary>Minimum interval in seconds between random head scans.</summary>
    [JsonProperty("head_scan_interval_min")]
    public float HeadScanIntervalMin { get; set; } = 3.0f;

    /// <summary>Maximum interval in seconds between random head scans.</summary>
    [JsonProperty("head_scan_interval_max")]
    public float HeadScanIntervalMax { get; set; } = 8.0f;

    /// <summary>Bot pose while lingering (0=crouch, 1=standing). 0.7 = slight crouch.</summary>
    [JsonProperty("pose")]
    public float Pose { get; set; } = 0.7f;

    /// <summary>Whether PMC bots can linger after objectives.</summary>
    [JsonProperty("enable_for_pmcs")]
    public bool EnableForPmcs { get; set; } = true;

    /// <summary>Whether regular scav bots can linger after objectives.</summary>
    [JsonProperty("enable_for_scavs")]
    public bool EnableForScavs { get; set; } = true;

    /// <summary>Whether player scav bots can linger after objectives.</summary>
    [JsonProperty("enable_for_pscavs")]
    public bool EnableForPscavs { get; set; } = true;

    public LingerConfig() { }
}
