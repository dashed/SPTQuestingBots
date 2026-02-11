using Newtonsoft.Json;

namespace SPTQuestingBots.Configuration;

/// <summary>
/// Configuration for the personality system — bot difficulty influences utility AI
/// scoring via aggression-based multipliers. Also controls raid time progression
/// modifiers that shift behavior from early rush to late-raid caution.
/// </summary>
public class PersonalityConfig
{
    /// <summary>Whether personality-based scoring modifiers are enabled.</summary>
    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Whether raid time progression modifiers are enabled.</summary>
    [JsonProperty("raid_time_enabled")]
    public bool RaidTimeEnabled { get; set; } = true;

    public PersonalityConfig() { }
}
