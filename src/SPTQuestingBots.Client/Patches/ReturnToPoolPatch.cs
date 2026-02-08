using System;
using System.Reflection;
using EFT.AssetsManager;
using SPT.Reflection.Patching;
using UnityEngine;

namespace SPTQuestingBots.Patches
{
    internal class ReturnToPoolPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(AssetPoolObject).GetMethod(nameof(AssetPoolObject.ReturnToPool), new Type[] { typeof(GameObject), typeof(bool) });
        }

        [PatchPrefix]
        protected static void PatchPrefix(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<Components.BotObjectiveManager>(out var objectiveManager))
            {
                UnityEngine.Object.Destroy(objectiveManager);
            }
        }
    }
}
