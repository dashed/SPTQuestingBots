using System.Reflection;
using EFT;
using SPT.Reflection.Patching;

namespace SPTQuestingBots.Patches.Movement
{
    /// <summary>
    /// Override MovementContext.IsAI to return false for all bots.
    /// This gives AI bots the same movement parameters as human players,
    /// resulting in smoother, more natural movement.
    ///
    /// Both Phobos and SAIN apply this exact same patch.
    /// </summary>
    public class MovementContextIsAIPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(MovementContext)
                .GetProperty(nameof(MovementContext.IsAI), BindingFlags.Public | BindingFlags.Instance)
                ?.GetGetMethod();
        }

        [PatchPrefix]
        protected static bool PatchPrefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }
}
