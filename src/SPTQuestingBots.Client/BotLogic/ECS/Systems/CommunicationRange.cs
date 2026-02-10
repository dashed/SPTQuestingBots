using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Checks whether two bots are within communication range based on earpiece equipment.
    /// Pure C# — no Unity or EFT dependencies.
    /// </summary>
    public static class CommunicationRange
    {
        /// <summary>
        /// Check if two bots are within communication range.
        /// Both must have earpieces to use the extended earpiece range;
        /// otherwise the shorter no-earpiece range applies.
        /// </summary>
        /// <param name="hasEarpieceA">Whether bot A has an earpiece.</param>
        /// <param name="hasEarpieceB">Whether bot B has an earpiece.</param>
        /// <param name="sqrDistance">Squared distance between the two bots.</param>
        /// <param name="noEarpieceRange">Base communication range without earpieces.</param>
        /// <param name="earpieceRange">Extended communication range when both have earpieces.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRange(bool hasEarpieceA, bool hasEarpieceB, float sqrDistance, float noEarpieceRange, float earpieceRange)
        {
            float range = (hasEarpieceA && hasEarpieceB) ? earpieceRange : noEarpieceRange;
            float rangeSqr = range * range;
            bool inRange = sqrDistance <= rangeSqr;
            LoggingController.LogDebug(
                "[CommunicationRange] Check: earA="
                    + hasEarpieceA
                    + " earB="
                    + hasEarpieceB
                    + " sqrDist="
                    + sqrDistance
                    + " range="
                    + range
                    + " inRange="
                    + inRange
            );
            return inRange;
        }
    }
}
