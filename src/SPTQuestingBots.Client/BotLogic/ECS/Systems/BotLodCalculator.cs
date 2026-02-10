using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Computes LOD tier and frame-skip decisions based on distance to nearest human player.
    /// Pure C# — no Unity or EFT dependencies.
    /// </summary>
    public static class BotLodCalculator
    {
        public const byte TierFull = 0;
        public const byte TierReduced = 1;
        public const byte TierMinimal = 2;

        /// <summary>
        /// Compute LOD tier based on squared distance to nearest human player.
        /// </summary>
        /// <param name="sqrDistToNearestHuman">Squared distance from bot to closest human.</param>
        /// <param name="reducedThresholdSqr">Squared distance threshold for reduced tier.</param>
        /// <param name="minimalThresholdSqr">Squared distance threshold for minimal tier.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ComputeTier(float sqrDistToNearestHuman, float reducedThresholdSqr, float minimalThresholdSqr)
        {
            byte tier;
            if (sqrDistToNearestHuman >= minimalThresholdSqr)
                tier = TierMinimal;
            else if (sqrDistToNearestHuman >= reducedThresholdSqr)
                tier = TierReduced;
            else
                tier = TierFull;

            LoggingController.LogDebug("[BotLodCalculator] ComputeTier: tier=" + tier + " (sqrDist=" + sqrDistToNearestHuman + ")");
            return tier;
        }

        /// <summary>
        /// Returns true if this frame should be skipped for the given LOD tier.
        /// frameCounter increments each tick. reducedSkip=2 means skip 2 of every 3 frames.
        /// </summary>
        /// <param name="lodTier">Current LOD tier (0=Full, 1=Reduced, 2=Minimal).</param>
        /// <param name="frameCounter">Monotonically increasing frame counter.</param>
        /// <param name="reducedSkip">Number of frames to skip per cycle in reduced tier.</param>
        /// <param name="minimalSkip">Number of frames to skip per cycle in minimal tier.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldSkipUpdate(byte lodTier, int frameCounter, int reducedSkip, int minimalSkip)
        {
            if (lodTier == TierFull)
                return false;
            if (lodTier == TierReduced)
                return (frameCounter % (reducedSkip + 1)) != 0;
            // TierMinimal
            return (frameCounter % (minimalSkip + 1)) != 0;
        }
    }
}
