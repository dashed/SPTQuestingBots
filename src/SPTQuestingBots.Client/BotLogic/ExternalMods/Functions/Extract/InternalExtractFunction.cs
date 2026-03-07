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
    public class InternalExtractFunction : AbstractExtractFunction
    {
        public override string MonitoredLayerName => "Exfiltration";

        public InternalExtractFunction(BotOwner _botOwner)
            : base(_botOwner)
        {
            _botOwner.Exfiltration.TimeToExfiltration = float.MaxValue;
        }

        public override bool IsTryingToExtract() => BotOwner.Exfiltration.WannaLeave();

        public override bool TryInstructBotToExtract()
        {
            tryExtractSingleBot(BotOwner);
            LoggingController.LogDebug("Instructing " + BotOwner.GetText() + " to extract now");
            recordExtractTelemetry(BotOwner);

            foreach (BotOwner follower in ECS.BotEntityBridge.GetFollowers(BotOwner))
            {
                if ((follower == null) || follower.IsDead)
                {
                    continue;
                }

                tryExtractSingleBot(follower);
                LoggingController.LogDebug("Instructing follower " + follower.GetText() + " to extract now");
                recordExtractTelemetry(follower);
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
                    null,
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

        private bool tryExtractSingleBot(BotOwner botOwner)
        {
            // Game time > _timeToExfiltration ? exfil now
            botOwner.Exfiltration.TimeToExfiltration = 0f;

            return true;
        }
    }
}
