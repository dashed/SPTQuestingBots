namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Vulture behavior phase constants.
/// Stored on <see cref="BotEntity.VulturePhase"/> as a byte.
/// </summary>
public static class VulturePhase
{
    public const byte None = 0;
    public const byte Approach = 1;
    public const byte SilentApproach = 2;
    public const byte HoldAmbush = 3;
    public const byte Rush = 4;
    public const byte Paranoia = 5;
    public const byte Complete = 6;
}
