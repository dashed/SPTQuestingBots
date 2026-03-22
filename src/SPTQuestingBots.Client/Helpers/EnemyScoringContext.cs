namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Context data for enemy-aware scoring decisions.
    /// Pure C# struct — no Unity dependencies — fully testable in net9.0.
    /// </summary>
    public struct EnemyScoringContext
    {
        /// <summary>Whether the bot has a valid, alive enemy tracked.</summary>
        public bool HasEnemy;

        /// <summary>Distance to the goal enemy in meters.</summary>
        public float Distance;

        /// <summary>Whether the enemy is currently visible.</summary>
        public bool IsVisible;

        /// <summary>Seconds since the enemy was last personally seen.</summary>
        public float TimeSinceLastSeen;
    }
}
