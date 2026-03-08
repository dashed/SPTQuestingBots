namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Type of loot target detected by the scanning system.
/// Stored as byte on BotEntity for dense iteration.
/// </summary>
public static class LootTargetType
{
    public const byte None = 0;
    public const byte Container = 1;
    public const byte LooseItem = 2;
    public const byte Corpse = 3;

    /// <summary>Default estimated value for containers (contents unknown before opening).</summary>
    public const float DefaultContainerValue = 15000f;

    /// <summary>Default estimated value for corpses (gear value unknown before searching).</summary>
    public const float DefaultCorpseValue = 20000f;
}
