using EFT;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Bridges pure-logic callout decisions to BSG's voice line API.
    /// Calls Player.Say() which works with both vanilla BSG and SAIN.
    /// </summary>
    public static class SquadVoiceHelper
    {
        /// <summary>
        /// Attempt to play a voice line for the given bot.
        /// Returns false if the bot is already speaking or calloutId is None.
        /// </summary>
        public static bool TrySay(BotOwner bot, int calloutId, bool aggressive = false)
        {
            if (calloutId == SquadCalloutId.None)
                return false;

            var player = bot?.GetPlayer;
            if (player == null)
                return false;

            // Don't interrupt ongoing speech
            if (player.Speaker != null && (player.Speaker.Speaking || player.Speaker.Busy))
                return false;

            var phrase = MapCalloutToPhrase(calloutId);
            var mask = BuildMask(bot);
            player.Say(phrase, true, 0f, mask, aggressive ? 1 : 0);
            return true;
        }

        private static EPhraseTrigger MapCalloutToPhrase(int calloutId)
        {
            switch (calloutId)
            {
                case SquadCalloutId.FollowMe:
                    return EPhraseTrigger.FollowMe;
                case SquadCalloutId.Gogogo:
                    return EPhraseTrigger.Gogogo;
                case SquadCalloutId.HoldPosition:
                    return EPhraseTrigger.HoldPosition;
                case SquadCalloutId.Roger:
                    return EPhraseTrigger.Roger;
                case SquadCalloutId.Going:
                    return EPhraseTrigger.Going;
                case SquadCalloutId.OnPosition:
                    return EPhraseTrigger.OnPosition;
                case SquadCalloutId.OnSix:
                    return EPhraseTrigger.OnSix;
                case SquadCalloutId.LeftFlank:
                    return EPhraseTrigger.LeftFlank;
                case SquadCalloutId.RightFlank:
                    return EPhraseTrigger.RightFlank;
                case SquadCalloutId.InTheFront:
                    return EPhraseTrigger.InTheFront;
                case SquadCalloutId.Covering:
                    return EPhraseTrigger.Covering;
                case SquadCalloutId.OnFight:
                    return EPhraseTrigger.OnFight;
                case SquadCalloutId.OnFirstContact:
                    return EPhraseTrigger.OnFirstContact;
                default:
                    return EPhraseTrigger.MumblePhrase;
            }
        }

        private static ETagStatus BuildMask(BotOwner bot)
        {
            // Start with Coop (squad communication)
            var mask = ETagStatus.Coop;

            // Add awareness state
            if (bot.Memory?.IsUnderFire == true)
                mask |= ETagStatus.Combat;
            else if (bot.Memory?.GoalEnemy != null)
                mask |= ETagStatus.Aware;
            else
                mask |= ETagStatus.Unaware;

            return mask;
        }
    }
}
