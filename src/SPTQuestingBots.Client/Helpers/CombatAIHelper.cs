using EFT;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Null-safe static helpers for combat AI enhancements.
    /// Reads BSG combat state (cover, dogfight, aggression, enemy scoring)
    /// and provides methods to trigger aggression changes and flanking requests.
    /// </summary>
    public static class CombatAIHelper
    {
        // ── Aggression System Extension (Item 12) ──────────────────────

        /// <summary>
        /// Apply additional aggression triggers beyond BSG's default two
        /// (ally death + nearby kill). Calls <c>BotOwner.Tactic.AggressionChange()</c>
        /// with a computed delta based on damage, combat duration, enemy count, and ammo.
        /// </summary>
        /// <param name="bot">The bot to modify aggression for.</param>
        /// <param name="damageTakenNormalized">Damage taken as fraction of max health (0-1).</param>
        /// <param name="timeInCombatSeconds">How long the bot has been in active combat.</param>
        /// <param name="enemyCount">Number of known enemies.</param>
        /// <param name="ammoFraction">Remaining ammo as fraction (0-1). Lower = more desperate.</param>
        public static void ApplyAggressionTriggers(
            BotOwner bot,
            float damageTakenNormalized,
            float timeInCombatSeconds,
            int enemyCount,
            float ammoFraction
        )
        {
            if (bot?.Tactic == null)
                return;

            float delta = 0f;

            // Damage taken: getting hurt increases aggression (fight-or-flight)
            if (damageTakenNormalized > 0f)
            {
                delta += damageTakenNormalized * 0.3f;
            }

            // Prolonged combat: aggression rises over time (frustration/desperation)
            if (timeInCombatSeconds > 10f)
            {
                float timeComponent = System.Math.Min(timeInCombatSeconds / 60f, 1f) * 0.2f;
                delta += timeComponent;
            }

            // Multiple enemies: outnumbered bots get more aggressive (cornered animal)
            if (enemyCount > 1)
            {
                delta += System.Math.Min((enemyCount - 1) * 0.1f, 0.3f);
            }

            // Low ammo: desperation increases aggression (need to finish fight)
            if (ammoFraction < 0.3f && ammoFraction >= 0f)
            {
                delta += (0.3f - ammoFraction) * 0.5f;
            }

            if (delta > 0f)
            {
                bot.Tactic.AggressionChange(delta);
            }
        }

        // ── DogFight State Reading (Item 13) ──────────────────────────

        /// <summary>
        /// Returns true if the bot is in an active dogfight state.
        /// A dogfight means close-quarters combat where the bot engages aggressively.
        /// </summary>
        public static bool IsInDogFight(BotOwner bot)
        {
            if (bot?.DogFight == null)
                return false;

            try
            {
                return bot.DogFight.DogFightState == BotDogFightStatus.dogFight;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads the BSG DogFight entry/exit distance thresholds from the bot's
        /// mind settings. Returns (dogFightIn, dogFightOut) or null if unavailable.
        /// </summary>
        public static (float In, float Out)? GetDogFightThresholds(BotOwner bot)
        {
            try
            {
                var mind = bot?.Settings?.FileSettings?.Mind;
                if (mind == null)
                    return null;

                return (mind.DOG_FIGHT_IN, mind.DOG_FIGHT_OUT);
            }
            catch
            {
                return null;
            }
        }

        // ── Cover State Reading (Item 14) ─────────────────────────────

        /// <summary>
        /// Returns true if the bot considers itself to be in cover.
        /// </summary>
        public static bool IsInCover(BotOwner bot)
        {
            if (bot?.Memory == null)
                return false;

            try
            {
                return bot.Memory.IsInCover;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if the bot has an active cover search result.
        /// </summary>
        public static bool HasCoverPoint(BotOwner bot)
        {
            try
            {
                return bot?.Covers?.LastSearchData != null;
            }
            catch
            {
                return false;
            }
        }

        // ── Flanking Trigger (Item 24) ────────────────────────────────

        /// <summary>
        /// Attempts to request a coordinated flank for the bot's group.
        /// Uses <c>BotGroupRequestController.TryActivateGoToPointRequest()</c> on the
        /// bot's group to move members toward the specified position.
        /// Returns true if the request was successfully sent.
        /// </summary>
        public static bool TryRequestFlank(BotOwner bot, Vector3 targetPosition)
        {
            if (bot?.BotsGroup?.RequestsController == null)
                return false;

            try
            {
                var player = bot.GetPlayer;
                if (player == null)
                    return false;

                return bot.BotsGroup.RequestsController.TryActivateGoToPointRequest(player, targetPosition);
            }
            catch
            {
                return false;
            }
        }

        // ── Push/Suppress Capability (Item 25) ────────────────────────

        /// <summary>
        /// Returns true if the bot has push/suppress capability.
        /// BSG's push/suppress layer (priority 58) is only enabled for hard+ difficulty.
        /// We check the bot's difficulty setting directly.
        /// </summary>
        public static bool HasPushCapability(BotOwner bot)
        {
            try
            {
                if (bot?.Profile?.Info == null)
                    return false;

                // BotDifficulty: easy=0, normal=1, hard=2, impossible=3
                int difficulty = (int)bot.Profile.Info.Settings.BotDifficulty;
                return difficulty >= 2;
            }
            catch
            {
                return false;
            }
        }

        // ── Enemy Scoring Context (Item 37) ───────────────────────────

        /// <summary>
        /// Reads enemy scoring context: goal enemy distance, visibility, and whether the
        /// bot is actively tracking an enemy. Used by personality system to modulate
        /// combat task scoring.
        /// </summary>
        public static EnemyScoringContext GetEnemyScoringContext(BotOwner bot)
        {
            var ctx = new EnemyScoringContext();

            try
            {
                var goalEnemy = bot?.Memory?.GoalEnemy;
                if (goalEnemy == null || goalEnemy.Person == null || !goalEnemy.Person.HealthController.IsAlive)
                    return ctx;

                ctx.HasEnemy = true;
                ctx.Distance = goalEnemy.Distance_1;
                ctx.IsVisible = goalEnemy.IsVisible;
                ctx.TimeSinceLastSeen =
                    goalEnemy.PersonalLastSeenTime > 0f ? UnityEngine.Time.time - goalEnemy.PersonalLastSeenTime : float.MaxValue;
            }
            catch
            {
                // Leave defaults
            }

            return ctx;
        }

        // ── Prone State (Item 38) ────────────────────────────────────

        /// <summary>
        /// Returns true if the bot is currently prone (lying down).
        /// Used to verify room clearing handles prone-to-stand transitions.
        /// </summary>
        public static bool IsProne(BotOwner bot)
        {
            try
            {
                return bot?.BotLay?.IsLay == true;
            }
            catch
            {
                return false;
            }
        }
    }
}
