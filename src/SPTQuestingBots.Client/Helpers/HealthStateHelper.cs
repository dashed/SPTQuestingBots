using EFT;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Null-safe static methods for querying bot health, stamina, and physical
    /// condition. Used by sprint gating, utility scoring, and room clearing.
    /// </summary>
    public static class HealthStateHelper
    {
        /// <summary>Stamina level below which the bot is considered exhausted.</summary>
        private const float StaminaExhaustionThreshold = 15f;

        /// <summary>
        /// Returns <c>true</c> if the bot's stamina is below the exhaustion threshold.
        /// </summary>
        public static bool IsStaminaExhausted(BotOwner bot)
        {
            var stamina = bot?.GetPlayer?.Physical?.Stamina;
            if (stamina == null)
            {
                return false;
            }

            return stamina.Current < StaminaExhaustionThreshold;
        }

        /// <summary>
        /// Returns <c>true</c> if the bot is currently using medicine (healing animation active).
        /// </summary>
        public static bool IsMedicineInUse(BotOwner bot)
        {
            return bot?.Medecine?.Using == true;
        }

        /// <summary>
        /// Returns <c>true</c> if the bot has a physical condition that disables sprinting
        /// (e.g., broken leg without painkillers).
        /// </summary>
        public static bool HasSprintDisablingCondition(BotOwner bot)
        {
            var movementContext = bot?.GetPlayer?.MovementContext;
            if (movementContext == null)
            {
                return false;
            }

            // Painkillers negate leg damage for sprint purposes
            if (movementContext.PhysicalConditionContainsAny(EPhysicalCondition.OnPainkillers))
            {
                return false;
            }

            return movementContext.PhysicalConditionContainsAny(EPhysicalCondition.LeftLegDamaged | EPhysicalCondition.RightLegDamaged);
        }

        /// <summary>
        /// Returns <c>true</c> if the bot has leg damage (regardless of painkiller state).
        /// Used for noise/stealth checks where painkillers don't eliminate the noise.
        /// </summary>
        public static bool HasLegDamage(BotOwner bot)
        {
            var movementContext = bot?.GetPlayer?.MovementContext;
            if (movementContext == null)
            {
                return false;
            }

            return movementContext.PhysicalConditionContainsAny(EPhysicalCondition.LeftLegDamaged | EPhysicalCondition.RightLegDamaged);
        }

        /// <summary>
        /// Returns <c>true</c> if the bot is overweight (weight ratio >= 1.0).
        /// </summary>
        public static bool IsOverweight(BotOwner bot)
        {
            var physical = bot?.GetPlayer?.Physical;
            if (physical == null)
            {
                return false;
            }

            return physical.Overweight >= 1f;
        }

        /// <summary>
        /// Returns <c>true</c> if the bot's first aid system has pending healing to do.
        /// </summary>
        public static bool NeedsFirstAid(BotOwner bot)
        {
            return bot?.Medecine?.FirstAid?.Have2Do == true;
        }
    }
}
