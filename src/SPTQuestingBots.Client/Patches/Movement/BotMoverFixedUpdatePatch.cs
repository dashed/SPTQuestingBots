using System.Reflection;
using EFT;
using SPT.Reflection.Patching;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Patches.Movement
{
    /// <summary>
    /// Conditionally skip BSG's BotMover.ManualFixedUpdate() when QuestingBots'
    /// custom movement controller is active for a bot.
    ///
    /// Without this patch, BSG's mover would fight with our Player.Move() calls
    /// and cause jittering. Only skips for bots actively under our control.
    ///
    /// Pattern matches Phobos and SAIN, which both use the same approach.
    /// </summary>
    public class BotMoverFixedUpdatePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotMover).GetMethod(nameof(BotMover.ManualFixedUpdate), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        protected static bool PatchPrefix(BotMover __instance)
        {
            // Let BSG mover run unless our custom mover is actively controlling this bot
            string profileId = __instance.BotOwner_0?.ProfileId;
            if (profileId == null)
                return true;

            return !BotEntityBridge.IsCustomMoverActive(profileId);
        }
    }
}
