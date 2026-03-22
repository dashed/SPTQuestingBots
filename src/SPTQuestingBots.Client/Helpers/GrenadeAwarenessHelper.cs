using EFT;
using EFT.InventoryLogic;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Null-safe static helpers for grenade awareness checks.
    /// Wraps BSG's <c>BotBewareGrenade</c>, <c>BotGrenadeController</c>,
    /// and <c>WeaponManager.Grenades</c> APIs.
    /// </summary>
    public static class GrenadeAwarenessHelper
    {
        /// <summary>
        /// Returns <c>true</c> if the bot's grenade awareness system says it should flee.
        /// The BSG RunAwayGrenade brain layer (~priority 80) will already override our questing
        /// layer (18), but this lets us handle state cleanup gracefully.
        /// </summary>
        public static bool ShouldFleeGrenade(BotOwner bot)
        {
            if (bot == null)
                return false;

            try
            {
                return bot.BewareGrenade?.ShallRunAway() == true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the bot is currently in a grenade throw animation.
        /// Movement commands should be skipped during throw animations to avoid
        /// interrupting the throw.
        /// </summary>
        public static bool IsThrowingGrenade(BotOwner bot)
        {
            if (bot == null)
                return false;

            try
            {
                return bot.WeaponManager?.Grenades?.ThrowindNow == true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the bot has smoke grenades in its inventory.
        /// Could be used for tactical movement decisions (smoke cover before crossing open areas).
        /// </summary>
        public static bool HasSmokeGrenades(BotOwner bot)
        {
            if (bot == null)
                return false;

            try
            {
                var grenades = bot.WeaponManager?.Grenades;
                if (grenades == null)
                    return false;

                return grenades.HaveGrenadeOfType(ThrowWeapType.smoke_grenade);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if any member of the given squad is currently throwing a grenade.
        /// Used by squad strategies to skip movement commands during throw animations.
        /// </summary>
        public static bool IsAnySquadMemberThrowing(BotLogic.ECS.SquadEntity squad)
        {
            if (squad == null)
                return false;

            for (int i = 0; i < squad.Members.Count; i++)
            {
                if (squad.Members[i].IsThrowingGrenade)
                    return true;
            }

            return false;
        }
    }
}
