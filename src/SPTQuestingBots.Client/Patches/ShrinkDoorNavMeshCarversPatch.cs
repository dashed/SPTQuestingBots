using System.Collections.Generic;
using System.Reflection;
using EFT;
using SPT.Reflection.Patching;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Patches
{
    /// <summary>
    /// Shrink door NavMesh carvers to 37.5% of their original size to prevent
    /// narrow hallways from being completely blocked off on the navmesh by open doors.
    ///
    /// Also initializes the door collider cache for per-bot collision bypass.
    ///
    /// Ported from Phobos ShrinkDoorNavMeshCarversPatch (Patches/NavMesh.cs).
    /// </summary>
    public class ShrinkDoorNavMeshCarversPatch : ModulePatch
    {
        private const float CarverScaleFactor = 0.375f;

        protected override MethodBase GetTargetMethod()
        {
            return typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        protected static void PatchPostfix()
        {
            // Cache door colliders for per-bot collision bypass
            DoorCollisionHelper.Initialize();

            // Shrink door NavMesh carvers
            var doorsController = UnityEngine.Object.FindObjectOfType<BotDoorsController>();
            if (doorsController == null)
            {
                LoggingController.LogWarning("ShrinkDoorNavMeshCarversPatch: BotDoorsController not found");
                return;
            }

            var processed = new HashSet<NavMeshDoorLink>();

            for (int i = 0; i < doorsController._navMeshDoorLinks.Count; i++)
            {
                var doorLink = doorsController._navMeshDoorLinks[i];

                if (!processed.Add(doorLink))
                    continue;

                doorLink.Carver_Opened.size = CarverScaleFactor * doorLink.Carver_Opened.size;
                doorLink.Carver_Closed.size = CarverScaleFactor * doorLink.Carver_Closed.size;
                doorLink.Carver_Breached.size = CarverScaleFactor * doorLink.Carver_Breached.size;
            }

            LoggingController.LogInfo(
                $"ShrinkDoorNavMeshCarversPatch: shrunk {processed.Count} door NavMesh carvers to {CarverScaleFactor * 100}%"
            );
        }
    }
}
