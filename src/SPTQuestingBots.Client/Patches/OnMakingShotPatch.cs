using System;
using System.Reflection;
using EFT;
using SPT.Reflection.Patching;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Telemetry;
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
            {
                LoggingController.LogDebug(
                    "[OnMakingShotPatch] Silenced weapon, skipping event for " + __instance.Profile.Info.Settings.Role
                );
                return;
            }

            var role = __instance.Profile.Info.Settings.Role;

            if (IsMarksmanType(role))
            {
                LoggingController.LogDebug("[OnMakingShotPatch] Marksman type " + role + ", skipping event");
                return;
            }

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
            LoggingController.LogDebug(
                "[OnMakingShotPatch] Recorded gunshot from "
                    + role
                    + " at ("
                    + __instance.Position.x.ToString("F0")
                    + ","
                    + __instance.Position.y.ToString("F0")
                    + ","
                    + __instance.Position.z.ToString("F0")
                    + ") isBoss="
                    + isBoss
            );

            recordShotTelemetry(__instance);
        }

        private static void recordShotTelemetry(Player shooter)
        {
            try
            {
                if (!TelemetryRecorder.IsEnabled)
                    return;

                var pos = shooter.Position;
                string weaponName = null;
                if (shooter.HandsController is Player.FirearmController fc)
                {
                    weaponName = fc.Item?.ShortName?.Localized();
                }

                int targetId = 0;
                string targetName = null;
                float distance = 0f;
                var botOwner = shooter.AIData?.BotOwner;
                if (botOwner?.Memory?.GoalEnemy != null)
                {
                    var enemy = botOwner.Memory.GoalEnemy;
                    var enemyPos = enemy.CurrPosition;
                    distance = Vector3.Distance(pos, enemyPos);
                    try
                    {
                        var enemyPlayer = enemy.Person as Player;
                        if (enemyPlayer != null)
                        {
                            targetId = enemyPlayer.Id;
                            targetName = enemyPlayer.Profile?.Nickname;
                        }
                    }
                    catch
                    {
                        // Swallow — enemy may not be a Player
                    }
                }

                TelemetryRecorder.RecordCombatEvent(
                    Time.time,
                    shooter.Id,
                    "shot",
                    targetId,
                    targetName,
                    weaponName,
                    0f,
                    distance,
                    pos.x,
                    pos.y,
                    pos.z
                );
            }
            catch (Exception ex)
            {
                LoggingController.LogError("[Telemetry] Failed to record shot: " + ex.Message);
            }
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
