using System.Reflection;
using EFT;
using SPT.Reflection.Patching;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Patches
{
    /// <summary>
    /// Postfix on Player.OnMakingShot to record gunshot combat events.
    /// Skips silenced weapons, marksman/sniper scavs, and tags boss shots.
    /// Ported from Vulture CombatSoundListener.GunshotListenerPatch.
    /// </summary>
    public class OnMakingShotPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(Player).GetMethod(nameof(Player.OnMakingShot), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        protected static void PatchPostfix(Player __instance)
        {
            if (__instance.HandsController is Player.FirearmController controller && controller.IsSilenced)
                return;

            var role = __instance.Profile.Info.Settings.Role;

            if (IsMarksmanType(role))
                return;

            bool isBoss = IsBossType(role);

            CombatEventRegistry.RecordEvent(
                new CombatEvent
                {
                    X = __instance.Position.x,
                    Y = __instance.Position.y,
                    Z = __instance.Position.z,
                    Time = Time.time,
                    Power = 100f,
                    Type = CombatEventType.Gunshot,
                    IsBoss = isBoss,
                    IsActive = true,
                }
            );
        }

        private static bool IsMarksmanType(WildSpawnType role)
        {
            return role == WildSpawnType.marksman || role == WildSpawnType.shooterBTR;
        }

        private static bool IsBossType(WildSpawnType role)
        {
            return role == WildSpawnType.bossKilla
                || role == WildSpawnType.bossBully
                || role == WildSpawnType.bossGluhar
                || role == WildSpawnType.bossKojaniy
                || role == WildSpawnType.bossTagilla
                || role == WildSpawnType.bossSanitar
                || role == WildSpawnType.bossKnight
                || role == WildSpawnType.followerBigPipe
                || role == WildSpawnType.followerBirdEye
                || role == WildSpawnType.sectantPriest
                || role == WildSpawnType.sectantWarrior
                || role == WildSpawnType.bossZryachiy
                || role == WildSpawnType.followerZryachiy
                || role == WildSpawnType.bossKolontay
                || role == WildSpawnType.followerKolontayAssault
                || role == WildSpawnType.followerKolontaySecurity
                || role == WildSpawnType.bossBoar
                || role == WildSpawnType.followerBoar
                || role == WildSpawnType.bossBoarSniper
                || role == WildSpawnType.bossPartisan;
        }
    }
}
