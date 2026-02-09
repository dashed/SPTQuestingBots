namespace SPTQuestingBots.BotLogic.ECS
{
    /// <summary>
    /// Classification of a bot entity.
    /// Replaces BotRegistrationManager.registeredPMCs/registeredBosses HashSet lookups.
    /// </summary>
    public enum BotType
    {
        Unknown = 0,
        PMC,
        Scav,
        PScav,
        Boss,
    }
}
