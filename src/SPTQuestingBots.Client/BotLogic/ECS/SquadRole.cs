namespace SPTQuestingBots.BotLogic.ECS
{
    /// <summary>
    /// Role assigned to a bot within its squad.
    /// Determines tactical positioning and behavior.
    /// </summary>
    public enum SquadRole : byte
    {
        /// <summary>No role assigned (solo or unassigned).</summary>
        None = 0,

        /// <summary>Squad leader — sets objectives and positions.</summary>
        Leader = 1,

        /// <summary>Close protection — stays near the leader.</summary>
        Guard = 2,

        /// <summary>Flanking element — moves to side positions.</summary>
        Flanker = 3,

        /// <summary>Overwatch — holds elevated/distant positions.</summary>
        Overwatch = 4,

        /// <summary>Escort — stays very close to the leader.</summary>
        Escort = 5,
    }
}
