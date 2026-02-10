namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Integer constants for squad voice callout types.
    /// Maps to BSG's <c>EPhraseTrigger</c> via <see cref="SPTQuestingBots.Helpers.SquadVoiceHelper"/>.
    /// Uses int constants instead of enum for zero-allocation comparisons in hot paths.
    /// </summary>
    public static class SquadCalloutId
    {
        /// <summary>No callout.</summary>
        public const int None = 0;

        /// <summary>Boss orders squad to follow. Maps to EPhraseTrigger.FollowMe.</summary>
        public const int FollowMe = 1;

        /// <summary>Boss orders squad to move out. Maps to EPhraseTrigger.Gogogo.</summary>
        public const int Gogogo = 2;

        /// <summary>Boss orders squad to hold position. Maps to EPhraseTrigger.HoldPosition.</summary>
        public const int HoldPosition = 3;

        /// <summary>Follower acknowledgment. Maps to EPhraseTrigger.Roger.</summary>
        public const int Roger = 4;

        /// <summary>Follower reports moving out. Maps to EPhraseTrigger.Going.</summary>
        public const int Going = 5;

        /// <summary>Follower reports arrival at position. Maps to EPhraseTrigger.OnPosition.</summary>
        public const int OnPosition = 6;

        /// <summary>Enemy spotted behind (dot forward &lt; -0.5). Maps to EPhraseTrigger.OnSix.</summary>
        public const int OnSix = 7;

        /// <summary>Enemy spotted on left flank (dot right &lt; -0.5). Maps to EPhraseTrigger.LeftFlank.</summary>
        public const int LeftFlank = 8;

        /// <summary>Enemy spotted on right flank (dot right &gt; 0.5). Maps to EPhraseTrigger.RightFlank.</summary>
        public const int RightFlank = 9;

        /// <summary>Enemy spotted in front (dot forward &gt; 0.5). Maps to EPhraseTrigger.InTheFront.</summary>
        public const int InTheFront = 10;

        /// <summary>Bot is providing covering fire. Maps to EPhraseTrigger.Covering.</summary>
        public const int Covering = 11;

        /// <summary>Bot is engaging in combat. Maps to EPhraseTrigger.OnFight.</summary>
        public const int OnFight = 12;

        /// <summary>First enemy contact detected. Maps to EPhraseTrigger.OnFirstContact.</summary>
        public const int OnFirstContact = 13;
    }
}
