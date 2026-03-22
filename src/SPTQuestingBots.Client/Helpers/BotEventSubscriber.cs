using Comfort.Common;
using EFT;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Subscribes to <c>BotEventHandler.OnSoundPlayed</c> and <c>OnKill</c>
    /// for canonical combat event detection. Records events into
    /// <see cref="CombatEventRegistry"/> as an additional source alongside
    /// <see cref="GrenadeExplosionSubscriber"/> and <see cref="DoorInteractionSubscriber"/>.
    ///
    /// Lifecycle: Subscribe at raid start, Unsubscribe/Clear at raid end.
    /// Follows the same pattern as DoorInteractionSubscriber.
    /// </summary>
    public static class BotEventSubscriber
    {
        private static bool _subscribed;

        /// <summary>
        /// Subscribe to BotEventHandler sound and kill events.
        /// Safe to call multiple times — prevents double-subscribe.
        /// </summary>
        public static void Subscribe()
        {
            if (_subscribed)
                return;

            if (!Singleton<BotEventHandler>.Instantiated)
            {
                LoggingController.LogWarning("[BotEventSubscriber] BotEventHandler not instantiated, cannot subscribe");
                return;
            }

            Singleton<BotEventHandler>.Instance.OnSoundPlayed += OnSoundPlayed;
            Singleton<BotEventHandler>.Instance.OnKill += OnKill;
            _subscribed = true;
            LoggingController.LogInfo("[BotEventSubscriber] Subscribed to sound and kill events");
        }

        /// <summary>
        /// Unsubscribe from all events. Call at raid end.
        /// </summary>
        public static void Unsubscribe()
        {
            if (!_subscribed)
                return;

            if (Singleton<BotEventHandler>.Instantiated)
            {
                Singleton<BotEventHandler>.Instance.OnSoundPlayed -= OnSoundPlayed;
                Singleton<BotEventHandler>.Instance.OnKill -= OnKill;
            }

            _subscribed = false;
            LoggingController.LogInfo("[BotEventSubscriber] Unsubscribed from sound and kill events");
        }

        /// <summary>
        /// Unsubscribe and reset all state.
        /// </summary>
        public static void Clear()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Whether the subscriber is currently active.
        /// </summary>
        public static bool IsSubscribed => _subscribed;

        /// <summary>
        /// Matches BSG's <c>BotEventHandler.GDelegate27</c>:
        /// <c>(IPlayer player, Vector3 position, float power, AISoundType type)</c>.
        /// Records loud sounds (power >= 30) as combat events.
        /// </summary>
        private static void OnSoundPlayed(IPlayer player, Vector3 position, float power, AISoundType type)
        {
            // Skip quiet sounds (footsteps, ambient)
            if (power < 30f)
                return;

            // Only record combat-relevant sounds
            if (type == AISoundType.step)
                return;

            LoggingController.LogDebug(
                "[BotEventSubscriber] Sound at ("
                    + position.x.ToString("F0")
                    + ","
                    + position.y.ToString("F0")
                    + ","
                    + position.z.ToString("F0")
                    + ") power="
                    + power.ToString("F0")
                    + " type="
                    + type
            );

            CombatEventRegistry.RecordEvent(
                new CombatEvent
                {
                    X = position.x,
                    Y = position.y,
                    Z = position.z,
                    Time = Time.time,
                    Power = power,
                    Type = CombatEventType.Gunshot,
                    IsBoss = false,
                    IsActive = true,
                }
            );
        }

        /// <summary>
        /// Matches BSG's <c>BotEventHandler.GDelegate20</c>:
        /// <c>(IPlayer killer, IPlayer target)</c>.
        /// Records kill events at the target's position.
        /// </summary>
        private static void OnKill(IPlayer killer, IPlayer target)
        {
            if (target == null)
                return;

            Vector3 position;
            try
            {
                position = target.Position;
            }
            catch
            {
                return;
            }

            LoggingController.LogInfo(
                "[BotEventSubscriber] Kill at ("
                    + position.x.ToString("F0")
                    + ","
                    + position.y.ToString("F0")
                    + ","
                    + position.z.ToString("F0")
                    + ")"
            );

            CombatEventRegistry.RecordEvent(
                new CombatEvent
                {
                    X = position.x,
                    Y = position.y,
                    Z = position.z,
                    Time = Time.time,
                    Power = 120f,
                    Type = CombatEventType.Death,
                    IsBoss = false,
                    IsActive = true,
                }
            );
        }
    }
}
