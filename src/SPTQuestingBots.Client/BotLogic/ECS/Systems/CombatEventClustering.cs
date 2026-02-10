namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Pure C# clustering and filtering logic for combat events.
    /// Extracted from <see cref="DynamicObjectiveGenerator"/> to enable
    /// unit testing without Quest/Unity dependencies.
    /// </summary>
    public static class CombatEventClustering
    {
        /// <summary>
        /// Result of clustering combat events into firefight hotspots.
        /// </summary>
        public struct ClusterResult
        {
            public float X;
            public float Y;
            public float Z;
            public int Intensity;
        }

        /// <summary>
        /// Cluster active combat events (excluding deaths) into firefight hotspots.
        /// Events within <paramref name="clusterRadiusSqr"/> of each other are grouped.
        /// Uses greedy seed-based clustering: the first unassigned event becomes a seed,
        /// all events within radius join that cluster.
        /// </summary>
        /// <param name="events">Array of combat events to cluster.</param>
        /// <param name="eventCount">Number of valid events in the array.</param>
        /// <param name="currentTime">Current game time for age filtering.</param>
        /// <param name="maxAge">Maximum event age in seconds.</param>
        /// <param name="clusterRadiusSqr">Squared cluster radius for distance checks.</param>
        /// <param name="output">Pre-allocated output buffer for cluster results.</param>
        /// <param name="maxClusters">Maximum number of clusters to output.</param>
        /// <returns>Number of clusters written to <paramref name="output"/>.</returns>
        public static int ClusterEvents(
            CombatEvent[] events,
            int eventCount,
            float currentTime,
            float maxAge,
            float clusterRadiusSqr,
            ClusterResult[] output,
            int maxClusters
        )
        {
            if (events == null || output == null || eventCount <= 0 || maxClusters <= 0)
                return 0;

            // Track which events have been assigned to a cluster
            bool[] assigned = new bool[eventCount];
            int clusterCount = 0;

            for (int seed = 0; seed < eventCount && clusterCount < maxClusters; seed++)
            {
                if (assigned[seed])
                    continue;

                ref CombatEvent seedEvt = ref events[seed];
                if (!seedEvt.IsActive)
                    continue;
                if (currentTime - seedEvt.Time > maxAge)
                    continue;
                if (seedEvt.Type == CombatEventType.Death)
                    continue;

                // Start a new cluster from this seed
                assigned[seed] = true;
                float sumX = seedEvt.X;
                float sumY = seedEvt.Y;
                float sumZ = seedEvt.Z;
                int memberCount = 1;
                int intensity = 1;
                if (seedEvt.Type == CombatEventType.Explosion)
                    intensity += 2;

                // Find all events within radius of the seed
                for (int j = seed + 1; j < eventCount; j++)
                {
                    if (assigned[j])
                        continue;

                    ref CombatEvent evt = ref events[j];
                    if (!evt.IsActive)
                        continue;
                    if (currentTime - evt.Time > maxAge)
                        continue;
                    if (evt.Type == CombatEventType.Death)
                        continue;

                    float dx = seedEvt.X - evt.X;
                    float dz = seedEvt.Z - evt.Z;
                    float distSqr = dx * dx + dz * dz;

                    if (distSqr <= clusterRadiusSqr)
                    {
                        assigned[j] = true;
                        sumX += evt.X;
                        sumY += evt.Y;
                        sumZ += evt.Z;
                        memberCount++;
                        intensity++;
                        if (evt.Type == CombatEventType.Explosion)
                            intensity += 2;
                    }
                }

                output[clusterCount] = new ClusterResult
                {
                    X = sumX / memberCount,
                    Y = sumY / memberCount,
                    Z = sumZ / memberCount,
                    Intensity = intensity,
                };
                clusterCount++;
            }

            return clusterCount;
        }

        /// <summary>
        /// Filter combat events to only active death events within age limit.
        /// </summary>
        /// <param name="events">Array of combat events.</param>
        /// <param name="eventCount">Number of valid events.</param>
        /// <param name="currentTime">Current game time.</param>
        /// <param name="maxAge">Maximum event age in seconds.</param>
        /// <param name="output">Pre-allocated output buffer.</param>
        /// <returns>Number of death events written to <paramref name="output"/>.</returns>
        public static int FilterDeathEvents(CombatEvent[] events, int eventCount, float currentTime, float maxAge, CombatEvent[] output)
        {
            if (events == null || output == null || eventCount <= 0)
                return 0;

            int written = 0;
            int maxWrite = output.Length;

            for (int i = 0; i < eventCount; i++)
            {
                ref CombatEvent evt = ref events[i];
                if (!evt.IsActive)
                    continue;
                if (evt.Type != CombatEventType.Death)
                    continue;
                if (currentTime - evt.Time > maxAge)
                    continue;

                if (written >= maxWrite)
                    break;

                output[written] = evt;
                written++;
            }

            return written;
        }
    }
}
