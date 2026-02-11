using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS;

/// <summary>
/// Behavior modifiers derived from a squad's personality type.
/// Coordination (1-5) affects sharing and formation tightness.
/// Aggression (1-5) affects engagement willingness and risk tolerance.
/// </summary>
public readonly struct SquadPersonalitySettings
{
    public readonly float CoordinationLevel;
    public readonly float AggressionLevel;

    public SquadPersonalitySettings(float coordinationLevel, float aggressionLevel)
    {
        CoordinationLevel = coordinationLevel;
        AggressionLevel = aggressionLevel;
    }

    /// <summary>
    /// Returns preset settings for a given personality type.
    /// </summary>
    public static SquadPersonalitySettings ForType(SquadPersonalityType type)
    {
        SquadPersonalitySettings settings;
        switch (type)
        {
            case SquadPersonalityType.Elite:
                settings = new SquadPersonalitySettings(5f, 4f);
                break;
            case SquadPersonalityType.GigaChads:
                settings = new SquadPersonalitySettings(4f, 5f);
                break;
            case SquadPersonalityType.Rats:
                settings = new SquadPersonalitySettings(2f, 1f);
                break;
            case SquadPersonalityType.TimmyTeam6:
                settings = new SquadPersonalitySettings(1f, 2f);
                break;
            default:
                settings = new SquadPersonalitySettings(3f, 3f);
                break;
        }

        LoggingController.LogDebug(
            "[SquadPersonalitySettings] Type="
                + type
                + " -> coordination="
                + settings.CoordinationLevel
                + ", aggression="
                + settings.AggressionLevel
        );
        return settings;
    }

    /// <summary>
    /// Chance (0-100%) that this squad shares information with members.
    /// Formula from SAIN: 25 + CoordinationLevel * 15 (range: 40-100%).
    /// </summary>
    public float GetSharingChance()
    {
        return 25f + CoordinationLevel * 15f;
    }
}
