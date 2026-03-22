namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Pure C# search logic for entrance points.
    /// No Unity or EFT dependencies — fully testable in net9.0.
    /// </summary>
    public static class EntrancePointScorer
    {
        /// <summary>
        /// Find the nearest entrance to a position.
        /// Uses the center point of each entrance for distance comparison.
        /// </summary>
        /// <param name="posX">Query position X.</param>
        /// <param name="posY">Query position Y.</param>
        /// <param name="posZ">Query position Z.</param>
        /// <param name="entrances">Pre-loaded entrance array from <see cref="BotZoneEntranceHelper.GetAllEntrances"/>.</param>
        /// <param name="nearest">The nearest entrance data (only valid if method returns true).</param>
        /// <returns>True if an entrance was found.</returns>
        public static bool TryFindNearestEntrance(
            float posX,
            float posY,
            float posZ,
            EntrancePointData[] entrances,
            out EntrancePointData nearest
        )
        {
            nearest = default;

            if (entrances == null || entrances.Length == 0)
                return false;

            float bestDistSqr = float.MaxValue;
            bool found = false;

            for (int i = 0; i < entrances.Length; i++)
            {
                var e = entrances[i];
                float dx = e.CenterX - posX;
                float dy = e.CenterY - posY;
                float dz = e.CenterZ - posZ;
                float distSqr = dx * dx + dy * dy + dz * dz;

                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    nearest = e;
                    found = true;
                }
            }

            return found;
        }
    }
}
