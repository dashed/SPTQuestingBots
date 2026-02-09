namespace SPTQuestingBots.BotLogic.ECS
{
    /// <summary>
    /// Personality archetype for a squad, determined by member composition.
    /// Higher enum values win ties in <see cref="Systems.SquadPersonalityCalculator"/>.
    /// </summary>
    public enum SquadPersonalityType : byte
    {
        None = 0,
        TimmyTeam6 = 1,
        Rats = 2,
        GigaChads = 3,
        Elite = 4,
    }
}
