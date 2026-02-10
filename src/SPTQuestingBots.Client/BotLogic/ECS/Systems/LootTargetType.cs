namespace SPTQuestingBots.BotLogic.ECS.Systems
{
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
    }
}
