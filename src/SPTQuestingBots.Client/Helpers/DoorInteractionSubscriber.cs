using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Subscribes to BotEventHandler.OnDoorInteracted to:
    /// 1. Track door open times (for forced-open timer awareness in CloseNearbyDoorsAction)
    /// 2. Record door interactions as combat events (for bot hearing/investigation)
    ///
    /// BSG keeps doors open for 15s after a bot opens them (PERIOD_FOR_DOOR_FORCED_STAY_OPENED).
    /// BSG also fires InteractObject on door state changes, alerting nearby bots.
    /// </summary>
    public static class DoorInteractionSubscriber
    {
        private static bool _subscribed;

        /// <summary>
        /// Tracks when each door was last opened by a bot.
        /// Key: door ID (string), Value: Time.time when opened.
        /// </summary>
        private static readonly Dictionary<string, float> _doorOpenTimes = new Dictionary<string, float>();

        /// <summary>
        /// Subscribe to door interaction events. Call at raid start.
        /// Safe to call multiple times.
        /// </summary>
        public static void Subscribe()
        {
            if (_subscribed)
                return;

            if (!Singleton<BotEventHandler>.Instantiated)
            {
                LoggingController.LogWarning("[DoorInteractionSubscriber] BotEventHandler not instantiated, cannot subscribe");
                return;
            }

            Singleton<BotEventHandler>.Instance.OnDoorInteracted += OnDoorInteracted;
            _subscribed = true;
            LoggingController.LogInfo("[DoorInteractionSubscriber] Subscribed to door interactions");
        }

        /// <summary>
        /// Unsubscribe from door interaction events. Call at raid end.
        /// </summary>
        public static void Unsubscribe()
        {
            if (!_subscribed)
                return;

            if (Singleton<BotEventHandler>.Instantiated)
            {
                Singleton<BotEventHandler>.Instance.OnDoorInteracted -= OnDoorInteracted;
            }

            _subscribed = false;
            LoggingController.LogInfo("[DoorInteractionSubscriber] Unsubscribed from door interactions");
        }

        /// <summary>
        /// Unsubscribe and clear all tracked door open times.
        /// </summary>
        public static void Clear()
        {
            Unsubscribe();
            _doorOpenTimes.Clear();
        }

        /// <summary>
        /// Check if a door was opened by a bot within the specified period.
        /// Used by CloseNearbyDoorsAction to avoid re-closing recently opened doors.
        /// </summary>
        /// <param name="doorId">The door's unique ID.</param>
        /// <param name="withinSeconds">Time window in seconds (default: BSG's 15s forced-open period).</param>
        /// <returns>True if the door was opened within the specified period.</returns>
        public static bool WasRecentlyOpened(string doorId, float withinSeconds)
        {
            if (doorId == null)
                return false;

            if (!_doorOpenTimes.TryGetValue(doorId, out float openedTime))
                return false;

            return Time.time - openedTime < withinSeconds;
        }

        private static void OnDoorInteracted(int botId, WorldInteractiveObject door)
        {
            if (door == null)
                return;

            // Track the door open time
            if (door.DoorState == EDoorState.Open || door.DoorState == EDoorState.Interacting)
            {
                _doorOpenTimes[door.Id] = Time.time;

                LoggingController.LogDebug(
                    "[DoorInteractionSubscriber] Door " + door.Id + " opened by bot " + botId + " at " + Time.time.ToString("F1")
                );
            }

            // Record as a combat event so nearby bots react to the door sound
            var doorConfig = ConfigController.Config?.Questing?.UnlockingDoors;
            if (doorConfig?.EnableDoorSoundEvents == true)
            {
                var doorPosition = door.transform.position;
                float power = doorConfig.DoorSoundPower;

                CombatEventRegistry.RecordEvent(
                    new CombatEvent
                    {
                        X = doorPosition.x,
                        Y = doorPosition.y,
                        Z = doorPosition.z,
                        Time = Time.time,
                        Power = power,
                        Type = CombatEventType.DoorOpen,
                        IsBoss = false,
                        IsActive = true,
                    }
                );
            }
        }
    }
}
