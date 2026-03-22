using EFT;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Null-safe static methods for reading weapon state from BotOwner.
    /// Used by task scorers and action classes for weapon readiness decisions.
    /// </summary>
    public static class WeaponStateHelper
    {
        /// <summary>
        /// Returns <c>true</c> if the bot's weapon is ready to fire.
        /// Returns <c>false</c> if unavailable.
        /// </summary>
        public static bool IsWeaponReady(BotOwner bot)
        {
            if (bot?.WeaponManager == null)
            {
                return false;
            }

            return bot.WeaponManager.IsWeaponReady;
        }

        /// <summary>
        /// Returns <c>true</c> if the bot is currently reloading.
        /// Returns <c>false</c> if unavailable.
        /// </summary>
        public static bool IsReloading(BotOwner bot)
        {
            if (bot?.WeaponManager?.Reload == null)
            {
                return false;
            }

            return bot.WeaponManager.Reload.Reloading;
        }

        /// <summary>
        /// Returns <c>true</c> if the bot's shoot data allows shooting.
        /// Returns <c>false</c> if unavailable.
        /// </summary>
        public static bool CanShootByState(BotOwner bot)
        {
            if (bot?.ShootData == null)
            {
                return false;
            }

            return bot.ShootData.CanShootByState;
        }

        /// <summary>
        /// Returns <c>true</c> if the bot's weapon has an active malfunction.
        /// Returns <c>false</c> if unavailable.
        /// </summary>
        public static bool HasMalfunction(BotOwner bot)
        {
            if (bot?.WeaponManager?.Malfunctions == null)
            {
                return false;
            }

            return bot.WeaponManager.Malfunctions.HaveMalfunction();
        }

        /// <summary>
        /// Returns the ammo ratio (0.0-1.0) of the bot's current magazine.
        /// Returns 1.0 if unavailable (assume full ammo when unknown).
        /// </summary>
        public static float GetAmmoRatio(BotOwner bot)
        {
            if (bot?.WeaponManager?.Reload == null)
            {
                return 1f;
            }

            int max = bot.WeaponManager.Reload.MaxBulletCount;
            if (max <= 0)
            {
                return 1f;
            }

            int current = bot.WeaponManager.Reload.BulletCount;
            float ratio = (float)current / max;
            return ratio > 1f ? 1f : (ratio < 0f ? 0f : ratio);
        }

        /// <summary>
        /// Returns <c>true</c> if the bot's weapon is a close-range type
        /// (pistol, shotgun, revolver). Returns <c>false</c> if unavailable.
        /// </summary>
        public static bool IsCloseWeapon(BotOwner bot)
        {
            if (bot?.WeaponManager == null)
            {
                return false;
            }

            return bot.WeaponManager.IsCloseWeapon;
        }

        /// <summary>
        /// Returns <c>true</c> if the bot's weapon is currently on automatic fire mode.
        /// Returns <c>false</c> if unavailable.
        /// </summary>
        public static bool IsAutomatic(BotOwner bot)
        {
            if (bot?.WeaponManager?.CurrentWeaponInfo == null)
            {
                return false;
            }

            return bot.WeaponManager.CurrentWeaponInfo.IsNowAutomatic;
        }

        /// <summary>
        /// Initializes the bot's suppression fire system for squad suppression.
        /// Requires the bot to have a goal enemy to suppress toward.
        /// Returns <c>true</c> if suppression was initialized, <c>false</c> otherwise.
        /// </summary>
        public static bool TryInitSuppressionFire(BotOwner bot)
        {
            if (bot?.SuppressShoot == null)
            {
                return false;
            }

            var goalEnemy = bot.Memory?.GoalEnemy;
            if (goalEnemy == null)
            {
                return false;
            }

            try
            {
                bot.SuppressShoot.Init(goalEnemy, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the bot's suppression fire timing allows shooting.
        /// Returns <c>false</c> if unavailable.
        /// </summary>
        public static bool CanSuppressFire(BotOwner bot)
        {
            if (bot?.SuppressShoot == null)
            {
                return false;
            }

            return bot.SuppressShoot.CanShootLastTime;
        }
    }
}
