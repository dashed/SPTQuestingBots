using Comfort.Common;
using EFT;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Subscribes to BotEventHandler.OnGrenadeExplosive and records explosion
    /// combat events into CombatEventRegistry. Filters out smoke grenades.
    /// Ported from Vulture CombatSoundListener grenade handling.
    /// </summary>
    public static class GrenadeExplosionSubscriber
    {
        private static bool _subscribed;

        /// <summary>
        /// Subscribe to grenade explosions. Call at raid start.
        /// Safe to call multiple times — prevents double-subscribe.
        /// </summary>
        public static void Subscribe()
        {
            if (_subscribed)
                return;

            if (!Singleton<BotEventHandler>.Instantiated)
            {
                LoggingController.LogWarning("GrenadeExplosionSubscriber: BotEventHandler not instantiated, cannot subscribe");
                return;
            }

            Singleton<BotEventHandler>.Instance.OnGrenadeExplosive += OnGrenadeExplosion;
            _subscribed = true;
        }

        /// <summary>
        /// Unsubscribe from grenade explosions. Call at raid end.
        /// </summary>
        public static void Unsubscribe()
        {
            if (!_subscribed)
                return;

            if (Singleton<BotEventHandler>.Instantiated)
            {
                Singleton<BotEventHandler>.Instance.OnGrenadeExplosive -= OnGrenadeExplosion;
            }

            _subscribed = false;
        }

        /// <summary>
        /// Unsubscribe and reset all state.
        /// </summary>
        public static void Clear()
        {
            Unsubscribe();
        }

        private static void OnGrenadeExplosion(
            Vector3 explosionPosition,
            string playerProfileID,
            bool isSmoke,
            float smokeRadius,
            float smokeLifeTime,
            int throwableId
        )
        {
            if (isSmoke)
                return;

            CombatEventRegistry.RecordEvent(
                new CombatEvent
                {
                    X = explosionPosition.x,
                    Y = explosionPosition.y,
                    Z = explosionPosition.z,
                    Time = Time.time,
                    Power = 150f,
                    Type = CombatEventType.Explosion,
                    IsBoss = false,
                    IsActive = true,
                }
            );
        }
    }
}
