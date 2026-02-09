using System.Reflection;
using EFT;
using SPT.Reflection.Patching;

namespace SPTQuestingBots.Patches.Movement
{
    /// <summary>
    /// Enable the vaulting component for AI bots by setting aiControlled to false
    /// in Player.InitVaultingComponent(). Bots with simplified skeletons are skipped.
    ///
    /// Required for stuck recovery via MovementContext.TryVaulting() to work on AI bots.
    ///
    /// Both Phobos and SAIN apply this exact same patch.
    /// </summary>
    public class EnableVaultPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(Player).GetMethod(nameof(Player.InitVaultingComponent), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        protected static void PatchPrefix(Player __instance, ref bool aiControlled)
        {
            // Don't enable vaulting for simplified-skeleton bots (e.g., non-rendered bots)
            if (__instance.UsedSimplifiedSkeleton)
                return;

            aiControlled = false;
        }
    }
}
