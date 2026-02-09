using EFT;
using SPTQuestingBots.BotLogic.ECS;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Static helper for transitioning a bot between our custom movement controller
    /// and BSG's native BotMover. Handles the BSG state sync that prevents stale
    /// pathfinding targets and jittering when handing control back.
    ///
    /// Pattern matches Phobos (PhobosLayer.OnLayerChanged) and SAIN (SAINLayer),
    /// which both sync the same 6 BSG state fields on deactivation.
    /// </summary>
    public static class CustomMoverHandoff
    {
        /// <summary>
        /// Activate the custom mover for a bot, taking over from BSG's BotMover.
        /// Stops BSG's mover and sets the ECS flag so BotMoverFixedUpdatePatch
        /// will skip ManualFixedUpdate for this bot.
        /// </summary>
        public static void Activate(BotOwner bot)
        {
            if (bot?.Mover == null)
                return;

            bot.Mover.Stop();
            BotEntityBridge.ActivateCustomMover(bot);
        }

        /// <summary>
        /// Deactivate the custom mover for a bot, returning control to BSG's BotMover.
        /// Syncs 6 BSG state fields to the bot's current position to prevent stale
        /// pathfinding targets, then calls SetPlayerToNavMesh to place the bot back
        /// on the NavMesh.
        ///
        /// The PrevPosLinkedTime_1 = 0f trick prevents BSG's mover from re-issuing
        /// a move command to its last target (which may be stale from before our
        /// custom mover was active).
        /// </summary>
        public static void Deactivate(BotOwner bot)
        {
            if (bot?.Mover == null)
                return;

            // Only deactivate if we were actually active
            string profileId = bot.Profile?.Id;
            if (profileId == null || !BotEntityBridge.IsCustomMoverActive(profileId))
                return;

            SyncBsgMoverState(bot);
            BotEntityBridge.DeactivateCustomMover(bot);
        }

        /// <summary>
        /// Sync BSG's BotMover state fields to the bot's current position.
        /// This prevents stale cached positions from causing phantom movement commands
        /// when BSG's mover resumes.
        ///
        /// Both Phobos and SAIN use identical state sync — 4 position fields set to
        /// current position, LastGoodCastPointTime = Time.time, PrevPosLinkedTime_1 = 0.
        /// </summary>
        private static void SyncBsgMoverState(BotOwner bot)
        {
            var mover = bot.Mover;
            var position = bot.Position;

            // Sync all cached position fields to current position
            mover.LastGoodCastPoint = position;
            mover.PrevSuccessLinkedFrom_1 = position;
            mover.PrevLinkPos = position;
            mover.PositionOnWayInner = position;

            // Mark the sync as fresh
            mover.LastGoodCastPointTime = Time.time;

            // Prevent re-issuing move command to last target
            mover.PrevPosLinkedTime_1 = 0f;

            // Place the bot back on the NavMesh
            mover.SetPlayerToNavMesh(position);
        }
    }
}
