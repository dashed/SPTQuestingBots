using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Pure C# route selection logic. Picks the best patrol route for a bot based on
    /// personality fit, raid time, and proximity to route waypoints.
    /// No Unity or EFT dependencies — fully testable in net9.0.
    /// </summary>
    public static class PatrolRouteSelector
    {
        /// <summary>
        /// Returns the index of the best patrol route for this bot, or -1 if none suitable.
        /// </summary>
        /// <param name="botX">Bot world X position.</param>
        /// <param name="botZ">Bot world Z position.</param>
        /// <param name="aggression">Bot aggression factor (0.0-1.0).</param>
        /// <param name="raidTimeNormalized">Normalized raid time (0.0-1.0).</param>
        /// <param name="routes">Available patrol routes for the current map.</param>
        /// <param name="seed">Deterministic seed for tiebreaking.</param>
        public static int SelectRoute(float botX, float botZ, float aggression, float raidTimeNormalized, PatrolRoute[] routes, int seed)
        {
            if (routes == null || routes.Length == 0)
                return -1;

            float bestScore = -1f;
            int bestIndex = -1;

            for (int i = 0; i < routes.Length; i++)
            {
                var route = routes[i];

                // Filter: personality
                if (aggression < route.MinAggression || aggression > route.MaxAggression)
                    continue;

                // Filter: raid time
                if (raidTimeNormalized < route.MinRaidTime || raidTimeNormalized > route.MaxRaidTime)
                    continue;

                // Must have waypoints
                if (route.Waypoints == null || route.Waypoints.Length == 0)
                    continue;

                // Score: inverse distance to nearest waypoint (closer = higher)
                float minDistSqr = float.MaxValue;
                for (int j = 0; j < route.Waypoints.Length; j++)
                {
                    float dx = botX - route.Waypoints[j].X;
                    float dz = botZ - route.Waypoints[j].Z;
                    float distSqr = dx * dx + dz * dz;
                    if (distSqr < minDistSqr)
                        minDistSqr = distSqr;
                }

                // Inverse distance: 1 / (1 + sqrt(dist)) so closer routes score higher
                float dist = minDistSqr > 0f ? Sqrt(minDistSqr) : 0f;
                float proximityScore = 1f / (1f + dist * 0.01f);

                // Personality fit: how centered the bot's aggression is within the route's range
                float aggressionRange = route.MaxAggression - route.MinAggression;
                float fitScore =
                    aggressionRange > 0f
                        ? 1f - System.Math.Abs(aggression - (route.MinAggression + aggressionRange * 0.5f)) / (aggressionRange * 0.5f)
                        : 1f;
                if (fitScore < 0f)
                    fitScore = 0f;

                float score = proximityScore * 0.6f + fitScore * 0.4f;

                // Deterministic tiebreak using seed and route index
                float jitter = ((seed * 31 + i * 17) & 0xFFFF) / 65536f * 0.01f;
                score += jitter;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>Simple float sqrt without Unity dependency.</summary>
        private static float Sqrt(float value)
        {
            return (float)System.Math.Sqrt(value);
        }
    }
}
