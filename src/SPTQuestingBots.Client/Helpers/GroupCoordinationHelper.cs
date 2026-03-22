using EFT;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Wraps BSG's <c>BotGroupRequestController</c> for squad command dispatch.
    /// Supplements our custom voice/tactical system — BSG's request system handles
    /// cooldowns, executor selection, validation, and negative responses natively.
    /// Access: <c>BotOwner.BotsGroup.RequestsController</c>.
    /// </summary>
    public static class GroupCoordinationHelper
    {
        /// <summary>
        /// Send a Follow request through BSG's group request system.
        /// Uses TryAskFollowMeRequest which targets a specific executor.
        /// Returns true if the request was accepted by the controller.
        /// </summary>
        public static bool TrySendFollowRequest(BotOwner requester, BotOwner executor)
        {
            if (requester?.BotsGroup?.RequestsController == null || executor == null)
                return false;

            try
            {
                var player = requester.GetPlayer;
                if (player == null)
                    return false;

                bool result = requester.BotsGroup.RequestsController.TryAskFollowMeRequest(player, executor);
                LoggingController.LogDebug("[GroupCoordinationHelper] FollowMeRequest result=" + result + " for " + requester.name);
                return result;
            }
            catch (System.Exception ex)
            {
                LoggingController.LogWarning("[GroupCoordinationHelper] FollowMeRequest failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Send a Hold request through BSG's group request system.
        /// Uses TryAskHoldRequest which targets a specific executor.
        /// </summary>
        public static void TrySendHoldRequest(BotOwner requester, BotOwner executor)
        {
            if (requester?.BotsGroup?.RequestsController == null || executor == null)
                return;

            try
            {
                var player = requester.GetPlayer;
                if (player == null)
                    return;

                requester.BotsGroup.RequestsController.TryAskHoldRequest(player, executor);
                LoggingController.LogDebug("[GroupCoordinationHelper] HoldRequest sent for " + requester.name);
            }
            catch (System.Exception ex)
            {
                LoggingController.LogWarning("[GroupCoordinationHelper] HoldRequest failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Send a GoToPoint request through BSG's group request system.
        /// Uses TryActivateGoToPointRequest which broadcasts to the group.
        /// Returns true if the request was accepted.
        /// </summary>
        public static bool TrySendGoToPointRequest(BotOwner bot, Vector3 point)
        {
            if (bot?.BotsGroup?.RequestsController == null)
                return false;

            try
            {
                var player = bot.GetPlayer;
                if (player == null)
                    return false;

                bool result = bot.BotsGroup.RequestsController.TryActivateGoToPointRequest(player, point);
                LoggingController.LogDebug(
                    "[GroupCoordinationHelper] GoToPointRequest result="
                        + result
                        + " for "
                        + bot.name
                        + " to ("
                        + point.x.ToString("F0")
                        + ","
                        + point.y.ToString("F0")
                        + ","
                        + point.z.ToString("F0")
                        + ")"
                );
                return result;
            }
            catch (System.Exception ex)
            {
                LoggingController.LogWarning("[GroupCoordinationHelper] GoToPointRequest failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Feed an investigation point into BSG's group search system.
        /// Calls <c>BotsGroup.AddPointToSearch(suspectedPoint, power, owner)</c> which
        /// notifies all group members and triggers <c>CalcGoalForBot()</c>.
        /// </summary>
        public static bool TryAddPointToSearch(BotOwner bot, Vector3 point, float power)
        {
            if (bot?.BotsGroup == null)
                return false;

            try
            {
                bot.BotsGroup.AddPointToSearch(point, power, bot);
                LoggingController.LogDebug(
                    "[GroupCoordinationHelper] AddPointToSearch for bot "
                        + bot.name
                        + " at ("
                        + point.x.ToString("F0")
                        + ","
                        + point.y.ToString("F0")
                        + ","
                        + point.z.ToString("F0")
                        + ") power="
                        + power.ToString("F0")
                );
                return true;
            }
            catch (System.Exception ex)
            {
                LoggingController.LogWarning("[GroupCoordinationHelper] AddPointToSearch failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Check if BSG's group voice system allows this bot to speak.
        /// Returns true if the bot can say the given phrase according to GroupTalk throttling.
        /// </summary>
        public static bool CanSay(BotOwner bot, EPhraseTrigger phrase)
        {
            if (bot?.BotsGroup?.GroupTalk == null)
                return true; // Allow if no group talk available

            try
            {
                return bot.BotsGroup.GroupTalk.CanSay(bot, phrase);
            }
            catch
            {
                return true; // Allow on error — don't block voice on exceptions
            }
        }

        /// <summary>
        /// Get the BSG group tactic type for a bot.
        /// Returns <see cref="GroupTacticType.None"/> (-1) if unavailable.
        /// Maps from <c>BotsGroup.BotCurrentTactic</c> enum (Attack=0, Ambush=1, Protect=2).
        /// </summary>
        public static int GetGroupTacticType(BotOwner bot)
        {
            try
            {
                var tactic = bot?.Tactic?.SubTactic;
                if (tactic == null)
                    return GroupTacticType.None;

                // BotsGroup.BotCurrentTactic: Attack=0, Ambush=1, Protect=2
                // We add 1 to distinguish from "None" (our 0 = unavailable)
                return (int)tactic.Tactic + 1;
            }
            catch
            {
                return GroupTacticType.None;
            }
        }

        /// <summary>
        /// Get the follower index from BSG's BotFollower system.
        /// Returns -1 if not a follower or unavailable.
        /// </summary>
        public static int GetFollowerIndex(BotOwner bot)
        {
            try
            {
                if (bot?.BotFollower?.HaveBoss != true)
                    return -1;

                return bot.BotFollower.Index;
            }
            catch
            {
                return -1;
            }
        }
    }

    /// <summary>
    /// Constants for BSG's group tactic types.
    /// Offset by +1 from <c>BotsGroup.BotCurrentTactic</c> enum to reserve 0 for "None".
    /// </summary>
    public static class GroupTacticType
    {
        /// <summary>No tactic assigned or unavailable.</summary>
        public const int None = 0;

        /// <summary>Aggressive forward push (BSG enum Attack=0, our value=1).</summary>
        public const int Attack = 1;

        /// <summary>Hold position and wait for targets (BSG enum Ambush=1, our value=2).</summary>
        public const int Ambush = 2;

        /// <summary>Defensive posture, stay close to leader (BSG enum Protect=2, our value=3).</summary>
        public const int Protect = 3;
    }
}
