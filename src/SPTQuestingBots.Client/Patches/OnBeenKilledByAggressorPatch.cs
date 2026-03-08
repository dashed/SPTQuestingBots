using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using SPT.Reflection.Patching;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Telemetry;

namespace SPTQuestingBots.Patches
{
    public class OnBeenKilledByAggressorPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(Player).GetMethod(nameof(Player.OnBeenKilledByAggressor), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        protected static void PatchPostfix(Player __instance, Player aggressor)
        {
            string message = __instance.GetText();
            message += " (" + (__instance.Side == EPlayerSide.Savage ? "Scav" : "PMC") + ")";

            message += " was killed by ";

            message += aggressor.GetText();
            message += " (" + (aggressor.Side == EPlayerSide.Savage ? "Scav" : "PMC") + ")";

            Singleton<GameWorld>.Instance.TryGetComponent(out Components.Spawning.PMCGenerator pmcGenerator);
            if ((pmcGenerator != null) && pmcGenerator.HasGeneratedBots)
            {
                BotOwner[] aliveInitialPMCs = pmcGenerator.AliveBots()?.ToArray();
                if (aliveInitialPMCs != null)
                {
                    message +=
                        ". Initial PMC's remaining: "
                        + (aliveInitialPMCs.Length - (aliveInitialPMCs.Any(p => p.Id == __instance.Id) ? 1 : 0));
                }
            }

            LoggingController.LogInfo(message);

            // Make sure the bot doesn't have any active quests if it's dead
            Controllers.BotJobAssignmentFactory.FailAllJobAssignmentsForBot(__instance.Profile.Id);

            // Record death as a combat event for dynamic objective generation
            BotLogic.ECS.Systems.CombatEventRegistry.RecordEvent(
                new BotLogic.ECS.Systems.CombatEvent
                {
                    X = __instance.Position.x,
                    Y = __instance.Position.y,
                    Z = __instance.Position.z,
                    Time = UnityEngine.Time.time,
                    Power = 50f,
                    Type = BotLogic.ECS.Systems.CombatEventType.Death,
                    IsBoss = false,
                    IsActive = true,
                }
            );

            recordDeathTelemetry(__instance, aggressor);
        }

        private static void recordDeathTelemetry(Player victim, Player aggressor)
        {
            try
            {
                if (!TelemetryRecorder.IsEnabled)
                    return;

                float raidTime = UnityEngine.Time.time;
                var pos = victim.Position;

                TelemetryRecorder.RecordBotEvent(
                    raidTime,
                    victim.Id,
                    victim.Profile.Id,
                    victim.Profile.Nickname,
                    victim.Profile.Info.Settings.Role.ToString(),
                    "death",
                    aggressor?.Profile?.Nickname,
                    pos.x,
                    pos.y,
                    pos.z
                );

                string weaponName = null;
                if (aggressor.HandsController is Player.FirearmController fc)
                    weaponName = fc.Item?.ShortName?.Localized();

                float distance = UnityEngine.Vector3.Distance(victim.Position, aggressor.Position);
                TelemetryRecorder.RecordCombatEvent(
                    raidTime,
                    aggressor.Id,
                    "kill",
                    victim.Id,
                    victim.Profile.Nickname,
                    weaponName,
                    0f,
                    distance,
                    aggressor.Position.x,
                    aggressor.Position.y,
                    aggressor.Position.z
                );
            }
            catch (Exception ex)
            {
                LoggingController.LogError("[Telemetry] Failed to record death: " + ex.Message);
            }
        }
    }
}
