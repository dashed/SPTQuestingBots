using System;
using System.Runtime.CompilerServices;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Distributes followers across candidate cell positions with seed-based jitter.
    /// Creates a search-party pattern by assigning each follower to a different
    /// neighboring grid cell with a unique offset.
    /// <para>
    /// Pure C# — no Unity or EFT dependencies — fully testable.
    /// </para>
    /// </summary>
    public static class ZoneFollowerPositionCalculator
    {
        /// <summary>
        /// Golden angle in radians, used for deterministic angular distribution.
        /// </summary>
        private const float GoldenAngle = 2.3999632f;

        /// <summary>
        /// Distributes followers across candidate positions using round-robin with seed-based offset and jitter.
        /// </summary>
        /// <param name="candidatePositions">Flat float array [x,y,z, x,y,z, ...] of navigable neighboring cell centers.</param>
        /// <param name="candidateCount">Number of candidate positions (candidatePositions.Length / 3).</param>
        /// <param name="followerSeeds">Per-follower noise seeds (from BotEntity.FieldNoiseSeed).</param>
        /// <param name="followerCount">Number of followers to distribute.</param>
        /// <param name="jitterRadius">Random offset radius around cell center (meters).</param>
        /// <param name="outPositions">Output flat float array [x,y,z, x,y,z, ...] — one position per follower.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DistributeFollowers(
            float[] candidatePositions,
            int candidateCount,
            int[] followerSeeds,
            int followerCount,
            float jitterRadius,
            float[] outPositions
        )
        {
            if (candidateCount <= 0 || candidatePositions == null)
            {
                for (int i = 0; i < followerCount * 3; i++)
                {
                    outPositions[i] = float.NaN;
                }
                return;
            }

            for (int i = 0; i < followerCount; i++)
            {
                int seed = followerSeeds[i];

                // Mask off sign bit to avoid overflow from Math.Abs(int.MinValue)
                int safeSeed = seed & 0x7FFFFFFF;
                int seedOffset = safeSeed % candidateCount;
                int candIdx = (i + seedOffset) % candidateCount;

                // Golden angle for deterministic angular distribution
                float angle = seed * GoldenAngle;

                // Simple hash to get a 0..1 fraction for radius variation
                float frac = ((seed * 0x45d9f3b) & 0x7FFFFFFF) / (float)0x7FFFFFFF;

                float effectiveRadius = jitterRadius * (0.3f + 0.7f * frac);
                float jitterX = (float)Math.Cos(angle) * effectiveRadius;
                float jitterZ = (float)Math.Sin(angle) * effectiveRadius;

                int srcOff = candIdx * 3;
                int dstOff = i * 3;
                outPositions[dstOff] = candidatePositions[srcOff] + jitterX;
                outPositions[dstOff + 1] = candidatePositions[srcOff + 1]; // Y unchanged
                outPositions[dstOff + 2] = candidatePositions[srcOff + 2] + jitterZ;
            }
        }
    }
}
