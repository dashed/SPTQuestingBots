namespace SPTQuestingBots.BotLogic.ECS
{
    /// <summary>
    /// Identifies a sensor boolean on <see cref="BotEntity"/>.
    /// Mirrors BotHiveMindSensorType but lives in the ECS namespace for decoupling.
    /// Used by <see cref="BotEntity.GetSensor"/> and <see cref="BotEntity.SetSensor"/>
    /// for generic group queries without per-sensor method duplication.
    /// </summary>
    public enum BotSensor
    {
        InCombat = 0,
        IsSuspicious,
        CanQuest,
        CanSprintToObjective,
        WantsToLoot,
    }
}
