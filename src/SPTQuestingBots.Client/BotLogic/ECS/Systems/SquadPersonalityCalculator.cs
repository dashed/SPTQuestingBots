namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Determines squad personality from member bot types via majority vote.
    /// Tie-breaking: higher <see cref="SquadPersonalityType"/> enum value wins.
    /// Pure C# — no Unity or EFT dependencies.
    /// </summary>
    public static class SquadPersonalityCalculator
    {
        /// <summary>
        /// Determine squad personality from member bot types.
        /// Each bot type maps to a personality; majority wins with higher-value tie-breaking.
        /// </summary>
        /// <param name="memberTypes">Array of bot types for squad members.</param>
        /// <param name="count">Number of valid entries in <paramref name="memberTypes"/>.</param>
        public static SquadPersonalityType DeterminePersonality(BotType[] memberTypes, int count)
        {
            if (count <= 0 || memberTypes == null)
                return SquadPersonalityType.None;

            // Vote counts per personality type (index matches enum value)
            int eliteVotes = 0;
            int gigaChadVotes = 0;
            int ratVotes = 0;
            int timmyVotes = 0;

            for (int i = 0; i < count; i++)
            {
                switch (memberTypes[i])
                {
                    case BotType.Boss:
                        eliteVotes++;
                        break;
                    case BotType.PMC:
                        gigaChadVotes++;
                        break;
                    case BotType.Scav:
                        ratVotes++;
                        break;
                    case BotType.PScav:
                        timmyVotes++;
                        break;
                    // BotType.Unknown: skip, no vote
                }
            }

            int totalVotes = eliteVotes + gigaChadVotes + ratVotes + timmyVotes;
            if (totalVotes == 0)
                return SquadPersonalityType.None;

            // Find the winner — higher enum value wins ties
            var bestType = SquadPersonalityType.None;
            int bestCount = 0;

            if (timmyVotes > bestCount || (timmyVotes == bestCount && SquadPersonalityType.TimmyTeam6 > bestType))
            {
                bestType = SquadPersonalityType.TimmyTeam6;
                bestCount = timmyVotes;
            }

            if (ratVotes > bestCount || (ratVotes == bestCount && SquadPersonalityType.Rats > bestType))
            {
                bestType = SquadPersonalityType.Rats;
                bestCount = ratVotes;
            }

            if (gigaChadVotes > bestCount || (gigaChadVotes == bestCount && SquadPersonalityType.GigaChads > bestType))
            {
                bestType = SquadPersonalityType.GigaChads;
                bestCount = gigaChadVotes;
            }

            if (eliteVotes > bestCount || (eliteVotes == bestCount && SquadPersonalityType.Elite > bestType))
            {
                bestType = SquadPersonalityType.Elite;
                bestCount = eliteVotes;
            }

            return bestType;
        }
    }
}
