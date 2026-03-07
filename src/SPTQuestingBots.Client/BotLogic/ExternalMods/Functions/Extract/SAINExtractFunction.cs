using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EFT;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Telemetry;

namespace SPTQuestingBots.BotLogic.ExternalMods.Functions.Extract
{
    public class SAINExtractFunction : AbstractExtractFunction
    {
        public override string MonitoredLayerName => "SAIN : Extract";

        public SAINExtractFunction(BotOwner _botOwner)
            : base(_botOwner) { }

        public override bool IsTryingToExtract() => IsMonitoredLayerActive();

        private bool tryExtractSingleBot(BotOwner botOwner) => SAIN.Plugin.SAINInterop.TryExtractBot(botOwner);

        private bool trySetExfilForBot(BotOwner botOwner) => SAIN.Plugin.SAINInterop.TrySetExfilForBot(botOwner);

        public override bool TryInstructBotToExtract()
        {
            if (!tryExtractSingleBot(BotOwner))
            {
                LoggingController.LogWarning(
                    "Cannot instruct " + BotOwner.GetText() + " to extract. SAIN Interop not initialized properly or is outdated."
                );

                return false;
            }

            LoggingController.LogDebug("Instructing " + BotOwner.GetText() + " to extract now");
            recordExtractTelemetry(BotOwner);

            foreach (BotOwner follower in ECS.BotEntityBridge.GetFollowers(BotOwner))
            {
                if ((follower == null) || follower.IsDead)
                {
                    continue;
                }

                if (tryExtractSingleBot(follower))
                {
                    LoggingController.LogDebug("Instructing follower " + follower.GetText() + " to extract now");
                    recordExtractTelemetry(follower);
                }
                else
                {
                    LoggingController.LogWarning(
                        "Could not instruct follower "
                            + follower.GetText()
                            + " to extract now. SAIN Interop not initialized properly or is outdated."
                    );
                }
            }

            if (!trySetExfilForBot(BotOwner))
            {
                LoggingController.LogWarning("Could not find an extract for " + BotOwner.GetText());
                return false;
            }

            return true;
        }

        private static void recordExtractTelemetry(BotOwner bot)
        {
            try
            {
                if (!TelemetryRecorder.IsEnabled)
                    return;

                var pos = bot.Position;
                TelemetryRecorder.RecordBotEvent(
                    UnityEngine.Time.time,
                    bot.Id,
                    bot.Profile.Id,
                    bot.Profile.Nickname,
                    bot.Profile.Info.Settings.Role.ToString(),
                    "extract",
                    "sain",
                    pos.x,
                    pos.y,
                    pos.z
                );
            }
            catch (Exception ex)
            {
                LoggingController.LogError("[Telemetry] Failed to record extract: " + ex.Message);
            }
        }
    }
}
